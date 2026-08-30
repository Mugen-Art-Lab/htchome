using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HTCHome
{
    // Diagnostic step after the pre/post SoftwareOnly experiments. Both proved
    // that changing RenderMode on the existing HwndTarget does not revive it.
    // This build leaves the old target untouched, logs WPF's private wake flags,
    // then creates one fresh duplicate Widget/HWND in the SAME process after
    // resume settles. The old window is deliberately left alive for this test.
    // If the fresh HWND animates while the old one remains frozen, the poisoned
    // state is local to the old layered HwndTarget/composition channel rather
    // than the process, Dispatcher, widget timers or available memory.
    internal static class ResumeRenderRecovery
    {
        private static readonly object Sync = new object();
        private static bool started;
        private static int suspendGeneration;
        private static int resumeGeneration;
        private static int freshProbeGeneration;

        private const uint GW_HWNDPREV = 3;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        public static bool Start()
        {
            if (!IsProfileProcess())
                return true;

            lock (Sync)
            {
                if (started)
                    return true;
                started = true;
            }

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SystemEvents.DisplaySettingsChanging += SystemEvents_DisplaySettingsChanging;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            RenderCapability.TierChanged += RenderCapability_TierChanged;

            SafeLog("[ResumeProbe] START fresh-HWND experiment tier=" + CurrentTier());
            return true;
        }

        private static bool IsProfileProcess()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                int generation;
                lock (Sync)
                {
                    suspendGeneration++;
                    generation = suspendGeneration;
                }

                RunOnUiSync(delegate
                {
                    SafeLog("[ResumeProbe] SUSPEND generation=" + generation + " tier=" + CurrentTier());
                    LogTargets("suspend");
                });
                return;
            }

            if (e.Mode != PowerModes.Resume)
                return;

            int resume;
            lock (Sync)
            {
                resumeGeneration++;
                resume = resumeGeneration;
            }

            RunOnUi(delegate
            {
                SafeLog("[ResumeProbe] RESUME generation=" + resume + " tier=" + CurrentTier());
                LogTargets("resume");
                ScheduleInspect(resume, 250, "resume+250ms");
                ScheduleInspect(resume, 3000, "resume+3s");
                ScheduleInspect(resume, 8000, "resume+8s");

                // Create a fresh layered WPF HWND only after the normal display
                // reconstruction window has had time to settle. Do not close the
                // old window in this experiment: closing the poisoned target is
                // itself known to expose DUCE OutOfMemoryException.
                ScheduleFreshProbe(resume, 12000);
                ScheduleInspect(resume, 20000, "resume+20s");
                ScheduleInspect(resume, 30000, "resume+30s");
            });
        }

        private static void SystemEvents_DisplaySettingsChanging(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                SafeLog("[ResumeProbe] DISPLAY changing tier=" + CurrentTier());
                LogTargets("display-changing");
            });
        }

        private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                SafeLog("[ResumeProbe] DISPLAY changed tier=" + CurrentTier());
                LogTargets("display-changed");
            });
        }

        private static void RenderCapability_TierChanged(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                SafeLog("[ResumeProbe] TIER_CHANGED tier=" + CurrentTier());
                LogTargets("tier-changed");
            });
        }

        private static void ScheduleInspect(int generation, int delayMs, string reason)
        {
            RunOnUi(delegate
            {
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(delayMs);
                timer.Tick += delegate(object sender, EventArgs e)
                {
                    timer.Stop();
                    lock (Sync)
                    {
                        if (generation != resumeGeneration)
                            return;
                    }

                    SafeLog("[ResumeProbe] VERIFY reason=" + reason + " tier=" + CurrentTier());
                    LogTargets(reason);
                };
                timer.Start();
            });
        }

        private static void ScheduleFreshProbe(int generation, int delayMs)
        {
            RunOnUi(delegate
            {
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(delayMs);
                timer.Tick += delegate(object sender, EventArgs e)
                {
                    timer.Stop();

                    lock (Sync)
                    {
                        if (generation != resumeGeneration || freshProbeGeneration == generation)
                            return;
                        freshProbeGeneration = generation;
                    }

                    CreateFreshWidgetProbe(generation);
                };
                timer.Start();
            });
        }

        private static void CreateFreshWidgetProbe(int generation)
        {
            try
            {
                List<Widget> oldWidgets = GetTrackedVisibleWidgets();
                if (oldWidgets.Count == 0)
                {
                    SafeLog("[ResumeProbe] FRESH_PROBE skipped: no tracked visible Widget");
                    return;
                }

                // A Mugen profile normally hosts one Weather/Clock Widget. If a
                // profile has more than one widget, probe only the first tracked
                // visible one so this diagnostic build cannot multiply windows.
                Widget oldWidget = oldWidgets[0];
                IntPtr oldHwnd = new WindowInteropHelper(oldWidget).Handle;

                SafeLog("[ResumeProbe] FRESH_PROBE_CREATE generation=" + generation +
                    " oldHwnd=0x" + oldHwnd.ToInt64().ToString("X") +
                    " path=" + oldWidget.path +
                    " left=" + oldWidget.Left +
                    " top=" + oldWidget.Top +
                    " tier=" + CurrentTier());
                LogTarget("old-before-fresh", oldWidget);

                Widget fresh = new Widget();
                fresh.ShowActivated = false;
                fresh.Initalize(oldWidget.path);

                if (fresh.HasErrors)
                {
                    SafeLog("[ResumeProbe] FRESH_PROBE failed: fresh Widget initialization reported errors");
                    return;
                }

                fresh.Load();

                // The plugin settings normally put the duplicate at the same saved
                // coordinates. Force exact current coordinates after Show as a test
                // safeguard, without changing the old window.
                fresh.Left = oldWidget.Left;
                fresh.Top = oldWidget.Top;
                fresh.Topmost = oldWidget.Topmost;

                IntPtr freshHwnd = new WindowInteropHelper(fresh).Handle;

                // Preserve the user's current desktop stack instead of using
                // Activate()/Topmost. Put the fresh window directly above the old
                // window but below whichever window was already above it.
                IntPtr previous = GetWindow(oldHwnd, GW_HWNDPREV);
                bool zResult = SetWindowPos(
                    freshHwnd,
                    previous,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);

                SafeLog("[ResumeProbe] FRESH_PROBE_SHOWN generation=" + generation +
                    " oldHwnd=0x" + oldHwnd.ToInt64().ToString("X") +
                    " freshHwnd=0x" + freshHwnd.ToInt64().ToString("X") +
                    " previousHwnd=0x" + previous.ToInt64().ToString("X") +
                    " zResult=" + zResult +
                    " tier=" + CurrentTier());
                LogTarget("old-after-fresh", oldWidget);
                LogTarget("fresh-after-show", fresh);

                // Deliberately do NOT add the duplicate to App.widgets and do NOT
                // close the poisoned old window. This is a one-run diagnostic
                // probe. Application shutdown will still close all WPF windows.
            }
            catch (OutOfMemoryException ex)
            {
                SafeLog("[ResumeProbe] FRESH_PROBE_OOM: " + ex);
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeProbe] FRESH_PROBE_FAILED: " + ex);
            }
        }

        private static List<Widget> GetTrackedVisibleWidgets()
        {
            List<Widget> result = new List<Widget>();
            if (App.widgets == null)
                return result;

            foreach (Widget widget in App.widgets)
            {
                if (widget != null && widget.IsLoaded && widget.IsVisible)
                    result.Add(widget);
            }
            return result;
        }

        private static void LogTargets(string reason)
        {
            Application app = Application.Current;
            if (app == null)
                return;

            int index = 0;
            foreach (Window window in app.Windows)
            {
                Widget widget = window as Widget;
                if (widget == null || !widget.IsLoaded || !widget.IsVisible)
                    continue;

                LogTarget(reason + "#" + index, widget);
                index++;
            }
        }

        private static void LogTarget(string reason, Widget window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                HwndSource source = HwndSource.FromHwnd(hwnd);
                HwndTarget target = source == null ? null : source.CompositionTarget;

                if (target == null)
                {
                    SafeLog("[ResumeProbe] TARGET reason=" + reason +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                        " target=<null> tier=" + CurrentTier());
                    return;
                }

                SafeLog("[ResumeProbe] TARGET reason=" + reason +
                    " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                    " tier=" + CurrentTier() +
                    " renderMode=" + target.RenderMode +
                    " internals={" + ReadTargetInternals(target) + "}");
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeProbe] TARGET reason=" + reason + " failed: " +
                    ex.GetType().FullName + ": " + ex.Message);
            }
        }

        private static string ReadTargetInternals(HwndTarget target)
        {
            string[] names = new string[]
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

            List<string> values = new List<string>();
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            foreach (string name in names)
            {
                try
                {
                    FieldInfo field = type.GetField(name, flags);
                    if (field == null)
                    {
                        values.Add(name + "=<missing>");
                        continue;
                    }

                    object value = field.GetValue(target);
                    values.Add(name + "=" + (value == null ? "null" : value.ToString()));
                }
                catch (Exception ex)
                {
                    values.Add(name + "=<" + ex.GetType().Name + ">");
                }
            }

            return string.Join(",", values.ToArray());
        }

        private static int CurrentTier()
        {
            return RenderCapability.Tier >> 16;
        }

        private static void RunOnUi(Action action)
        {
            try
            {
                Application app = Application.Current;
                if (app == null || app.Dispatcher == null)
                    return;

                if (app.Dispatcher.CheckAccess())
                    action();
                else
                    app.Dispatcher.BeginInvoke(action);
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeProbe] dispatcher failed: " + ex.GetType().FullName + ": " + ex.Message);
            }
        }

        private static void RunOnUiSync(Action action)
        {
            try
            {
                Application app = Application.Current;
                if (app == null || app.Dispatcher == null)
                {
                    SafeLog("[ResumeProbe] synchronous dispatcher unavailable");
                    return;
                }

                if (app.Dispatcher.CheckAccess())
                    action();
                else
                    app.Dispatcher.Invoke(action, DispatcherPriority.Send);
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeProbe] synchronous dispatcher failed: " +
                    ex.GetType().FullName + ": " + ex.Message);
            }
        }

        private static void SafeLog(string message)
        {
            try
            {
                App.Log(message);
            }
            catch { }
        }
    }

    public partial class App
    {
        private static readonly bool ResumeRenderRecoveryBootstrap = ResumeRenderRecovery.Start();
    }
}
