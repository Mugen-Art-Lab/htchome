using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    // Four-way suspend/resume composition experiment.
    // Baseline is untouched. Hide uses WPF Window.Hide(). Cloak leaves the WPF
    // Window visible but asks DWM not to present the HWND. Minimize keeps the HWND
    // alive but moves it to the minimized state. All non-baseline modes are held
    // through the same +12s fresh-MediaContext probe and restored at +22s.
    internal static class ResumeRenderRecovery
    {
        private enum ResumeDiagnosticMode
        {
            Normal,
            Hide,
            Cloak,
            Minimize
        }

        private sealed class ControlledWindow
        {
            public Window Window;
            public IntPtr Hwnd;
            public WindowState PreviousWindowState;
            public bool WasCloaked;
        }

        private const int ProbeDelayMs = 12000;
        private const int ControlRestoreDelayMs = 22000;
        private const int DWMWA_CLOAK = 13;
        private const int DWMWA_CLOAKED = 14;

        private static readonly object Sync = new object();
        private static readonly List<ControlledWindow> ControlledWindows = new List<ControlledWindow>();
        private static bool started;
        private static ResumeDiagnosticMode diagnosticMode;
        private static int resumeGeneration;
        private static int probeGeneration;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        public static bool Start()
        {
            if (!IsProfileProcess())
                return true;

            lock (Sync)
            {
                if (started)
                    return true;
                started = true;
                diagnosticMode = ParseDiagnosticMode();
            }

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SafeLog("[ResumeProbe] FRESH_DISPATCHER probe armed");
            SafeLog("[ResumeMatrix] ARMED profile=" + GetProfileId() +
                " mode=" + ModeName(diagnosticMode) +
                " restoreDelayMs=" + ControlRestoreDelayMs);
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

        private static ResumeDiagnosticMode ParseDiagnosticMode()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--resume-hide-control", StringComparison.OrdinalIgnoreCase))
                    return ResumeDiagnosticMode.Hide;

                string value = null;
                if (string.Equals(args[i], "--resume-diag", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) value = args[i + 1];
                }
                else if (args[i].StartsWith("--resume-diag=", StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i].Substring("--resume-diag=".Length);
                }

                if (string.IsNullOrWhiteSpace(value)) continue;
                if (string.Equals(value, "hide", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticMode.Hide;
                if (string.Equals(value, "cloak", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticMode.Cloak;
                if (string.Equals(value, "minimize", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticMode.Minimize;
                return ResumeDiagnosticMode.Normal;
            }
            return ResumeDiagnosticMode.Normal;
        }

        private static string ModeName(ResumeDiagnosticMode mode)
        {
            switch (mode)
            {
                case ResumeDiagnosticMode.Hide: return "hide";
                case ResumeDiagnosticMode.Cloak: return "cloak";
                case ResumeDiagnosticMode.Minimize: return "minimize";
                default: return "normal";
            }
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
                if (diagnosticMode == ResumeDiagnosticMode.Normal)
                {
                    // Deliberately do not invoke the UI Dispatcher for the baseline.
                    SafeLog("[ResumeMatrix] SUSPEND_BASELINE mode=normal thread=" +
                        Thread.CurrentThread.ManagedThreadId + " tier=" + (RenderCapability.Tier >> 16));
                }
                else
                {
                    ApplySuspendControl();
                }
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
            SafeLog("[ResumeMatrix] RESUME generation=" + generation +
                " mode=" + ModeName(diagnosticMode) +
                " controlledWindows=" + GetControlledWindowCount() +
                " mainTier=" + (RenderCapability.Tier >> 16));

            if (diagnosticMode != ResumeDiagnosticMode.Normal)
                ScheduleControlRestore(generation);

            // Do not depend on a potentially poisoned UI Dispatcher to schedule this probe.
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

        private static void ApplySuspendControl()
        {
            Application app = Application.Current;
            if (app == null)
            {
                SafeLog("[ResumeMatrix] SUSPEND_APPLY_FAILED mode=" + ModeName(diagnosticMode) + " application=<null>");
                return;
            }

            SafeLog("[ResumeMatrix] SUSPEND_APPLY_BEGIN mode=" + ModeName(diagnosticMode) +
                " thread=" + Thread.CurrentThread.ManagedThreadId +
                " tier=" + (RenderCapability.Tier >> 16));

            try
            {
                app.Dispatcher.Invoke(DispatcherPriority.Send, new Action(delegate
                {
                    List<ControlledWindow> records = new List<ControlledWindow>();
                    foreach (Window window in app.Windows)
                    {
                        if (window == null || !window.IsVisible) continue;

                        IntPtr hwnd = new WindowInteropHelper(window).Handle;
                        int cloaked = 0;
                        bool wasCloaked = false;
                        try
                        {
                            wasCloaked = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0;
                        }
                        catch { }

                        ControlledWindow record = new ControlledWindow
                        {
                            Window = window,
                            Hwnd = hwnd,
                            PreviousWindowState = window.WindowState,
                            WasCloaked = wasCloaked
                        };
                        records.Add(record);

                        SafeLog("[ResumeMatrix] WINDOW_BEFORE mode=" + ModeName(diagnosticMode) +
                            " type=" + window.GetType().FullName +
                            " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                            " visible=" + window.IsVisible +
                            " state=" + window.WindowState +
                            " iconic=" + SafeIsIconic(hwnd) +
                            " dwmCloaked=" + wasCloaked);

                        ApplyModeToWindow(record);
                    }

                    lock (Sync)
                    {
                        ControlledWindows.Clear();
                        ControlledWindows.AddRange(records);
                    }

                    SafeLog("[ResumeMatrix] SUSPEND_APPLY_OK mode=" + ModeName(diagnosticMode) +
                        " controlledWindows=" + records.Count +
                        " uiThread=" + Thread.CurrentThread.ManagedThreadId +
                        " tier=" + (RenderCapability.Tier >> 16));
                }));
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeMatrix] SUSPEND_APPLY_FAILED mode=" + ModeName(diagnosticMode) + "\n" + ex);
            }
        }

        private static void ApplyModeToWindow(ControlledWindow record)
        {
            try
            {
                int hr = 0;
                switch (diagnosticMode)
                {
                    case ResumeDiagnosticMode.Hide:
                        record.Window.Hide();
                        break;

                    case ResumeDiagnosticMode.Cloak:
                        int cloak = 1;
                        hr = DwmSetWindowAttribute(record.Hwnd, DWMWA_CLOAK, ref cloak, sizeof(int));
                        break;

                    case ResumeDiagnosticMode.Minimize:
                        record.Window.WindowState = WindowState.Minimized;
                        break;
                }

                SafeLog("[ResumeMatrix] WINDOW_APPLIED mode=" + ModeName(diagnosticMode) +
                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                    " visible=" + record.Window.IsVisible +
                    " state=" + record.Window.WindowState +
                    " iconic=" + SafeIsIconic(record.Hwnd) +
                    " hr=" + hr);
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeMatrix] WINDOW_APPLY_FAILED mode=" + ModeName(diagnosticMode) +
                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") + "\n" + ex);
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
                    SafeLog("[ResumeMatrix] RESTORE_FAILED generation=" + generation + " application=<null>");
                    return;
                }

                SafeLog("[ResumeMatrix] RESTORE_QUEUE generation=" + generation +
                    " mode=" + ModeName(diagnosticMode) +
                    " controlledWindows=" + GetControlledWindowCount() +
                    " tier=" + (RenderCapability.Tier >> 16));

                try
                {
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate
                    {
                        List<ControlledWindow> records;
                        lock (Sync)
                        {
                            records = new List<ControlledWindow>(ControlledWindows);
                            ControlledWindows.Clear();
                        }

                        int restored = 0;
                        foreach (ControlledWindow record in records)
                        {
                            try
                            {
                                RestoreWindow(record);
                                restored++;
                                SafeLog("[ResumeMatrix] WINDOW_RESTORED mode=" + ModeName(diagnosticMode) +
                                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                                    " sameHwnd=" + (new WindowInteropHelper(record.Window).Handle == record.Hwnd) +
                                    " visible=" + record.Window.IsVisible +
                                    " state=" + record.Window.WindowState +
                                    " iconic=" + SafeIsIconic(record.Hwnd));
                            }
                            catch (Exception ex)
                            {
                                SafeLog("[ResumeMatrix] RESTORE_WINDOW_FAILED mode=" + ModeName(diagnosticMode) +
                                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") + "\n" + ex);
                            }
                        }

                        SafeLog("[ResumeMatrix] RESTORE_OK generation=" + generation +
                            " mode=" + ModeName(diagnosticMode) +
                            " restoredWindows=" + restored +
                            " uiThread=" + Thread.CurrentThread.ManagedThreadId +
                            " tier=" + (RenderCapability.Tier >> 16));
                    }));
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeMatrix] RESTORE_QUEUE_FAILED generation=" + generation +
                        " mode=" + ModeName(diagnosticMode) + "\n" + ex);
                }
            }));
            restorer.IsBackground = true;
            restorer.Name = "Mugen Resume Matrix Restorer";
            restorer.Start();
        }

        private static void RestoreWindow(ControlledWindow record)
        {
            switch (diagnosticMode)
            {
                case ResumeDiagnosticMode.Hide:
                    record.Window.Show();
                    break;

                case ResumeDiagnosticMode.Cloak:
                    int cloak = record.WasCloaked ? 1 : 0;
                    int hr = DwmSetWindowAttribute(record.Hwnd, DWMWA_CLOAK, ref cloak, sizeof(int));
                    if (hr != 0)
                        SafeLog("[ResumeMatrix] UNCLOAK_HRESULT hwnd=0x" + record.Hwnd.ToInt64().ToString("X") + " hr=" + hr);
                    break;

                case ResumeDiagnosticMode.Minimize:
                    record.Window.WindowState = record.PreviousWindowState;
                    break;
            }
        }

        private static string SafeIsIconic(IntPtr hwnd)
        {
            try { return IsIconic(hwnd).ToString(); }
            catch { return "<unavailable>"; }
        }

        private static int GetControlledWindowCount()
        {
            lock (Sync)
                return ControlledWindows.Count;
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
                    p.WindowStyle = unchecked((int)0x80000000);
                    p.ExtendedWindowStyle = 0x00000080 | 0x08000000;

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
