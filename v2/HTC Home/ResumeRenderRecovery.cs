using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HTCHome
{
    // Isolated post-hibernate recovery experiment for Mugen profile processes.
    // The passive ResumeDiagnostics remains untouched. After resume/display
    // changes settle, if WPF is still stuck at Tier 0, this switches the existing
    // HwndTarget to SoftwareOnly and invalidates the same Window. It does not
    // restart, hide, move, recreate or change the Z-order of the widget window.
    internal static class ResumeRenderRecovery
    {
        private static readonly object Sync = new object();
        private static bool started;
        private static int resumeGeneration;
        private static int attemptedGeneration;
        private static DateTime lastResumeUtc = DateTime.MinValue;
        private static DateTime lastDisplayChangeUtc = DateTime.MinValue;

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
                RunOnUi(delegate
                {
                    SafeLog("[ResumeRepair] SUSPEND tier=" + CurrentTier());
                    LogTargets("suspend");
                });
                return;
            }

            if (e.Mode != PowerModes.Resume)
                return;

            int generation;
            lock (Sync)
            {
                DateTime now = DateTime.UtcNow;
                resumeGeneration++;
                generation = resumeGeneration;
                attemptedGeneration = 0;
                lastResumeUtc = now;
                lastDisplayChangeUtc = now;
            }

            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] RESUME generation=" + generation + " tier=" + CurrentTier());
                LogTargets("resume");
                ScheduleCheck(generation, 3000, "resume+3s");
                ScheduleCheck(generation, 9000, "resume+9s");
                ScheduleCheck(generation, 15000, "resume+15s");
            });
        }

        private static void SystemEvents_DisplaySettingsChanging(object sender, EventArgs e)
        {
            lock (Sync)
                lastDisplayChangeUtc = DateTime.UtcNow;

            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] DISPLAY changing tier=" + CurrentTier());
                LogTargets("display-changing");
            });
        }

        private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            int generation;
            lock (Sync)
            {
                lastDisplayChangeUtc = DateTime.UtcNow;
                generation = resumeGeneration;
            }

            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] DISPLAY changed generation=" + generation + " tier=" + CurrentTier());
                LogTargets("display-changed");
                if (generation > 0)
                    ScheduleCheck(generation, 3500, "display+3.5s");
            });
        }

        private static void RenderCapability_TierChanged(object sender, EventArgs e)
        {
            int generation;
            lock (Sync)
                generation = resumeGeneration;

            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] TIER_CHANGED generation=" + generation + " tier=" + CurrentTier());
                LogTargets("tier-changed");
                if (generation > 0)
                    ScheduleCheck(generation, 1500, "tier-change+1.5s");
            });
        }

        private static void ScheduleCheck(int generation, int delayMs, string reason)
        {
            RunOnUi(delegate
            {
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(delayMs);
                timer.Tick += delegate(object sender, EventArgs e)
                {
                    timer.Stop();
                    TryRecovery(generation, reason);
                };
                timer.Start();
            });
        }

        private static void TryRecovery(int generation, string reason)
        {
            try
            {
                DateTime resumeUtc;
                DateTime displayUtc;
                int attempted;

                lock (Sync)
                {
                    if (generation != resumeGeneration)
                        return;
                    resumeUtc = lastResumeUtc;
                    displayUtc = lastDisplayChangeUtc;
                    attempted = attemptedGeneration;
                }

                double sinceResume = (DateTime.UtcNow - resumeUtc).TotalSeconds;
                double sinceDisplay = (DateTime.UtcNow - displayUtc).TotalSeconds;
                int tier = CurrentTier();

                SafeLog("[ResumeRepair] CHECK reason=" + reason +
                    " generation=" + generation +
                    " tier=" + tier +
                    " sinceResume=" + sinceResume.ToString("0.0", CultureInfo.InvariantCulture) +
                    "s sinceDisplay=" + sinceDisplay.ToString("0.0", CultureInfo.InvariantCulture) + "s");

                if (tier > 0)
                {
                    SafeLog("[ResumeRepair] HEALTHY: hardware tier recovered; no intervention");
                    return;
                }

                if (sinceResume < 8.0 || sinceDisplay < 3.0)
                {
                    ScheduleCheck(generation, 3000, "settle-retry");
                    return;
                }

                if (attempted == generation)
                    return;

                lock (Sync)
                    attemptedGeneration = generation;

                SafeLog("[ResumeRepair] ATTEMPT generation=" + generation +
                    ": Tier remained 0 after display settle; switching existing HwndTarget(s) to SoftwareOnly");
                LogTargets("pre-rebind");

                foreach (Window window in GetWidgetWindows())
                {
                    IntPtr hwnd = new WindowInteropHelper(window).Handle;
                    HwndSource source = HwndSource.FromHwnd(hwnd);
                    HwndTarget target = source == null ? null : source.CompositionTarget;

                    if (target == null)
                    {
                        SafeLog("[ResumeRepair] no HwndTarget for hwnd=0x" + hwnd.ToInt64().ToString("X"));
                        continue;
                    }

                    try
                    {
                        RenderMode before = target.RenderMode;
                        target.RenderMode = RenderMode.SoftwareOnly;
                        window.InvalidateVisual();

                        UIElement content = window.Content as UIElement;
                        if (content != null)
                            content.InvalidateVisual();

                        SafeLog("[ResumeRepair] REBIND_OK hwnd=0x" + hwnd.ToInt64().ToString("X") +
                            " renderMode=" + before + "->" + target.RenderMode +
                            " tier=" + CurrentTier());
                    }
                    catch (OutOfMemoryException ex)
                    {
                        SafeLog("[ResumeRepair] REBIND_OOM hwnd=0x" + hwnd.ToInt64().ToString("X") + ": " + ex);
                    }
                    catch (Exception ex)
                    {
                        SafeLog("[ResumeRepair] REBIND_FAILED hwnd=0x" + hwnd.ToInt64().ToString("X") + ": " + ex);
                    }
                }

                ScheduleVerify(generation, 1000, "rebind+1s");
                ScheduleVerify(generation, 5000, "rebind+5s");
                ScheduleVerify(generation, 15000, "rebind+15s");
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeRepair] CHECK failed: " + ex);
            }
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

    // WPF instantiates App before widget windows are created. This partial-class
    // field subscribes the isolated recovery probe without modifying App.xaml.cs.
    public partial class App
    {
        private static readonly bool ResumeRenderRecoveryBootstrap = ResumeRenderRecovery.Start();
    }
}
