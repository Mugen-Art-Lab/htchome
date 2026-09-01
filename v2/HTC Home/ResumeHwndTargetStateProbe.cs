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
    // Passive state recorder. Run #55 exposed that the earlier diagnostic used the
    // non-existent field name _renderOp; .NET Framework WPF actually uses
    // _currentRenderOp. This version records the real DispatcherOperation and the
    // promotion/render timers so we can see whether PostRender actually queued work.
    internal static class ResumeHwndTargetStateProbe
    {
        private sealed class TargetRecord
        {
            public IntPtr Hwnd;
            public HwndTarget Target;
            public object MediaContext;
            public string WindowType;
            public string CachedWindowState;
            public string CachedTargetState;
            public string CachedMediaContextState;
            public DateTime CachedUtc;
        }

        private static readonly object Sync = new object();
        private static readonly string[] TargetFields =
        {
            "_isSuspended", "_needsRePresentOnWake", "_hasRePresentedSinceWake",
            "_isRenderTargetEnabled", "_disableCookie", "_isMinimized",
            "_isSessionDisconnected", "_lastWakeOrUnlockEvent", "_windowPosChanging", "_userInputResize"
        };

        private static readonly string[] MediaContextFields =
        {
            "_interlockState", "_needToCommitChannel", "_commitPendingAfterRender",
            "_animationRenderRate", "_lastPresentationResults", "_lastCommitTime",
            "_isRendering", "_isDisposed", "_isConnected", "_isDisconnecting",
            "_currentRenderOp", "_inputMarkerOp", "_promoteRenderOpToInput",
            "_promoteRenderOpToRender", "_estimatedNextVSyncTimer"
        };

        private static readonly List<TargetRecord> Targets = new List<TargetRecord>();
        private static DispatcherTimer cacheTimer;
        private static bool startQueued;
        private static bool subscribed;
        private static int generation;

        public static bool Start()
        {
            if (!IsProfileProcess()) return true;
            lock (Sync)
            {
                if (startQueued) return true;
                startQueued = true;
            }

            try
            {
                Application app = Application.Current;
                if (app != null)
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(SubscribeAndStartCache));
            }
            catch (Exception ex) { SafeLog("[HwndTargetProbe] START_QUEUE_FAILED " + ex); }
            return true;
        }

        private static void SubscribeAndStartCache()
        {
            if (subscribed) return;
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

                SafeLog("[HwndTargetProbe] ARMED profile=" + GetProfileId() + " mode=" + GetDiagnosticMode() +
                    " targetFields=" + string.Join(",", TargetFields) +
                    " mediaFields=" + string.Join(",", MediaContextFields));
            }
            catch (Exception ex) { SafeLog("[HwndTargetProbe] SUBSCRIBE_FAILED " + ex); }
        }

        private static void RefreshCache()
        {
            Application app = Application.Current;
            if (app == null) return;
            try
            {
                var updated = new List<TargetRecord>();
                foreach (Window window in app.Windows)
                {
                    if (window == null) continue;
                    IntPtr hwnd = IntPtr.Zero;
                    HwndTarget target = null;
                    try
                    {
                        hwnd = new WindowInteropHelper(window).Handle;
                        if (hwnd == IntPtr.Zero) continue;
                        HwndSource source = HwndSource.FromHwnd(hwnd);
                        if (source != null) target = source.CompositionTarget;
                    }
                    catch { }
                    if (hwnd == IntPtr.Zero || target == null) continue;

                    object mediaContext = GetExistingMediaContext(target);
                    TargetRecord record = new TargetRecord();
                    record.Hwnd = hwnd;
                    record.Target = target;
                    record.MediaContext = mediaContext;
                    record.WindowType = window.GetType().FullName;
                    record.CachedUtc = DateTime.UtcNow;
                    record.CachedWindowState = "visible=" + window.IsVisible +
                        " state=" + window.WindowState + " active=" + window.IsActive +
                        " loaded=" + window.IsLoaded + " targetId=" + RuntimeHelpers.GetHashCode(target) +
                        " renderMode=" + SafeRenderMode(target);
                    record.CachedTargetState = ReadState(target, TargetFields);
                    record.CachedMediaContextState = ReadMediaContextState(mediaContext);
                    updated.Add(record);
                }
                lock (Sync)
                {
                    Targets.Clear(); Targets.AddRange(updated);
                }
            }
            catch (Exception ex)
            {
                SafeLog("[HwndTargetProbe] CACHE_FAILED type=" + ex.GetType().FullName + " msg=" + ex.Message);
            }
        }

        private static object GetExistingMediaContext(HwndTarget target)
        {
            if (target == null) return null;
            try
            {
                Type type = typeof(Visual).Assembly.GetType("System.Windows.Media.MediaContext", false);
                if (type == null) return null;
                MethodInfo from = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic,
                    null, new[] { typeof(Dispatcher) }, null);
                if (from == null) return null;
                return from.Invoke(null, new object[] { target.Dispatcher });
            }
            catch { return null; }
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                LogCachedTargets("SUSPEND_PRE_CACHED", generation);
                LogLiveTargets("SUSPEND_POST", generation);
                return;
            }
            if (e.Mode != PowerModes.Resume) return;
            int currentGeneration = Interlocked.Increment(ref generation);
            LogLiveTargets("RESUME+0", currentGeneration);
            StartTimeline(currentGeneration);
        }

        private static void SystemEvents_DisplaySettingsChanging(object sender, EventArgs e) { LogLiveTargets("DISPLAY_CHANGING", generation); }
        private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e) { LogLiveTargets("DISPLAY_CHANGED", generation); }

        private static void StartTimeline(int currentGeneration)
        {
            Thread thread = new Thread(new ThreadStart(delegate
            {
                int[] offsets = { 250, 1000, 3000, 8000, 10000, 12000, 15000, 20000, 24000, 30000 };
                int previous = 0;
                foreach (int offset in offsets)
                {
                    int delay = offset - previous;
                    if (delay > 0) Thread.Sleep(delay);
                    previous = offset;
                    if (currentGeneration != generation) return;
                    LogLiveTargets("RESUME+" + FormatOffset(offset), currentGeneration);
                }
            }));
            thread.IsBackground = true;
            thread.Name = "Mugen HwndTarget State Timeline";
            thread.Start();
        }

        private static string FormatOffset(int milliseconds)
        {
            if (milliseconds < 1000) return milliseconds.ToString(CultureInfo.InvariantCulture) + "ms";
            if (milliseconds % 1000 == 0) return (milliseconds / 1000).ToString(CultureInfo.InvariantCulture) + "s";
            return (milliseconds / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + "s";
        }

        private static void LogCachedTargets(string label, int currentGeneration)
        {
            List<TargetRecord> snapshot;
            lock (Sync) snapshot = new List<TargetRecord>(Targets);
            if (snapshot.Count == 0)
            {
                SafeLog("[HwndTargetProbe] SNAPSHOT label=" + label + " generation=" + currentGeneration +
                    " profile=" + GetProfileId() + " mode=" + GetDiagnosticMode() + " targets=0");
                return;
            }

            DateTime now = DateTime.UtcNow;
            foreach (TargetRecord record in snapshot)
            {
                long ageMs = (long)Math.Max(0, (now - record.CachedUtc).TotalMilliseconds);
                SafeLog("[HwndTargetProbe] TARGET label=" + label + " generation=" + currentGeneration +
                    " profile=" + GetProfileId() + " mode=" + GetDiagnosticMode() +
                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") + " windowType=" + record.WindowType +
                    " cacheAgeMs=" + ageMs + " " + record.CachedWindowState + " " + record.CachedTargetState +
                    " mediaContextId=" + ObjectId(record.MediaContext) + " " + record.CachedMediaContextState);
            }
        }

        private static void LogLiveTargets(string label, int currentGeneration)
        {
            List<TargetRecord> snapshot;
            lock (Sync) snapshot = new List<TargetRecord>(Targets);
            if (snapshot.Count == 0)
            {
                SafeLog("[HwndTargetProbe] SNAPSHOT label=" + label + " generation=" + currentGeneration +
                    " profile=" + GetProfileId() + " mode=" + GetDiagnosticMode() + " targets=0");
                return;
            }

            foreach (TargetRecord record in snapshot)
            {
                SafeLog("[HwndTargetProbe] TARGET label=" + label + " generation=" + currentGeneration +
                    " profile=" + GetProfileId() + " mode=" + GetDiagnosticMode() +
                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") + " windowType=" + record.WindowType +
                    " targetId=" + RuntimeHelpers.GetHashCode(record.Target) + " " + ReadState(record.Target, TargetFields) +
                    " mediaContextId=" + ObjectId(record.MediaContext) + " " + ReadMediaContextState(record.MediaContext));
            }
        }

        private static string ReadState(object instance, string[] fields)
        {
            if (instance == null) return "target=<null>";
            var parts = new List<string>();
            foreach (string fieldName in fields) parts.Add(fieldName + "=" + ReadField(instance, fieldName));
            return string.Join(" ", parts.ToArray());
        }

        private static string ReadMediaContextState(object mediaContext)
        {
            if (mediaContext == null) return "mediaContext=<unavailable>";
            var parts = new List<string>();
            foreach (string fieldName in MediaContextFields)
            {
                FieldInfo field = FindField(mediaContext.GetType(), fieldName);
                if (field == null)
                {
                    parts.Add("mc." + fieldName + "=<missing>");
                    continue;
                }

                object value = null;
                try { value = field.GetValue(mediaContext); }
                catch (Exception ex)
                {
                    parts.Add("mc." + fieldName + "=<error:" + ex.GetType().Name + ">");
                    continue;
                }

                parts.Add("mc." + fieldName + "=" + FormatValue(value));

                DispatcherOperation op = value as DispatcherOperation;
                if (op != null)
                {
                    try
                    {
                        parts.Add("mc." + fieldName + ".status=" + op.Status);
                        parts.Add("mc." + fieldName + ".priority=" + op.Priority);
                    }
                    catch { }
                }

                DispatcherTimer timer = value as DispatcherTimer;
                if (timer != null)
                {
                    try
                    {
                        parts.Add("mc." + fieldName + ".enabled=" + timer.IsEnabled);
                        parts.Add("mc." + fieldName + ".interval=" + timer.Interval);
                        parts.Add("mc." + fieldName + ".tag=" + FormatValue(timer.Tag));
                    }
                    catch { }
                }
            }
            return string.Join(" ", parts.ToArray());
        }

        private static string ReadField(object instance, string fieldName)
        {
            if (instance == null) return "<null>";
            try
            {
                FieldInfo field = FindField(instance.GetType(), fieldName);
                if (field == null) return "<missing>";
                return FormatValue(field.GetValue(instance));
            }
            catch (Exception ex) { return "<error:" + ex.GetType().Name + ">"; }
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, flags);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "<null>";
            if (value is string) return ((string)value).Replace("\r", " ").Replace("\n", " ");
            if (value is DateTime) return ((DateTime)value).ToString("O", CultureInfo.InvariantCulture);
            if (value is TimeSpan) return ((TimeSpan)value).ToString();
            if (value is IntPtr) return "0x" + ((IntPtr)value).ToInt64().ToString("X");
            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                try { return Convert.ToString(value, CultureInfo.InvariantCulture); } catch { }
            }
            return "<" + type.FullName + "#" + RuntimeHelpers.GetHashCode(value) + ">";
        }

        private static string ObjectId(object value)
        {
            return value == null ? "<null>" : RuntimeHelpers.GetHashCode(value).ToString(CultureInfo.InvariantCulture);
        }

        private static string SafeRenderMode(HwndTarget target)
        {
            try { return target.RenderMode.ToString(); } catch { return "<unavailable>"; }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                SafeLog("[HwndTargetProbe] DOMAIN_UNHANDLED generation=" + generation + " profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode() + " terminating=" + e.IsTerminating +
                    " exception=" + (e.ExceptionObject == null ? "<null>" : e.ExceptionObject.ToString()));
                LogLiveTargets("DOMAIN_UNHANDLED", generation);
            }
            catch { }
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            try
            {
                SafeLog("[HwndTargetProbe] PROCESS_EXIT generation=" + generation + " profile=" + GetProfileId() +
                    " mode=" + GetDiagnosticMode());
                LogLiveTargets("PROCESS_EXIT", generation);
            }
            catch { }
        }

        private static bool IsProfileProcess()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase) ||
                    args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetProfileId()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase)) return i + 1 < args.Length ? args[i + 1] : "<missing>";
                if (args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase)) return args[i].Substring("--profile=".Length);
            }
            return "<none>";
        }

        private static string GetDiagnosticMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--resume-hide-control", StringComparison.OrdinalIgnoreCase)) return "target0";
                if (string.Equals(args[i], "--resume-diag", StringComparison.OrdinalIgnoreCase)) return i + 1 < args.Length ? args[i + 1] : "normal";
                if (args[i].StartsWith("--resume-diag=", StringComparison.OrdinalIgnoreCase)) return args[i].Substring("--resume-diag=".Length);
            }
            return "normal";
        }

        private static void SafeLog(string message)
        {
            try { App.Log(message); } catch { }
        }
    }

    public partial class App
    {
        private static readonly bool ResumeHwndTargetStateProbeBootstrap = ResumeHwndTargetStateProbe.Start();
    }
}
