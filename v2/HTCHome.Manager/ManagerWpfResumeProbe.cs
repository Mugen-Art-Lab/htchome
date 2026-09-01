using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        private static bool windowHooksAttached;
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

            try
            {
                Application app = Application.Current;
                if (app != null)
                    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(AttachMainWindowHooks));
            }
            catch { }
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

        private static void AttachMainWindowHooks()
        {
            if (windowHooksAttached) return;
            Application app = Application.Current;
            if (app == null) return;

            Window window = app.MainWindow;
            if (window == null && app.Windows != null && app.Windows.Count > 0)
                window = app.Windows[0];
            if (window == null)
            {
                app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(AttachMainWindowHooks));
                return;
            }

            windowHooksAttached = true;
            Window captured = window;
            captured.IsVisibleChanged += delegate
            {
                TraceWindow(captured, "visibility-changed");
                if (captured.IsVisible)
                {
                    captured.Dispatcher.BeginInvoke(DispatcherPriority.Render,
                        new Action(delegate { TraceWindow(captured, "visible+render-priority"); }));
                    ScheduleWindowTrace(captured, "visible+250ms", 250);
                    ScheduleWindowTrace(captured, "visible+1000ms", 1000);
                }
            };
            captured.StateChanged += delegate { TraceWindow(captured, "state-changed"); };
            TraceWindow(captured, "hooks-attached");
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

                        Window main = app.MainWindow;
                        if (main != null) TraceWindow(main, "resume-existing-ui");
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

        private static void ScheduleWindowTrace(Window window, string label, int delayMs)
        {
            if (window == null) return;
            DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher);
            timer.Interval = TimeSpan.FromMilliseconds(delayMs);
            timer.Tick += delegate
            {
                timer.Stop();
                TraceWindow(window, label);
            };
            timer.Start();
        }

        private static void TraceWindow(Window window, string label)
        {
            try
            {
                if (window == null)
                {
                    Write("MANAGER_WINDOW_STATE label=" + label + " window=<null>");
                    return;
                }

                HwndSource source = PresentationSource.FromVisual(window) as HwndSource;
                if (source == null)
                {
                    Write("MANAGER_WINDOW_STATE label=" + label +
                        " visible=" + window.IsVisible + " state=" + window.WindowState + " source=<null>");
                    return;
                }

                HwndTarget target = source.CompositionTarget;
                object mediaContext = GetExistingMediaContext(target);
                object currentRenderOp = GetFieldValue(mediaContext, "_currentRenderOp");

                Write("MANAGER_WINDOW_STATE label=" + label +
                    " hwnd=0x" + source.Handle.ToInt64().ToString("X") +
                    " visible=" + window.IsVisible +
                    " state=" + window.WindowState +
                    " active=" + window.IsActive +
                    " targetId=" + (target == null ? 0 : RuntimeHelpers.GetHashCode(target)) +
                    " target.suspended=" + ReadField(target, "_isSuspended") +
                    " target.enabled=" + ReadField(target, "_isRenderTargetEnabled") +
                    " target.needsRePresent=" + ReadField(target, "_needsRePresentOnWake") +
                    " target.hasRePresented=" + ReadField(target, "_hasRePresentedSinceWake") +
                    " target.disableCookie=" + ReadField(target, "_disableCookie") +
                    " mcId=" + (mediaContext == null ? 0 : RuntimeHelpers.GetHashCode(mediaContext)) +
                    " mc.interlock=" + ReadField(mediaContext, "_interlockState") +
                    " mc.currentRenderOp=" + FormatDispatcherOperation(currentRenderOp) +
                    " mc.isRendering=" + ReadField(mediaContext, "_isRendering") +
                    " mc.needCommit=" + ReadField(mediaContext, "_needToCommitChannel") +
                    " mc.commitPending=" + ReadField(mediaContext, "_commitPendingAfterRender"));
            }
            catch (Exception ex)
            {
                Write("MANAGER_WINDOW_STATE_FAILED label=" + label + " " + ex);
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
                object value = field.GetValue(instance);
                return value == null ? "<null>" : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { return "<error:" + ex.GetType().Name + ">"; }
        }

        private static string FormatDispatcherOperation(object value)
        {
            DispatcherOperation op = value as DispatcherOperation;
            if (op == null) return value == null ? "<null>" : "<" + value.GetType().Name + ">";
            try { return "status=" + op.Status + ",priority=" + op.Priority; }
            catch { return "<DispatcherOperation>"; }
        }

        private static void RunFreshDispatcherAfterDelay(int current, int delayMs)
        {
            Thread launcher = new Thread(new ThreadStart(delegate
            {
                Thread.Sleep(delayMs);
                RunFreshDispatcher(current);
            }));
            launcher.IsBackground = true;
            launcher.Name = "Mugen Manager WPF probe launcher";
            launcher.Start();
        }

        private static void RunFreshDispatcher(int current)
        {
            Thread thread = new Thread(new ThreadStart(delegate
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
            }));
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
