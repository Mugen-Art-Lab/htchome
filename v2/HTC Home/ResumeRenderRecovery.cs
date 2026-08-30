using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HTCHome
{
    // Process-local diagnostics for the post-hibernate DUCE failure.
    // A fresh Dispatcher/MediaContext cannot recover a poisoned HTC Home PID,
    // while the hidden Mugen Manager survives the same bad system wake. The
    // --resume-hide-control experiment therefore hides one profile's visible
    // WPF windows at Suspend, keeps the process alive through the existing fresh
    // Dispatcher probe, then restores the windows after the probe finishes.
    internal static class ResumeRenderRecovery
    {
        private const int ProbeDelayMs = 12000;
        private const int ControlRestoreDelayMs = 22000;

        private static readonly object Sync = new object();
        private static readonly List<Window> ControlHiddenWindows = new List<Window>();
        private static bool started;
        private static bool resumeHideControl;
        private static int resumeGeneration;
        private static int probeGeneration;

        public static bool Start()
        {
            if (!IsProfileProcess())
                return true;

            lock (Sync)
            {
                if (started)
                    return true;
                started = true;
                resumeHideControl = HasSwitch("--resume-hide-control");
            }

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SafeLog("[ResumeProbe] FRESH_DISPATCHER probe armed");

            if (resumeHideControl)
            {
                SafeLog("[ResumeControl] ENABLED profile=" + GetProfileId() +
                    " mode=hide-visible-windows-on-suspend restoreDelayMs=" + ControlRestoreDelayMs);
            }

            return true;
        }

        public static bool ShouldSuppressDiagnosticException(Exception exception)
        {
            if (!IsProfileProcess() || !(exception is OutOfMemoryException))
                return false;

            string text = exception.ToString();
            return text.IndexOf("System.Windows.Media.Composition.DUCE.Channel.SyncFlush", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("System.Windows.Media.MediaContext", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("System.Windows.Interop.HwndTarget", StringComparison.Ordinal) >= 0;
        }

        private static bool IsProfileProcess()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase) ||
                    args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool HasSwitch(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string GetProfileId()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length ? args[i + 1] : "<missing>";

                if (args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring("--profile=".Length);
            }
            return "<none>";
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                if (resumeHideControl)
                    HideControlWindowsForSuspend();
                return;
            }

            if (e.Mode != PowerModes.Resume)
                return;

            int generation;
            lock (Sync)
            {
                resumeGeneration++;
                generation = resumeGeneration;
            }

            SafeLog("[ResumeProbe] RESUME generation=" + generation +
                " mainThread=" + Thread.CurrentThread.ManagedThreadId +
                " mainTier=" + (RenderCapability.Tier >> 16));

            if (resumeHideControl)
            {
                SafeLog("[ResumeControl] RESUME_HOLD_HIDDEN generation=" + generation +
                    " hiddenWindows=" + GetControlHiddenWindowCount() +
                    " mainTier=" + (RenderCapability.Tier >> 16));
                ScheduleControlRestore(generation);
            }

            // Do not depend on the poisoned UI Dispatcher to schedule this test.
            Thread launcher = new Thread(new ThreadStart(delegate
            {
                Thread.Sleep(ProbeDelayMs);
                lock (Sync)
                {
                    if (generation != resumeGeneration || probeGeneration == generation)
                        return;
                    probeGeneration = generation;
                }
                RunFreshDispatcherProbe(generation);
            }));
            launcher.IsBackground = true;
            launcher.Name = "Mugen WPF Resume Probe Launcher";
            launcher.Start();
        }

        private static void HideControlWindowsForSuspend()
        {
            Application app = Application.Current;
            if (app == null)
            {
                SafeLog("[ResumeControl] SUSPEND_HIDE_FAILED application=<null>");
                return;
            }

            SafeLog("[ResumeControl] SUSPEND_HIDE_BEGIN thread=" + Thread.CurrentThread.ManagedThreadId +
                " tier=" + (RenderCapability.Tier >> 16));

            try
            {
                app.Dispatcher.Invoke(DispatcherPriority.Send, new Action(delegate
                {
                    List<Window> visibleWindows = new List<Window>();
                    foreach (Window window in app.Windows)
                    {
                        if (window != null && window.IsVisible)
                            visibleWindows.Add(window);
                    }

                    lock (Sync)
                    {
                        ControlHiddenWindows.Clear();
                        ControlHiddenWindows.AddRange(visibleWindows);
                    }

                    foreach (Window window in visibleWindows)
                    {
                        try
                        {
                            IntPtr hwnd = new WindowInteropHelper(window).Handle;
                            SafeLog("[ResumeControl] HIDE_WINDOW type=" + window.GetType().FullName +
                                " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                                " title=" + SafeWindowTitle(window));
                            window.Hide();
                        }
                        catch (Exception ex)
                        {
                            SafeLog("[ResumeControl] HIDE_WINDOW_FAILED type=" + window.GetType().FullName + "\n" + ex);
                        }
                    }

                    SafeLog("[ResumeControl] SUSPEND_HIDE_OK hiddenWindows=" + visibleWindows.Count +
                        " uiThread=" + Thread.CurrentThread.ManagedThreadId +
                        " tier=" + (RenderCapability.Tier >> 16));
                }));
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeControl] SUSPEND_HIDE_FAILED\n" + ex);
            }
        }

        private static void ScheduleControlRestore(int generation)
        {
            Thread restorer = new Thread(new ThreadStart(delegate
            {
                Thread.Sleep(ControlRestoreDelayMs);

                lock (Sync)
                {
                    if (generation != resumeGeneration)
                        return;
                }

                Application app = Application.Current;
                if (app == null)
                {
                    SafeLog("[ResumeControl] RESTORE_FAILED generation=" + generation + " application=<null>");
                    return;
                }

                SafeLog("[ResumeControl] RESTORE_QUEUE generation=" + generation +
                    " hiddenWindows=" + GetControlHiddenWindowCount() +
                    " tier=" + (RenderCapability.Tier >> 16));

                try
                {
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate
                    {
                        List<Window> windows;
                        lock (Sync)
                        {
                            windows = new List<Window>(ControlHiddenWindows);
                            ControlHiddenWindows.Clear();
                        }

                        int restored = 0;
                        foreach (Window window in windows)
                        {
                            try
                            {
                                window.Show();
                                restored++;
                            }
                            catch (Exception ex)
                            {
                                SafeLog("[ResumeControl] RESTORE_WINDOW_FAILED type=" +
                                    (window == null ? "<null>" : window.GetType().FullName) + "\n" + ex);
                            }
                        }

                        SafeLog("[ResumeControl] RESTORE_OK generation=" + generation +
                            " restoredWindows=" + restored +
                            " uiThread=" + Thread.CurrentThread.ManagedThreadId +
                            " tier=" + (RenderCapability.Tier >> 16));
                    }));
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeControl] RESTORE_QUEUE_FAILED generation=" + generation + "\n" + ex);
                }
            }));
            restorer.IsBackground = true;
            restorer.Name = "Mugen Resume Control Restorer";
            restorer.Start();
        }

        private static int GetControlHiddenWindowCount()
        {
            lock (Sync)
                return ControlHiddenWindows.Count;
        }

        private static string SafeWindowTitle(Window window)
        {
            try
            {
                string title = window == null ? null : window.Title;
                if (string.IsNullOrEmpty(title)) return "<empty>";
                return title.Replace("\r", " ").Replace("\n", " ");
            }
            catch
            {
                return "<unavailable>";
            }
        }

        private static void RunFreshDispatcherProbe(int generation)
        {
            Thread thread = new Thread(new ThreadStart(delegate
            {
                HwndSource source = null;
                DispatcherTimer finishTimer = null;
                Dispatcher dispatcher = null;

                try
                {
                    dispatcher = Dispatcher.CurrentDispatcher;
                    SafeLog("[ResumeProbe] NEW_DISPATCHER_BEGIN generation=" + generation +
                        " thread=" + Thread.CurrentThread.ManagedThreadId +
                        " tierBeforeSource=" + (RenderCapability.Tier >> 16));

                    dispatcher.UnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                    {
                        SafeLog("[ResumeProbe] NEW_DISPATCHER_UNHANDLED generation=" + generation +
                            " type=" + e.Exception.GetType().FullName + "\n" + e.Exception);
                        e.Handled = true;
                        try { if (finishTimer != null) finishTimer.Stop(); } catch { }
                        try { if (source != null) source.Dispose(); } catch { }
                        try { Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send); } catch { }
                    };

                    HwndSourceParameters p = new HwndSourceParameters("HTC Home Mugen WPF Resume Probe");
                    p.Width = 96;
                    p.Height = 96;
                    p.PositionX = -32000;
                    p.PositionY = -32000;
                    p.WindowStyle = unchecked((int)0x80000000); // WS_POPUP
                    p.ExtendedWindowStyle = 0x00000080 | 0x08000000; // TOOLWINDOW | NOACTIVATE

                    source = new HwndSource(p);

                    Border visual = new Border();
                    visual.Width = 96;
                    visual.Height = 96;
                    visual.Background = Brushes.White;
                    visual.Child = new TextBlock
                    {
                        Text = "Mugen",
                        Foreground = Brushes.Black,
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    DoubleAnimation pulse = new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(300));
                    pulse.AutoReverse = true;
                    pulse.RepeatBehavior = RepeatBehavior.Forever;
                    visual.BeginAnimation(UIElement.OpacityProperty, pulse);

                    source.RootVisual = visual;

                    HwndTarget target = source.CompositionTarget;
                    SafeLog("[ResumeProbe] NEW_DISPATCHER_SOURCE_OK generation=" + generation +
                        " hwnd=0x" + source.Handle.ToInt64().ToString("X") +
                        " thread=" + Thread.CurrentThread.ManagedThreadId +
                        " tier=" + (RenderCapability.Tier >> 16) +
                        " renderMode=" + (target == null ? "<null>" : target.RenderMode.ToString()));

                    int ticks = 0;
                    DispatcherTimer pulseTimer = new DispatcherTimer(DispatcherPriority.Render);
                    pulseTimer.Interval = TimeSpan.FromMilliseconds(250);
                    pulseTimer.Tick += delegate
                    {
                        ticks++;
                        visual.InvalidateVisual();
                    };
                    pulseTimer.Start();

                    finishTimer = new DispatcherTimer(DispatcherPriority.Send);
                    finishTimer.Interval = TimeSpan.FromSeconds(6);
                    finishTimer.Tick += delegate
                    {
                        finishTimer.Stop();
                        pulseTimer.Stop();
                        SafeLog("[ResumeProbe] NEW_DISPATCHER_PROBE_OK generation=" + generation +
                            " ticks=" + ticks +
                            " tier=" + (RenderCapability.Tier >> 16) +
                            " renderMode=" + (source.CompositionTarget == null ? "<null>" : source.CompositionTarget.RenderMode.ToString()));
                        source.Dispose();
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    };
                    finishTimer.Start();

                    Dispatcher.Run();
                    SafeLog("[ResumeProbe] NEW_DISPATCHER_END generation=" + generation);
                }
                catch (OutOfMemoryException ex)
                {
                    SafeLog("[ResumeProbe] NEW_DISPATCHER_PROBE_OOM generation=" + generation + "\n" + ex);
                    try { if (source != null) source.Dispose(); } catch { }
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeProbe] NEW_DISPATCHER_PROBE_FAILED generation=" + generation + "\n" + ex);
                    try { if (source != null) source.Dispose(); } catch { }
                }
            }));

            thread.IsBackground = true;
            thread.Name = "Mugen Fresh WPF Dispatcher Probe";
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static void SafeLog(string message)
        {
            try { App.Log(message); }
            catch { }
        }
    }

    public partial class App
    {
        private static readonly bool ResumeRenderRecoveryBootstrap = ResumeRenderRecovery.Start();
    }
}
