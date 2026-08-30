using System;
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
    // Process-local diagnostic for the post-hibernate DUCE failure.
    // A fresh HwndTarget on the OLD Dispatcher already failed, which points at
    // Dispatcher/MediaContext scope. This probe creates a completely new STA
    // Dispatcher on another thread after Resume, then creates an off-screen
    // HwndSource with its own animated visual tree. If it can render for several
    // seconds, a damaged main Dispatcher/MediaContext can potentially be replaced
    // without restarting the process. If this probe gets the same DUCE OOM, the
    // broken state is below Dispatcher scope and a process restart is the honest
    // recovery boundary.
    internal static class ResumeRenderRecovery
    {
        private static readonly object Sync = new object();
        private static bool started;
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
            }

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SafeLog("[ResumeProbe] FRESH_DISPATCHER probe armed");
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

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
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

            // Do not depend on the poisoned UI Dispatcher to schedule this test.
            Thread launcher = new Thread(delegate()
            {
                Thread.Sleep(12000);
                lock (Sync)
                {
                    if (generation != resumeGeneration || probeGeneration == generation)
                        return;
                    probeGeneration = generation;
                }
                RunFreshDispatcherProbe(generation);
            });
            launcher.IsBackground = true;
            launcher.Name = "Mugen WPF Resume Probe Launcher";
            launcher.Start();
        }

        private static void RunFreshDispatcherProbe(int generation)
        {
            Thread thread = new Thread(delegate()
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
                        if (source.RootVisual != null)
                            source.RootVisual.InvalidateVisual();
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
            });

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
