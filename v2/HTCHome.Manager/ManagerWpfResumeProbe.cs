using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HTCHome.Manager
{
    internal static class ManagerWpfResumeProbe
    {
        public static void ProbeExistingUi(int generation, string label)
        {
            try
            {
                Application app = Application.Current;
                if (app == null)
                {
                    ResumeSystemDiagnostics.Trace("MANAGER_WPF_UI label=" + label + " generation=" + generation + " app=<null>");
                    return;
                }

                app.Dispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        int tier = RenderCapability.Tier >> 16;
                        int windows = app.Windows == null ? 0 : app.Windows.Count;
                        int visible = 0;
                        string sourceState = "<none>";

                        if (app.Windows != null)
                        {
                            foreach (Window window in app.Windows)
                            {
                                if (window.IsVisible) visible++;
                                if (sourceState == "<none>")
                                {
                                    HwndSource source = PresentationSource.FromVisual(window) as HwndSource;
                                    if (source != null)
                                    {
                                        sourceState = "hwnd=0x" + source.Handle.ToInt64().ToString("X") +
                                                      ",renderMode=" + (source.CompositionTarget == null
                                                          ? "<null>"
                                                          : source.CompositionTarget.RenderMode.ToString());
                                    }
                                }
                            }
                        }

                        ResumeSystemDiagnostics.Trace("MANAGER_WPF_UI_OK label=" + label +
                            " generation=" + generation +
                            " thread=" + Thread.CurrentThread.ManagedThreadId +
                            " tier=" + tier +
                            " windows=" + windows +
                            " visible=" + visible +
                            " source=" + sourceState);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        ResumeSystemDiagnostics.Trace("MANAGER_WPF_UI_OOM label=" + label +
                            " generation=" + generation + " " + ex);
                    }
                    catch (Exception ex)
                    {
                        ResumeSystemDiagnostics.Trace("MANAGER_WPF_UI_FAILED label=" + label +
                            " generation=" + generation + " " + ex);
                    }
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                ResumeSystemDiagnostics.Trace("MANAGER_WPF_UI_QUEUE_FAILED label=" + label +
                    " generation=" + generation + " " + ex);
            }
        }

        public static void RunFreshDispatcherAfterDelay(int generation, int delayMs)
        {
            Thread launcher = new Thread(delegate
            {
                Thread.Sleep(delayMs);
                RunFreshDispatcher(generation);
            });
            launcher.IsBackground = true;
            launcher.Name = "Mugen Manager WPF probe launcher";
            launcher.Start();
        }

        private static void RunFreshDispatcher(int generation)
        {
            Thread thread = new Thread(delegate
            {
                HwndSource source = null;
                DispatcherTimer finishTimer = null;

                try
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    ResumeSystemDiagnostics.Trace("MANAGER_WPF_FRESH_BEGIN generation=" + generation +
                        " thread=" + Thread.CurrentThread.ManagedThreadId);

                    dispatcher.UnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                    {
                        ResumeSystemDiagnostics.Trace("MANAGER_WPF_FRESH_UNHANDLED generation=" + generation +
                            " type=" + e.Exception.GetType().FullName + " " + e.Exception);
                        e.Handled = true;
                        try { if (finishTimer != null) finishTimer.Stop(); } catch { }
                        try { if (source != null) source.Dispose(); } catch { }
                        try { Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send); } catch { }
                    };

                    int tierBefore = RenderCapability.Tier >> 16;
                    ResumeSystemDiagnostics.Trace("MANAGER_WPF_FRESH_TIER generation=" + generation +
                        " tierBeforeSource=" + tierBefore);

                    HwndSourceParameters p = new HwndSourceParameters("HTC Home Mugen Manager WPF Resume Control");
                    p.Width = 64;
                    p.Height = 64;
                    p.PositionX = -32000;
                    p.PositionY = -32000;
                    p.WindowStyle = unchecked((int)0x80000000); // WS_POPUP
                    p.ExtendedWindowStyle = 0x00000080 | 0x08000000; // TOOLWINDOW | NOACTIVATE
                    source = new HwndSource(p);

                    Border visual = new Border
                    {
                        Width = 64,
                        Height = 64,
                        Background = Brushes.White
                    };
                    visual.Child = new TextBlock
                    {
                        Text = "M",
                        Foreground = Brushes.Black,
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    DoubleAnimation pulse = new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(250));
                    pulse.AutoReverse = true;
                    pulse.RepeatBehavior = RepeatBehavior.Forever;
                    visual.BeginAnimation(UIElement.OpacityProperty, pulse);
                    source.RootVisual = visual;

                    ResumeSystemDiagnostics.Trace("MANAGER_WPF_FRESH_SOURCE_OK generation=" + generation +
                        " hwnd=0x" + source.Handle.ToInt64().ToString("X") +
                        " tier=" + (RenderCapability.Tier >> 16) +
                        " renderMode=" + (source.CompositionTarget == null
                            ? "<null>"
                            : source.CompositionTarget.RenderMode.ToString()));

                    int ticks = 0;
                    DispatcherTimer renderTimer = new DispatcherTimer(DispatcherPriority.Render)
                    {
                        Interval = TimeSpan.FromMilliseconds(250)
                    };
                    renderTimer.Tick += delegate
                    {
                        ticks++;
                        visual.InvalidateVisual();
                    };
                    renderTimer.Start();

                    finishTimer = new DispatcherTimer(DispatcherPriority.Send)
                    {
                        Interval = TimeSpan.FromSeconds(5)
                    };
                    finishTimer.Tick += delegate
                    {
                        finishTimer.Stop();
                        renderTimer.Stop();
                        ResumeSystemDiagnostics.Trace("MANAGER_WPF_FRESH_OK generation=" + generation +
                            " ticks=" + ticks +
                            " tier=" + (RenderCapability.Tier >> 16));
                        source.Dispose();
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    };
                    finishTimer.Start();

                    Dispatcher.Run();
                    ResumeSystemDiagnostics.Trace("MANAGER_WPF_FRESH_END generation=" + generation);
                }
                catch (OutOfMemoryException ex)
                {
                    ResumeSystemDiagnostics.Trace("MANAGER_WPF_FRESH_OOM generation=" + generation + " " + ex);
                    try { if (source != null) source.Dispose(); } catch { }
                }
                catch (Exception ex)
                {
                    ResumeSystemDiagnostics.Trace("MANAGER_WPF_FRESH_FAILED generation=" + generation + " " + ex);
                    try { if (source != null) source.Dispose(); } catch { }
                }
            });

            thread.IsBackground = true;
            thread.Name = "Mugen Manager Fresh WPF Dispatcher Probe";
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }
}
