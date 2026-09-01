using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HTCHome
{
    // Prototype-fix watchdog layered on top of ResumeRenderRecovery's proven
    // target0 path (TargetOff on Suspend -> synchronous TargetOn at Resume).
    //
    // Run #55 demonstrated a second, non-poisoned failure class: an existing
    // HwndTarget can be enabled late yet keep showing an old DWM surface. This
    // watchdog does not paint on the normal path. It checks whether the original
    // MediaContext produced a new commit after Resume; only a stale target gets a
    // native WM_PAINT/DoPaint kick via InvalidateRect + UpdateWindow.
    internal static class ResumePrototypeFixWatchdog
    {
        private sealed class WatchRecord
        {
            public Window Window;
            public IntPtr Hwnd;
            public HwndTarget Target;
            public object MediaContext;
            public object LastCommitAtArm;
            public int TargetId;
        }

        private const int CheckDelayMs = 1500;
        private const int VerifyDelayMs = 700;

        private static readonly object Sync = new object();
        private static bool started;
        private static int generation;
        private static int renderingGeneration;
        private static readonly List<WatchRecord> Records = new List<WatchRecord>();
        private static DispatcherTimer checkTimer;
        private static DispatcherTimer verifyTimer;
        private static EventHandler renderingHandler;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        public static bool Start()
        {
            if (!IsProtectedProfile()) return true;

            lock (Sync)
            {
                if (started) return true;
                started = true;
            }

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SafeLog("[ResumeFix] WATCHDOG_ARMED profile=" + GetProfileId() +
                " checkDelayMs=" + CheckDelayMs +
                " verifyDelayMs=" + VerifyDelayMs);
            return true;
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume) return;

            int current = System.Threading.Interlocked.Increment(ref generation);
            Application app = Application.Current;
            if (app == null)
            {
                SafeLog("[ResumeFix] WATCHDOG_RESUME generation=" + current + " application=<null>");
                return;
            }

            try
            {
                app.Dispatcher.BeginInvoke(DispatcherPriority.Send,
                    new Action(delegate { ArmOnUi(current); }));
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeFix] WATCHDOG_QUEUE_FAILED generation=" + current + " " + ex);
            }
        }

        private static void ArmOnUi(int current)
        {
            if (current != generation) return;

            StopTimersAndRenderingHook();
            Records.Clear();
            renderingGeneration = 0;

            Application app = Application.Current;
            if (app == null) return;

            foreach (Window window in app.Windows)
            {
                if (window == null || !window.IsVisible) continue;

                try
                {
                    IntPtr hwnd = new WindowInteropHelper(window).Handle;
                    HwndSource source = HwndSource.FromHwnd(hwnd);
                    HwndTarget target = source == null ? null : source.CompositionTarget;
                    if (hwnd == IntPtr.Zero || target == null) continue;

                    object mc = GetExistingMediaContext(target);
                    WatchRecord record = new WatchRecord
                    {
                        Window = window,
                        Hwnd = hwnd,
                        Target = target,
                        MediaContext = mc,
                        LastCommitAtArm = GetFieldValue(mc, "_lastCommitTime"),
                        TargetId = RuntimeHelpers.GetHashCode(target)
                    };
                    Records.Add(record);

                    SafeLog("[ResumeFix] WATCHDOG_TARGET_ARM generation=" + current +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                        " targetId=" + record.TargetId +
                        " targetEnabled=" + ReadField(target, "_isRenderTargetEnabled") +
                        " suspended=" + ReadField(target, "_isSuspended") +
                        " lastCommit=" + FormatValue(record.LastCommitAtArm) +
                        " renderOp=" + FormatDispatcherOperation(GetFieldValue(mc, "_currentRenderOp")) +
                        " interlock=" + ReadField(mc, "_interlockState"));
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeFix] WATCHDOG_TARGET_ARM_FAILED generation=" + current + " " + ex);
                }
            }

            renderingHandler = delegate(object sender, EventArgs args)
            {
                if (current == generation) renderingGeneration = current;
            };
            CompositionTarget.Rendering += renderingHandler;

            checkTimer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher);
            checkTimer.Interval = TimeSpan.FromMilliseconds(CheckDelayMs);
            checkTimer.Tick += delegate
            {
                checkTimer.Stop();
                CheckAndKick(current);
            };
            checkTimer.Start();

            SafeLog("[ResumeFix] WATCHDOG_STARTED generation=" + current +
                " targets=" + Records.Count +
                " uiThread=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
        }

        private static void CheckAndKick(int current)
        {
            if (current != generation) return;

            if (renderingHandler != null)
            {
                try { CompositionTarget.Rendering -= renderingHandler; } catch { }
                renderingHandler = null;
            }

            bool kickedAny = false;
            foreach (WatchRecord record in Records)
            {
                try
                {
                    object currentCommit = GetFieldValue(record.MediaContext, "_lastCommitTime");
                    bool commitAdvanced = !ValuesEqual(record.LastCommitAtArm, currentCommit);
                    bool targetEnabled = ReadBoolField(record.Target, "_isRenderTargetEnabled", false);
                    bool sameHwnd = new WindowInteropHelper(record.Window).Handle == record.Hwnd;
                    string renderOp = FormatDispatcherOperation(GetFieldValue(record.MediaContext, "_currentRenderOp"));

                    SafeLog("[ResumeFix] WATCHDOG_CHECK generation=" + current +
                        " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                        " sameHwnd=" + sameHwnd +
                        " sameTarget=" + (RuntimeHelpers.GetHashCode(record.Target) == record.TargetId) +
                        " targetEnabled=" + targetEnabled +
                        " renderingSeen=" + (renderingGeneration == current) +
                        " commitAdvanced=" + commitAdvanced +
                        " lastCommitBefore=" + FormatValue(record.LastCommitAtArm) +
                        " lastCommitNow=" + FormatValue(currentCommit) +
                        " renderOp=" + renderOp +
                        " interlock=" + ReadField(record.MediaContext, "_interlockState"));

                    if (!sameHwnd || !targetEnabled || commitAdvanced) continue;

                    bool invalidated = InvalidateRect(record.Hwnd, IntPtr.Zero, true);
                    bool updated = UpdateWindow(record.Hwnd);
                    kickedAny = true;

                    SafeLog("[ResumeFix] WATCHDOG_WM_PAINT_KICK generation=" + current +
                        " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                        " invalidateRect=" + invalidated +
                        " updateWindow=" + updated +
                        " renderOp=" + FormatDispatcherOperation(GetFieldValue(record.MediaContext, "_currentRenderOp")) +
                        " interlock=" + ReadField(record.MediaContext, "_interlockState"));
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeFix] WATCHDOG_CHECK_FAILED generation=" + current + " " + ex);
                }
            }

            if (!kickedAny)
            {
                SafeLog("[ResumeFix] WATCHDOG_HEALTHY generation=" + current +
                    " targets=" + Records.Count +
                    " renderingSeen=" + (renderingGeneration == current));
                Records.Clear();
                return;
            }

            Application app = Application.Current;
            if (app == null)
            {
                Records.Clear();
                return;
            }

            verifyTimer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher);
            verifyTimer.Interval = TimeSpan.FromMilliseconds(VerifyDelayMs);
            verifyTimer.Tick += delegate
            {
                verifyTimer.Stop();
                VerifyAfterKick(current);
            };
            verifyTimer.Start();
        }

        private static void VerifyAfterKick(int current)
        {
            if (current != generation) return;

            foreach (WatchRecord record in Records)
            {
                try
                {
                    object currentCommit = GetFieldValue(record.MediaContext, "_lastCommitTime");
                    SafeLog("[ResumeFix] WATCHDOG_VERIFY generation=" + current +
                        " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                        " targetEnabled=" + ReadField(record.Target, "_isRenderTargetEnabled") +
                        " commitAdvanced=" + (!ValuesEqual(record.LastCommitAtArm, currentCommit)) +
                        " lastCommitNow=" + FormatValue(currentCommit) +
                        " renderOp=" + FormatDispatcherOperation(GetFieldValue(record.MediaContext, "_currentRenderOp")) +
                        " interlock=" + ReadField(record.MediaContext, "_interlockState"));
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeFix] WATCHDOG_VERIFY_FAILED generation=" + current + " " + ex);
                }
            }

            Records.Clear();
        }

        private static void StopTimersAndRenderingHook()
        {
            if (checkTimer != null)
            {
                try { checkTimer.Stop(); } catch { }
                checkTimer = null;
            }
            if (verifyTimer != null)
            {
                try { verifyTimer.Stop(); } catch { }
                verifyTimer = null;
            }
            if (renderingHandler != null)
            {
                try { CompositionTarget.Rendering -= renderingHandler; } catch { }
                renderingHandler = null;
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
                return from == null ? null : from.Invoke(null, new object[] { target.Dispatcher });
            }
            catch { return null; }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            while (type != null)
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private static object GetFieldValue(object instance, string name)
        {
            if (instance == null) return null;
            try
            {
                FieldInfo field = FindField(instance.GetType(), name);
                return field == null ? null : field.GetValue(instance);
            }
            catch { return null; }
        }

        private static string ReadField(object instance, string name)
        {
            if (instance == null) return "<null>";
            try
            {
                FieldInfo field = FindField(instance.GetType(), name);
                if (field == null) return "<missing>";
                return FormatValue(field.GetValue(instance));
            }
            catch (Exception ex) { return "<error:" + ex.GetType().Name + ">"; }
        }

        private static bool ReadBoolField(object instance, string name, bool fallback)
        {
            if (instance == null) return fallback;
            try
            {
                FieldInfo field = FindField(instance.GetType(), name);
                object value = field == null ? null : field.GetValue(instance);
                return value is bool ? (bool)value : fallback;
            }
            catch { return fallback; }
        }

        private static string FormatDispatcherOperation(object value)
        {
            DispatcherOperation op = value as DispatcherOperation;
            if (op == null) return value == null ? "<null>" : "<" + value.GetType().Name + ">";
            try { return "status=" + op.Status + ",priority=" + op.Priority; }
            catch { return "<DispatcherOperation>"; }
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "<null>";
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

        private static bool ValuesEqual(object left, object right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            try { return left.Equals(right); }
            catch { return false; }
        }

        private static bool IsProtectedProfile()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--resume-hide-control", StringComparison.OrdinalIgnoreCase)) return true;

                string value = null;
                if (string.Equals(args[i], "--resume-diag", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) value = args[i + 1];
                }
                else if (args[i].StartsWith("--resume-diag=", StringComparison.OrdinalIgnoreCase))
                    value = args[i].Substring("--resume-diag=".Length);

                if (string.IsNullOrWhiteSpace(value)) continue;
                return !string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase);
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

        private static void SafeLog(string message)
        {
            try { App.Log(message); } catch { }
        }
    }

    public partial class App
    {
        private static readonly bool ResumePrototypeFixWatchdogBootstrap = ResumePrototypeFixWatchdog.Start();
    }
}
