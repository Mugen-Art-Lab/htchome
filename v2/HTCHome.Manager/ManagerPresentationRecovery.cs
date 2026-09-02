using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace HTCHome.Manager
{
    // Manager stale-presentation recovery.
    //
    // A reproduced white-client failure showed that the Manager Dispatcher,
    // bindings, HwndTarget and fresh same-PID WPF MediaSystem could all remain
    // healthy while the existing main HwndTarget stopped presenting new pixels.
    // Manually resizing the Window immediately restored the already-updated UI.
    //
    // Keep the normal path lightweight: after explicit Manager presentation-risk
    // operations (Show from tray and process-status mutations), ask Windows for one
    // native WM_PAINT. If that paint does not move MediaContext._lastCommitTime,
    // send a same-size WM_SIZE so WPF runs HwndTarget.OnResize without changing the
    // actual Window geometry. This reproduces the proven manual wake path without
    // the visible +1/-1 pixel resize workaround.
    internal static class ManagerPresentationRecovery
    {
        private sealed class RecoveryRecord
        {
            public int Generation;
            public Window Window;
            public string Reason;
            public IntPtr Hwnd;
            public HwndTarget Target;
            public object MediaContext;
            public int TargetId;
            public object CommitAtArm;
            public object CommitBeforePaint;
            public object CommitBeforeSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint WM_SIZE = 0x0005;
        private const int SIZE_RESTORED = 0;
        private const int PaintDelayMs = 250;
        private const int PaintVerifyDelayMs = 300;
        private const int SizeVerifyDelayMs = 350;

        private static readonly object Sync = new object();
        private static int generation;
        private static DispatcherTimer paintTimer;
        private static DispatcherTimer paintVerifyTimer;
        private static DispatcherTimer sizeVerifyTimer;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        public static void Arm(Window window, string reason)
        {
            if (window == null) return;

            if (!window.Dispatcher.CheckAccess())
            {
                try
                {
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Send,
                        new Action(delegate { Arm(window, reason); }));
                }
                catch { }
                return;
            }

            StopTimers();

            int current = ++generation;
            RecoveryRecord record = Capture(window, reason, current);
            if (record == null)
            {
                Write("ARM_SKIPPED generation=" + current + " reason=" + SafeReason(reason) +
                    " visible=" + window.IsVisible + " state=" + window.WindowState);
                return;
            }

            Write("ARM generation=" + current + " reason=" + SafeReason(reason) + " " + Describe(record));

            paintTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher);
            paintTimer.Interval = TimeSpan.FromMilliseconds(PaintDelayMs);
            paintTimer.Tick += delegate
            {
                paintTimer.Stop();
                PaintStage(record);
            };
            paintTimer.Start();
        }

        private static RecoveryRecord Capture(Window window, string reason, int current)
        {
            try
            {
                if (!window.IsVisible || window.WindowState == WindowState.Minimized) return null;

                HwndSource source = PresentationSource.FromVisual(window) as HwndSource;
                if (source == null || source.Handle == IntPtr.Zero || source.CompositionTarget == null) return null;

                HwndTarget target = source.CompositionTarget;
                object mediaContext = GetExistingMediaContext(target);
                return new RecoveryRecord
                {
                    Generation = current,
                    Window = window,
                    Reason = reason ?? string.Empty,
                    Hwnd = source.Handle,
                    Target = target,
                    MediaContext = mediaContext,
                    TargetId = RuntimeHelpers.GetHashCode(target),
                    CommitAtArm = GetFieldValue(mediaContext, "_lastCommitTime")
                };
            }
            catch (Exception ex)
            {
                Write("CAPTURE_FAILED generation=" + current + " reason=" + SafeReason(reason) + " " + ex);
                return null;
            }
        }

        private static void PaintStage(RecoveryRecord record)
        {
            if (!Validate(record, "PAINT_STAGE")) return;

            try
            {
                record.CommitBeforePaint = GetFieldValue(record.MediaContext, "_lastCommitTime");
                bool invalidated = InvalidateRect(record.Hwnd, IntPtr.Zero, true);
                bool updated = UpdateWindow(record.Hwnd);

                Write("WM_PAINT_KICK generation=" + record.Generation +
                    " reason=" + SafeReason(record.Reason) +
                    " invalidateRect=" + invalidated +
                    " updateWindow=" + updated +
                    " commitBefore=" + FormatValue(record.CommitBeforePaint) + " " + Describe(record));

                paintVerifyTimer = new DispatcherTimer(DispatcherPriority.Background, record.Window.Dispatcher);
                paintVerifyTimer.Interval = TimeSpan.FromMilliseconds(PaintVerifyDelayMs);
                paintVerifyTimer.Tick += delegate
                {
                    paintVerifyTimer.Stop();
                    VerifyPaint(record);
                };
                paintVerifyTimer.Start();
            }
            catch (Exception ex)
            {
                Write("WM_PAINT_FAILED generation=" + record.Generation + " reason=" + SafeReason(record.Reason) + " " + ex);
            }
        }

        private static void VerifyPaint(RecoveryRecord record)
        {
            if (!Validate(record, "PAINT_VERIFY")) return;

            try
            {
                object commitNow = GetFieldValue(record.MediaContext, "_lastCommitTime");
                bool commitAdvanced = !ValuesEqual(record.CommitBeforePaint, commitNow);

                Write("WM_PAINT_VERIFY generation=" + record.Generation +
                    " reason=" + SafeReason(record.Reason) +
                    " commitAdvanced=" + commitAdvanced +
                    " commitBefore=" + FormatValue(record.CommitBeforePaint) +
                    " commitNow=" + FormatValue(commitNow) + " " + Describe(record));

                if (commitAdvanced)
                {
                    Write("RECOVERY_COMPLETE generation=" + record.Generation +
                        " reason=" + SafeReason(record.Reason) + " stage=wm-paint");
                    return;
                }

                SameSizeResizeStage(record);
            }
            catch (Exception ex)
            {
                Write("WM_PAINT_VERIFY_FAILED generation=" + record.Generation + " reason=" + SafeReason(record.Reason) + " " + ex);
            }
        }

        private static void SameSizeResizeStage(RecoveryRecord record)
        {
            if (!Validate(record, "WM_SIZE_STAGE")) return;

            try
            {
                RECT rect;
                if (!GetClientRect(record.Hwnd, out rect))
                {
                    Write("WM_SIZE_SKIPPED generation=" + record.Generation +
                        " reason=" + SafeReason(record.Reason) + " getClientRect=False");
                    return;
                }

                int width = Math.Max(0, rect.Right - rect.Left);
                int height = Math.Max(0, rect.Bottom - rect.Top);
                if (width > 0xFFFF || height > 0xFFFF)
                {
                    Write("WM_SIZE_SKIPPED generation=" + record.Generation +
                        " reason=" + SafeReason(record.Reason) +
                        " size=" + width + "x" + height + " outOfRange=True");
                    return;
                }

                record.CommitBeforeSize = GetFieldValue(record.MediaContext, "_lastCommitTime");
                int packed = (width & 0xFFFF) | ((height & 0xFFFF) << 16);
                SendMessage(record.Hwnd, WM_SIZE, new IntPtr(SIZE_RESTORED), new IntPtr(packed));

                Write("WM_SIZE_KICK generation=" + record.Generation +
                    " reason=" + SafeReason(record.Reason) +
                    " size=" + width + "x" + height +
                    " commitBefore=" + FormatValue(record.CommitBeforeSize) + " " + Describe(record));

                sizeVerifyTimer = new DispatcherTimer(DispatcherPriority.Background, record.Window.Dispatcher);
                sizeVerifyTimer.Interval = TimeSpan.FromMilliseconds(SizeVerifyDelayMs);
                sizeVerifyTimer.Tick += delegate
                {
                    sizeVerifyTimer.Stop();
                    VerifySize(record);
                };
                sizeVerifyTimer.Start();
            }
            catch (Exception ex)
            {
                Write("WM_SIZE_FAILED generation=" + record.Generation + " reason=" + SafeReason(record.Reason) + " " + ex);
            }
        }

        private static void VerifySize(RecoveryRecord record)
        {
            if (!Validate(record, "WM_SIZE_VERIFY")) return;

            try
            {
                object commitNow = GetFieldValue(record.MediaContext, "_lastCommitTime");
                bool commitAdvanced = !ValuesEqual(record.CommitBeforeSize, commitNow);
                Write("WM_SIZE_VERIFY generation=" + record.Generation +
                    " reason=" + SafeReason(record.Reason) +
                    " commitAdvanced=" + commitAdvanced +
                    " commitBefore=" + FormatValue(record.CommitBeforeSize) +
                    " commitNow=" + FormatValue(commitNow) + " " + Describe(record));

                Write("RECOVERY_COMPLETE generation=" + record.Generation +
                    " reason=" + SafeReason(record.Reason) +
                    " stage=wm-size commitAdvanced=" + commitAdvanced);
            }
            catch (Exception ex)
            {
                Write("WM_SIZE_VERIFY_FAILED generation=" + record.Generation + " reason=" + SafeReason(record.Reason) + " " + ex);
            }
        }

        private static bool Validate(RecoveryRecord record, string stage)
        {
            if (record == null || record.Generation != generation) return false;

            try
            {
                if (!record.Window.IsVisible || record.Window.WindowState == WindowState.Minimized)
                {
                    Write(stage + "_SKIPPED generation=" + record.Generation +
                        " reason=" + SafeReason(record.Reason) +
                        " visible=" + record.Window.IsVisible + " state=" + record.Window.WindowState);
                    return false;
                }

                HwndSource source = PresentationSource.FromVisual(record.Window) as HwndSource;
                if (source == null || source.Handle != record.Hwnd || source.CompositionTarget == null)
                {
                    Write(stage + "_SKIPPED generation=" + record.Generation +
                        " reason=" + SafeReason(record.Reason) + " sameHwnd=False");
                    return false;
                }

                if (RuntimeHelpers.GetHashCode(source.CompositionTarget) != record.TargetId)
                {
                    Write(stage + "_SKIPPED generation=" + record.Generation +
                        " reason=" + SafeReason(record.Reason) + " sameTarget=False");
                    return false;
                }

                if (!ReadBoolField(record.Target, "_isRenderTargetEnabled", true))
                {
                    Write(stage + "_SKIPPED generation=" + record.Generation +
                        " reason=" + SafeReason(record.Reason) + " targetEnabled=False");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Write(stage + "_VALIDATE_FAILED generation=" + record.Generation +
                    " reason=" + SafeReason(record.Reason) + " " + ex);
                return false;
            }
        }

        private static string Describe(RecoveryRecord record)
        {
            return "hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                " targetId=" + record.TargetId +
                " target.enabled=" + ReadField(record.Target, "_isRenderTargetEnabled") +
                " target.suspended=" + ReadField(record.Target, "_isSuspended") +
                " target.needsRePresent=" + ReadField(record.Target, "_needsRePresentOnWake") +
                " mcId=" + (record.MediaContext == null ? 0 : RuntimeHelpers.GetHashCode(record.MediaContext)) +
                " mc.interlock=" + ReadField(record.MediaContext, "_interlockState") +
                " mc.currentRenderOp=" + FormatDispatcherOperation(GetFieldValue(record.MediaContext, "_currentRenderOp")) +
                " mc.lastCommit=" + FormatValue(GetFieldValue(record.MediaContext, "_lastCommitTime")) +
                " mc.lastPresentation=" + FormatValue(GetFieldValue(record.MediaContext, "_lastPresentationResults")) +
                " mc.needCommit=" + ReadField(record.MediaContext, "_needToCommitChannel") +
                " mc.commitPending=" + ReadField(record.MediaContext, "_commitPendingAfterRender");
        }

        private static void StopTimers()
        {
            if (paintTimer != null) { try { paintTimer.Stop(); } catch { } paintTimer = null; }
            if (paintVerifyTimer != null) { try { paintVerifyTimer.Stop(); } catch { } paintVerifyTimer = null; }
            if (sizeVerifyTimer != null) { try { sizeVerifyTimer.Stop(); } catch { } sizeVerifyTimer = null; }
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
            if (type.IsEnum || type.IsPrimitive || value is decimal || value is string)
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

        private static string SafeReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? "<none>" : reason.Replace("\r", " ").Replace("\n", " ");
        }

        private static void Write(string text)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "manager-presentation-recovery.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                lock (Sync)
                {
                    File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) + " " + text + Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }
}
