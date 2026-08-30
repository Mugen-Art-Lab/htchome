using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace HTCHome.Manager
{
    internal static class ManagerWpfResumeProbe
    {
        private static int generation;
        private static bool started;
        private static readonly object Sync = new object();

        public static void Start()
        {
            lock (Sync)
            {
                if (started) return;
                started = true;
            }
            SystemEvents.PowerModeChanged += PowerModeChanged;
            Write("MANAGER_WPF_PROBE_ARMED");
        }

        public static void Stop()
        {
            lock (Sync)
            {
                if (!started) return;
                started = false;
            }
            try { SystemEvents.PowerModeChanged -= PowerModeChanged; } catch { }
        }

        private static void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume) return;
            int current = Interlocked.Increment(ref generation);
            ProbeExistingUi(current, "resume+0");
            RunFreshDispatcherAfterDelay(current, 12000);
        }

        private static void ProbeExistingUi(int current, string label)
        {
            try
            {
                Application app = Application.Current;
                if (app == null)
                {
                    Write("MANAGER_WPF_UI app=<null> generation=" + current);
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
                                        sourceState = "hwnd=0x" + source.Handle.ToInt64().ToString("X") + ",renderMode=" +
                                            (source.CompositionTarget == null ? "<null>" : source.CompositionTarget.RenderMode.ToString());
                                }
                            }
                        }

                        Write("MANAGER_WPF_UI_OK label=" + label + " generation=" + current +
                              " tier=" + tier + " windows=" + windows + " visible=" + visible + " source=" + sourceState);
                    }
                    catch (Exception ex)
                    {
                        Write("MANAGER_WPF_UI_FAILED generation=" + current + " " + ex);
                    }
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Write("MANAGER_WPF_UI_QUEUE_FAILED generation=" + current + " " + ex);
            }
        }

        private static void RunFreshDispatcherAfterDelay(int current, int delayMs)
        {
            Thread launcher = new Thread(delegate
            {
                Thread.Sleep(delayMs);
                RunFreshDispatcher(current);
            });
            launcher.IsBackground = true;
            launcher.Name = "Mugen Manager WPF probe launcher";
            launcher.Start();
        }

        private static void RunFreshDispatcher(int current)
        {
            Thread thread = new Thread(delegate
            {
                HwndSource source = null;
                DispatcherTimer finishTimer = null;
                try
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    Write("MANAGER_WPF_FRESH_BEGIN generation=" + current + " tierBeforeSource=" + (RenderCapability.Tier >> 16));

                    HwndSourceParameters p = new HwndSourceParameters("HTC Home Mugen Manager WPF Resume Control");
                    p.Width = 64;
                    p.Height = 64;
                    p.PositionX = -32000;
                    p.PositionY = -32000;
                    p.WindowStyle = unchecked((int)0x80000000);
                    p.ExtendedWindowStyle = 0x00000080 | 0x08000000;
                    source = new HwndSource(p);

                    Border visual = new Border { Width = 64, Height = 64, Background = Brushes.White };
                    visual.Child = new TextBlock { Text = "M", Foreground = Brushes.Black, FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

                    DoubleAnimation pulse = new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(250));
                    pulse.AutoReverse = true;
                    pulse.RepeatBehavior = RepeatBehavior.Forever;
                    visual.BeginAnimation(UIElement.OpacityProperty, pulse);
                    source.RootVisual = visual;

                    Write("MANAGER_WPF_FRESH_SOURCE_OK generation=" + current + " tier=" + (RenderCapability.Tier >> 16) +
                          " renderMode=" + (source.CompositionTarget == null ? "<null>" : source.CompositionTarget.RenderMode.ToString()));

                    int ticks = 0;
                    DispatcherTimer renderTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
                    renderTimer.Tick += delegate { ticks++; visual.InvalidateVisual(); };
                    renderTimer.Start();

                    finishTimer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
                    finishTimer.Tick += delegate
                    {
                        finishTimer.Stop();
                        renderTimer.Stop();
                        Write("MANAGER_WPF_FRESH_OK generation=" + current + " ticks=" + ticks + " tier=" + (RenderCapability.Tier >> 16));
                        source.Dispose();
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    };
                    finishTimer.Start();

                    Dispatcher.Run();
                }
                catch (OutOfMemoryException ex)
                {
                    Write("MANAGER_WPF_FRESH_OOM generation=" + current + " " + ex);
                    try { if (source != null) source.Dispose(); } catch { }
                }
                catch (Exception ex)
                {
                    Write("MANAGER_WPF_FRESH_FAILED generation=" + current + " " + ex);
                    try { if (source != null) source.Dispose(); } catch { }
                }
            });
            thread.IsBackground = true;
            thread.Name = "Mugen Manager Fresh WPF Dispatcher Probe";
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static void Write(string text)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "manager-wpf-resume-probe.log");
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
