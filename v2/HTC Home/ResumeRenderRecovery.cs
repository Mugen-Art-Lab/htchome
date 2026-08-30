using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HTCHome
{
    // Pre-suspend WPF recovery experiment for Mugen profile processes.
    // The previous experiment proved that changing RenderMode after resume is
    // too late: the existing render channel is already unusable by then. This
    // version moves the same HwndTarget to SoftwareOnly while Tier is still 2,
    // before Windows suspends the GPU. It does not restart, hide, move or
    // recreate the widget window.
    internal static class ResumeRenderRecovery
    {
        private static readonly object Sync = new object();
        private static bool started;
        private static int suspendGeneration;
        private static int resumeGeneration;

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
            SafeLog("[ResumeRepair] PRE_SUSPEND_GUARD started tier=" + CurrentTier());
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

                // This is intentionally synchronous. The useful window is between
                // PowerModes.Suspend and WPF dropping Tier 2 to Tier 0, so a queued
                // BeginInvoke could execute only after the GPU is already gone.
                RunOnUiSync(delegate
                {
                    SafeLog("[ResumeRepair] SUSPEND_PREPARE generation=" + generation +
                        " tier=" + CurrentTier());
                    LogTargets("pre-suspend-before");
                    ArmSoftwareTargets(generation);
                    LogTargets("pre-suspend-after");
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
                SafeLog("[ResumeRepair] RESUME generation=" + resume + " tier=" + CurrentTier());
                LogTargets("resume");
                ScheduleVerify(resume, 250, "resume+250ms");
                ScheduleVerify(resume, 3000, "resume+3s");
                ScheduleVerify(resume, 10000, "resume+10s");
                ScheduleVerify(resume, 30000, "resume+30s");
            });
        }

        private static void ArmSoftwareTargets(int generation)
        {
            foreach (Window window in GetWidgetWindows())
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                HwndSource source = HwndSource.FromHwnd(hwnd);
                HwndTarget target = source == null ? null : source.CompositionTarget;

                if (target == null)
                {
                    SafeLog("[ResumeRepair] PRE_SUSPEND_NO_TARGET generation=" + generation +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X"));
                    continue;
                }

                try
                {
                    RenderMode before = target.RenderMode;
                    target.RenderMode = RenderMode.SoftwareOnly;

                    // Ask for one final render while the channel is still healthy.
                    // This also makes the mode transition observable before sleep.
                    window.InvalidateVisual();
                    UIElement content = window.Content as UIElement;
                    if (content != null)
                        content.InvalidateVisual();

                    SafeLog("[ResumeRepair] PRE_SUSPEND_ARMED generation=" + generation +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                        " renderMode=" + before + "->" + target.RenderMode +
                        " tier=" + CurrentTier());
                }
                catch (OutOfMemoryException ex)
                {
                    SafeLog("[ResumeRepair] PRE_SUSPEND_OOM generation=" + generation +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X") + ": " + ex);
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeRepair] PRE_SUSPEND_FAILED generation=" + generation +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X") + ": " + ex);
                }
            }
        }

        private static void SystemEvents_DisplaySettingsChanging(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] DISPLAY changing tier=" + CurrentTier());
                LogTargets("display-changing");
            });
        }

        private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] DISPLAY changed tier=" + CurrentTier());
                LogTargets("display-changed");
            });
        }

        private static void RenderCapability_TierChanged(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] TIER_CHANGED tier=" + CurrentTier());
                LogTargets("tier-changed");
            });
        }

        private static void ScheduleVerify(int generation, int delayMs, string reason)
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
                    SafeLog("[ResumeRepair] VERIFY reason=" + reason + " tier=" + CurrentTier());
                    LogTargets(reason);
                };
                timer.Start();
            });
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
                SafeLog("[ResumeRepair] dispatcher failed: " + ex.GetType().FullName + ": " + ex.Message);
            }
        }

        private static void RunOnUiSync(Action action)
        {
            try
            {
                Application app = Application.Current;
                if (app == null || app.Dispatcher == null)
                {
                    SafeLog("[ResumeRepair] synchronous dispatcher unavailable");
                    return;
                }

                if (app.Dispatcher.CheckAccess())
                    action();
                else
                    app.Dispatcher.Invoke(action, DispatcherPriority.Send);
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeRepair] synchronous dispatcher failed: " +
                    ex.GetType().FullName + ": " + ex.Message);
            }
        }

        private static List<Window> GetWidgetWindows()
        {
            List<Window> result = new List<Window>();
            Application app = Application.Current;
            if (app == null)
                return result;

            foreach (Window window in app.Windows)
            {
                if (window is Widget && window.IsLoaded && window.IsVisible)
                    result.Add(window);
            }
            return result;
        }

        private static void LogTargets(string reason)
        {
            foreach (Window window in GetWidgetWindows())
            {
                try
                {
                    IntPtr hwnd = new WindowInteropHelper(window).Handle;
                    HwndSource source = HwndSource.FromHwnd(hwnd);
                    HwndTarget target = source == null ? null : source.CompositionTarget;
                    SafeLog("[ResumeRepair] TARGET reason=" + reason +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                        " tier=" + CurrentTier() +
                        " renderMode=" + (target == null ? "<null>" : target.RenderMode.ToString()));
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeRepair] TARGET reason=" + reason + " failed: " +
                        ex.GetType().FullName + ": " + ex.Message);
                }
            }
        }

        private static int CurrentTier()
        {
            return RenderCapability.Tier >> 16;
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
