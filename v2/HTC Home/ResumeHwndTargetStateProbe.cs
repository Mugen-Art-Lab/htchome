using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HTCHome
{
    // Passive diagnostics for the four-way resume matrix.
    //
    // The matrix has shown that WPF Hide and Minimize survive a bad wake while
    // Baseline and DWM Cloak can acquire the poisoned MediaSystem/DUCE state.
    // This probe does not change presentation state. It only snapshots the
    // existing HwndTarget and selected private WPF state so we can identify the
    // exact state transition shared by Hide + Minimize but absent from Cloak.
    internal static class ResumeHwndTargetStateProbe
    {
        private sealed class TargetRecord
        {
            public IntPtr Hwnd;
            public HwndTarget Target;
            public string WindowType;
            public string CachedWindowState;
            public string CachedTargetState;
            public DateTime CachedUtc;
        }

        private static readonly object Sync = new object();
        private static readonly string[] TargetFields =
        {
            "_isSuspended",
            "_needsRePresentOnWake",
            "_hasRePresentedSinceWake",
            "_isRenderTargetEnabled",
            "_disableCookie",
            "_isMinimized",
            "_isSessionDisconnected",
            "_lastWakeOrUnlockEvent"
        };

        private static readonly List<TargetRecord> Targets = new List<TargetRecord>();
        private static DispatcherTimer cacheTimer;
        private static bool startQueued;
        private static bool subscribed;
        private static int generation;

        public static bool Start()
        {
            if (!IsProfileProcess())
                return true;

            lock (Sync)
            {
                if (startQueued)
                    return true;
                startQueued = true;
            }

            // Queue subscription until the App static initialization has completed.
            // ResumeRenderRecovery subscribes synchronously, so our Suspend handler
            // runs after the matrix has applied Hide/Cloak/Minimize. This gives us
            // a real post-intervention snapshot without changing matrix behavior.
            try
            {
                Application app = Application.Current;
                if (app != null)
                {
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(SubscribeAndStartCache));
                }
            }
            catch (Exception ex)
            {
                SafeLog("[HwndTargetProbe] START_QUEUE_FAILED " + ex);
            }

            return true;
        }

        private static void SubscribeAndStartCache()
        {
            if (subscribed)
                return;
            subscribed = true;

            try
            {
                SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
                SystemEvents.DisplaySettingsChanging += SystemEvents_DisplaySettingsChanging;
                SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

                cacheTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle);
                cacheTimer.Interval = TimeSpan.FromSeconds(1);
                cacheTimer.Tick += delegate { RefreshCache(); };
                cacheTimer.Start();

                RefreshCache();
                SafeLog("[HwndTargetProbe] ARMED profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode() +
                    " fields=" + string.Join(",", TargetFields));
            }
            catch (Exception ex)
            {
                SafeLog("[HwndTargetProbe] SUBSCRIBE_FAILED " + ex);
            }
        }

        private static void RefreshCache()
        {
            Application app = Application.Current;
            if (app == null)
                return;

            try
            {
                var updated = new List<TargetRecord>();

                foreach (Window window in app.Windows)
                {
                    if (window == null)
                        continue;

                    IntPtr hwnd = IntPtr.Zero;
                    HwndTarget target = null;
                    try
                    {
                        hwnd = new WindowInteropHelper(window).Handle;
                        if (hwnd == IntPtr.Zero)
                            continue;

                        HwndSource source = HwndSource.FromHwnd(hwnd);
                        if (source != null)
                            target = source.CompositionTarget;
                    }
                    catch { }

                    if (hwnd == IntPtr.Zero || target == null)
                        continue;

                    TargetRecord record = new TargetRecord();
                    record.Hwnd = hwnd;
                    record.Target = target;
                    record.WindowType = window.GetType().FullName;
                    record.CachedUtc = DateTime.UtcNow;
                    record.CachedWindowState =
                        "visible=" + window.IsVisible +
                        " state=" + window.WindowState +
                        " active=" + window.IsActive +
                        " loaded=" + window.IsLoaded +
                        " targetId=" + RuntimeHelpers.GetHashCode(target) +
                        " renderMode=" + SafeRenderMode(target) +
                        " tier=" + (RenderCapability.Tier >> 16);
                    record.CachedTargetState = ReadTargetState(target);
                    updated.Add(record);
                }

                lock (Sync)
                {
                    Targets.Clear();
                    Targets.AddRange(updated);
                }
            }
            catch (Exception ex)
            {
                SafeLog("[HwndTargetProbe] CACHE_FAILED type=" + ex.GetType().FullName + " msg=" + ex.Message);
            }
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                // Cached state is from the last healthy UI-thread tick and therefore
                // represents the pre-intervention state. Live state is read after the
                // matrix Suspend handler has already applied its selected mode.
                LogCachedTargets("SUSPEND_PRE_CACHED", generation);
                LogLiveTargets("SUSPEND_POST", generation);
                return;
            }

            if (e.Mode != PowerModes.Resume)
                return;

            int currentGeneration = Interlocked.Increment(ref generation);
            LogLiveTargets("RESUME+0", currentGeneration);
            StartTimeline(currentGeneration);
        }

        private static void SystemEvents_DisplaySettingsChanging(object sender, EventArgs e)
        {
            LogLiveTargets("DISPLAY_CHANGING", generation);
        }

        private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            LogLiveTargets("DISPLAY_CHANGED", generation);
        }

        private static void StartTimeline(int currentGeneration)
        {
            Thread thread = new Thread(new ThreadStart(delegate
            {
                int[] offsets = { 250, 1000, 3000, 10000, 12000, 21000, 24000, 30000 };
                int previous = 0;

                foreach (int offset in offsets)
                {
                    int delay = offset - previous;
                    if (delay > 0)
                        Thread.Sleep(delay);
                    previous = offset;

                    if (currentGeneration != generation)
                        return;

                    LogLiveTargets("RESUME+" + FormatOffset(offset), currentGeneration);
                }
            }));

            thread.IsBackground = true;
            thread.Name = "Mugen HwndTarget State Timeline";
            thread.Start();
        }

        private static string FormatOffset(int milliseconds)
        {
            if (milliseconds < 1000)
                return milliseconds.ToString(CultureInfo.InvariantCulture) + "ms";
            if (milliseconds % 1000 == 0)
                return (milliseconds / 1000).ToString(CultureInfo.InvariantCulture) + "s";
            return (milliseconds / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + "s";
        }

        private static void LogCachedTargets(string label, int currentGeneration)
        {
            List<TargetRecord> snapshot;
            lock (Sync)
                snapshot = new List<TargetRecord>(Targets);

            if (snapshot.Count == 0)
            {
                SafeLog("[HwndTargetProbe] SNAPSHOT label=" + label +
                    " generation=" + currentGeneration +
                    " profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode() +
                    " targets=0");
                return;
            }

            DateTime now = DateTime.UtcNow;
            foreach (TargetRecord record in snapshot)
            {
                long ageMs = (long)Math.Max(0, (now - record.CachedUtc).TotalMilliseconds);
                SafeLog("[HwndTargetProbe] TARGET label=" + label +
                    " generation=" + currentGeneration +
                    " profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode() +
                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                    " windowType=" + record.WindowType +
                    " cacheAgeMs=" + ageMs +
                    " " + record.CachedWindowState +
                    " " + record.CachedTargetState);
            }
        }

        private static void LogLiveTargets(string label, int currentGeneration)
        {
            List<TargetRecord> snapshot;
            lock (Sync)
                snapshot = new List<TargetRecord>(Targets);

            if (snapshot.Count == 0)
            {
                SafeLog("[HwndTargetProbe] SNAPSHOT label=" + label +
                    " generation=" + currentGeneration +
                    " profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode() +
                    " targets=0");
                return;
            }

            foreach (TargetRecord record in snapshot)
            {
                SafeLog("[HwndTargetProbe] TARGET label=" + label +
                    " generation=" + currentGeneration +
                    " profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode() +
                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                    " windowType=" + record.WindowType +
                    " targetId=" + RuntimeHelpers.GetHashCode(record.Target) +
                    " tier=" + (RenderCapability.Tier >> 16) +
                    " " + ReadTargetState(record.Target));
            }
        }

        private static string ReadTargetState(HwndTarget target)
        {
            if (target == null)
                return "target=<null>";

            var parts = new List<string>();
            foreach (string fieldName in TargetFields)
                parts.Add(fieldName + "=" + ReadField(target, fieldName));
            return string.Join(" ", parts.ToArray());
        }

        private static string ReadField(object instance, string fieldName)
        {
            try
            {
                FieldInfo field = FindField(instance.GetType(), fieldName);
                if (field == null)
                    return "<missing>";
                return FormatValue(field.GetValue(instance));
            }
            catch (Exception ex)
            {
                return "<error:" + ex.GetType().Name + ">";
            }
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, flags);
                if (field != null)
                    return field;
                type = type.BaseType;
            }
            return null;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "<null>";
            if (value is DateTime)
                return ((DateTime)value).ToString("O", CultureInfo.InvariantCulture);
            if (value is IntPtr)
                return "0x" + ((IntPtr)value).ToInt64().ToString("X");
            try
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture)
                    .Replace("\r", " ").Replace("\n", " ");
            }
            catch
            {
                return "<" + value.GetType().Name + ">";
            }
        }

        private static string SafeRenderMode(HwndTarget target)
        {
            try { return target.RenderMode.ToString(); }
            catch { return "<unavailable>"; }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                SafeLog("[HwndTargetProbe] DOMAIN_UNHANDLED generation=" + generation +
                    " profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode() +
                    " terminating=" + e.IsTerminating +
                    " exception=" + (e.ExceptionObject == null ? "<null>" : e.ExceptionObject.ToString()));
                LogLiveTargets("DOMAIN_UNHANDLED", generation);
            }
            catch { }
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            try
            {
                SafeLog("[HwndTargetProbe] PROCESS_EXIT generation=" + generation +
                    " profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode());
                LogLiveTargets("PROCESS_EXIT", generation);
            }
            catch { }
        }

        private static bool IsProfileProcess()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase) ||
                    args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetProfileId()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length ? args[i + 1] : "<missing>";
                if (args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring("--profile=".Length);
            }
            return "<none>";
        }

        private static string GetDiagnosticMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--resume-hide-control", StringComparison.OrdinalIgnoreCase))
                    return "hide";
                if (string.Equals(args[i], "--resume-diag", StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length ? args[i + 1] : "normal";
                if (args[i].StartsWith("--resume-diag=", StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring("--resume-diag=".Length);
            }
            return "normal";
        }

        private static void SafeLog(string message)
        {
            try { App.Log(message); }
            catch { }
        }
    }

    public partial class App
    {
        private static readonly bool ResumeHwndTargetStateProbeBootstrap =
            ResumeHwndTargetStateProbe.Start();
    }
}
