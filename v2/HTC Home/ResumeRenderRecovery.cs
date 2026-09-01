using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
    // Timing experiment after run #54 proved that disabling only the existing
    // HwndTarget can protect a visible, Normal Window on a bad early resume.
    // All protected modes use exactly the same TargetOff mechanism; only the
    // restore delay differs (0s / 3s / 12s). No Window.Hide or Minimize is used.
    internal static class ResumeRenderRecovery
    {
        private enum ResumeDiagnosticMode
        {
            Normal,
            Target0,
            Target3,
            Target12
        }

        private sealed class ControlledWindow
        {
            public Window Window;
            public IntPtr Hwnd;
            public HwndSource Source;
            public HwndTarget Target;
            public MethodInfo UpdateWindowSettings;
            public HwndSourceHook SuppressReenableHook;
            public int UpdateWindowSettingsMessage;
            public bool TargetWasEnabled;
            public bool TargetDisableApplied;
        }

        private const int ProbeDelayMs = 15000;

        private static readonly object Sync = new object();
        private static readonly List<ControlledWindow> ControlledWindows = new List<ControlledWindow>();
        private static bool started;
        private static ResumeDiagnosticMode diagnosticMode;
        private static int resumeGeneration;
        private static int probeGeneration;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        public static bool Start()
        {
            if (!IsProfileProcess()) return true;

            lock (Sync)
            {
                if (started) return true;
                started = true;
                diagnosticMode = ParseDiagnosticMode();
            }

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SafeLog("[ResumeProbe] FRESH_DISPATCHER probe armed");
            SafeLog("[ResumeTiming] ARMED profile=" + GetProfileId() +
                " mode=" + ModeName(diagnosticMode) +
                " restoreDelayMs=" + RestoreDelayMs(diagnosticMode));
            return true;
        }

        public static bool ShouldSuppressDiagnosticException(Exception exception)
        {
            if (!IsProfileProcess() || !(exception is OutOfMemoryException)) return false;
            string text = exception.ToString();
            return text.IndexOf("System.Windows.Media.Composition.DUCE.Channel.SyncFlush", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("System.Windows.Media.MediaContext", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("System.Windows.Interop.HwndTarget", StringComparison.Ordinal) >= 0;
        }

        private static bool IsProfileProcess()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase) ||
                    args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static ResumeDiagnosticMode ParseDiagnosticMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                string value = null;
                if (string.Equals(args[i], "--resume-hide-control", StringComparison.OrdinalIgnoreCase))
                    return ResumeDiagnosticMode.Target0;
                if (string.Equals(args[i], "--resume-diag", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) value = args[i + 1];
                }
                else if (args[i].StartsWith("--resume-diag=", StringComparison.OrdinalIgnoreCase))
                    value = args[i].Substring("--resume-diag=".Length);

                if (string.IsNullOrWhiteSpace(value)) continue;
                if (string.Equals(value, "target0", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "hide", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticMode.Target0;
                if (string.Equals(value, "target3", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "targetoff", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "cloak", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticMode.Target3;
                if (string.Equals(value, "target12", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "minimize", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticMode.Target12;
                return ResumeDiagnosticMode.Normal;
            }
            return ResumeDiagnosticMode.Normal;
        }

        private static string ModeName(ResumeDiagnosticMode mode)
        {
            switch (mode)
            {
                case ResumeDiagnosticMode.Target0: return "target0";
                case ResumeDiagnosticMode.Target3: return "target3";
                case ResumeDiagnosticMode.Target12: return "target12";
                default: return "normal";
            }
        }

        private static int RestoreDelayMs(ResumeDiagnosticMode mode)
        {
            switch (mode)
            {
                case ResumeDiagnosticMode.Target3: return 3000;
                case ResumeDiagnosticMode.Target12: return 12000;
                case ResumeDiagnosticMode.Target0: return 0;
                default: return -1;
            }
        }

        private static string GetProfileId()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--profile", StringComparison.OrdinalIgnoreCase)) return i + 1 < args.Length ? args[i + 1] : "<missing>";
                if (args[i].StartsWith("--profile=", StringComparison.OrdinalIgnoreCase)) return args[i].Substring("--profile=".Length);
            }
            return "<none>";
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                if (diagnosticMode == ResumeDiagnosticMode.Normal)
                    SafeLog("[ResumeTiming] SUSPEND_BASELINE mode=normal thread=" + Thread.CurrentThread.ManagedThreadId);
                else
                    ApplySuspendTargetOff();
                return;
            }

            if (e.Mode != PowerModes.Resume) return;

            int generation;
            lock (Sync) { resumeGeneration++; generation = resumeGeneration; }

            SafeLog("[ResumeProbe] RESUME generation=" + generation + " mainThread=" + Thread.CurrentThread.ManagedThreadId);
            SafeLog("[ResumeTiming] RESUME generation=" + generation +
                " mode=" + ModeName(diagnosticMode) +
                " controlledWindows=" + GetControlledWindowCount() +
                " restoreDelayMs=" + RestoreDelayMs(diagnosticMode));

            if (diagnosticMode != ResumeDiagnosticMode.Normal)
                ScheduleTargetRestore(generation, RestoreDelayMs(diagnosticMode));

            Thread launcher = new Thread(new ThreadStart(delegate
            {
                Thread.Sleep(ProbeDelayMs);
                lock (Sync)
                {
                    if (generation != resumeGeneration || probeGeneration == generation) return;
                    probeGeneration = generation;
                }
                RunFreshDispatcherProbe(generation);
            }));
            launcher.IsBackground = true;
            launcher.Name = "Mugen WPF Resume Probe Launcher";
            launcher.Start();
        }

        private static void ApplySuspendTargetOff()
        {
            Application app = Application.Current;
            if (app == null)
            {
                SafeLog("[ResumeTiming] SUSPEND_APPLY_FAILED mode=" + ModeName(diagnosticMode) + " application=<null>");
                return;
            }

            SafeLog("[ResumeTiming] SUSPEND_APPLY_BEGIN mode=" + ModeName(diagnosticMode) +
                " thread=" + Thread.CurrentThread.ManagedThreadId);

            try
            {
                app.Dispatcher.Invoke(DispatcherPriority.Send, new Action(delegate
                {
                    List<ControlledWindow> records = new List<ControlledWindow>();
                    foreach (Window window in app.Windows)
                    {
                        if (window == null || !window.IsVisible) continue;

                        IntPtr hwnd = new WindowInteropHelper(window).Handle;
                        HwndSource source = HwndSource.FromHwnd(hwnd);
                        HwndTarget target = source == null ? null : source.CompositionTarget;

                        ControlledWindow record = new ControlledWindow
                        {
                            Window = window,
                            Hwnd = hwnd,
                            Source = source,
                            Target = target,
                            TargetWasEnabled = ReadBoolField(target, "_isRenderTargetEnabled", true)
                        };
                        records.Add(record);

                        SafeLog("[ResumeTiming] WINDOW_BEFORE mode=" + ModeName(diagnosticMode) +
                            " type=" + window.GetType().FullName +
                            " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                            " visible=" + window.IsVisible +
                            " state=" + window.WindowState +
                            " iconic=" + SafeIsIconic(hwnd) +
                            " targetEnabled=" + ReadField(target, "_isRenderTargetEnabled") +
                            " minimizedFlag=" + ReadField(target, "_isMinimized") +
                            " disableCookie=" + ReadField(target, "_disableCookie"));

                        ApplyTargetOff(record);

                        SafeLog("[ResumeTiming] WINDOW_APPLIED mode=" + ModeName(diagnosticMode) +
                            " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                            " visible=" + record.Window.IsVisible +
                            " state=" + record.Window.WindowState +
                            " iconic=" + SafeIsIconic(record.Hwnd) +
                            " targetEnabled=" + ReadField(record.Target, "_isRenderTargetEnabled") +
                            " minimizedFlag=" + ReadField(record.Target, "_isMinimized") +
                            " disableCookie=" + ReadField(record.Target, "_disableCookie") +
                            " targetDisableApplied=" + record.TargetDisableApplied);
                    }

                    lock (Sync)
                    {
                        ControlledWindows.Clear();
                        ControlledWindows.AddRange(records);
                    }

                    SafeLog("[ResumeTiming] SUSPEND_APPLY_OK mode=" + ModeName(diagnosticMode) +
                        " controlledWindows=" + records.Count +
                        " uiThread=" + Thread.CurrentThread.ManagedThreadId);
                }));
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeTiming] SUSPEND_APPLY_FAILED mode=" + ModeName(diagnosticMode) + "\n" + Unwrap(ex));
            }
        }

        private static void ApplyTargetOff(ControlledWindow record)
        {
            if (record.Source == null || record.Target == null)
                throw new InvalidOperationException("HwndSource/HwndTarget not available");

            record.UpdateWindowSettings = FindUpdateWindowSettings(record.Target);
            if (record.UpdateWindowSettings == null)
                throw new MissingMethodException(record.Target.GetType().FullName, "UpdateWindowSettings(Boolean)");

            record.UpdateWindowSettingsMessage = ReadStaticIntField(record.Target.GetType(), "s_updateWindowSettings");
            if (record.UpdateWindowSettingsMessage == 0)
                throw new MissingFieldException(record.Target.GetType().FullName, "s_updateWindowSettings");

            record.SuppressReenableHook = delegate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (msg == record.UpdateWindowSettingsMessage)
                {
                    handled = true;
                    SafeLog("[ResumeTiming] TARGET_REENABLE_SUPPRESSED mode=" + ModeName(diagnosticMode) +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                        " msg=0x" + msg.ToString("X", CultureInfo.InvariantCulture));
                }
                return IntPtr.Zero;
            };

            record.Source.AddHook(record.SuppressReenableHook);
            record.UpdateWindowSettings.Invoke(record.Target, new object[] { false });
            record.TargetDisableApplied = !ReadBoolField(record.Target, "_isRenderTargetEnabled", true);

            SafeLog("[ResumeTiming] TARGET_OFF_APPLIED mode=" + ModeName(diagnosticMode) +
                " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                " updateMsg=0x" + record.UpdateWindowSettingsMessage.ToString("X", CultureInfo.InvariantCulture) +
                " targetEnabled=" + ReadField(record.Target, "_isRenderTargetEnabled") +
                " disableCookie=" + ReadField(record.Target, "_disableCookie"));
        }

        private static void ScheduleTargetRestore(int generation, int delayMs)
        {
            Thread restorer = new Thread(new ThreadStart(delegate
            {
                if (delayMs > 0) Thread.Sleep(delayMs);
                lock (Sync) { if (generation != resumeGeneration) return; }

                Application app = Application.Current;
                if (app == null)
                {
                    SafeLog("[ResumeTiming] RESTORE_FAILED generation=" + generation + " application=<null>");
                    return;
                }

                SafeLog("[ResumeTiming] RESTORE_QUEUE generation=" + generation +
                    " mode=" + ModeName(diagnosticMode) +
                    " delayMs=" + delayMs +
                    " controlledWindows=" + GetControlledWindowCount());

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
                                RestoreTarget(record);
                                restored++;
                                SafeLog("[ResumeTiming] WINDOW_RESTORED mode=" + ModeName(diagnosticMode) +
                                    " delayMs=" + delayMs +
                                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                                    " sameHwnd=" + (new WindowInteropHelper(record.Window).Handle == record.Hwnd) +
                                    " visible=" + record.Window.IsVisible +
                                    " state=" + record.Window.WindowState +
                                    " iconic=" + SafeIsIconic(record.Hwnd) +
                                    " targetEnabled=" + ReadField(record.Target, "_isRenderTargetEnabled") +
                                    " disableCookie=" + ReadField(record.Target, "_disableCookie"));
                            }
                            catch (Exception ex)
                            {
                                SafeLog("[ResumeTiming] RESTORE_WINDOW_FAILED mode=" + ModeName(diagnosticMode) +
                                    " delayMs=" + delayMs +
                                    " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") + "\n" + Unwrap(ex));
                            }
                        }

                        SafeLog("[ResumeTiming] RESTORE_OK generation=" + generation +
                            " mode=" + ModeName(diagnosticMode) +
                            " delayMs=" + delayMs +
                            " restoredWindows=" + restored +
                            " uiThread=" + Thread.CurrentThread.ManagedThreadId);
                    }));
                }
                catch (Exception ex)
                {
                    SafeLog("[ResumeTiming] RESTORE_QUEUE_FAILED generation=" + generation +
                        " mode=" + ModeName(diagnosticMode) + "\n" + Unwrap(ex));
                }
            }));
            restorer.IsBackground = true;
            restorer.Name = "Mugen HwndTarget Restore " + delayMs + "ms";
            restorer.Start();
        }

        private static void RestoreTarget(ControlledWindow record)
        {
            if (record.Source != null && record.SuppressReenableHook != null)
            {
                try { record.Source.RemoveHook(record.SuppressReenableHook); } catch { }
            }

            if (record.TargetWasEnabled && record.Target != null && record.UpdateWindowSettings != null)
                record.UpdateWindowSettings.Invoke(record.Target, new object[] { true });

            SafeLog("[ResumeTiming] TARGET_RESTORED mode=" + ModeName(diagnosticMode) +
                " hwnd=0x" + record.Hwnd.ToInt64().ToString("X") +
                " targetEnabled=" + ReadField(record.Target, "_isRenderTargetEnabled") +
                " disableCookie=" + ReadField(record.Target, "_disableCookie"));
        }

        private static MethodInfo FindUpdateWindowSettings(HwndTarget target)
        {
            if (target == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            Type type = target.GetType();
            while (type != null)
            {
                MethodInfo method = type.GetMethod("UpdateWindowSettings", flags, null, new[] { typeof(bool) }, null);
                if (method != null) return method;
                type = type.BaseType;
            }
            return null;
        }

        private static FieldInfo FindField(Type type, string name, bool isStatic)
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly |
                (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            while (type != null)
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private static string ReadField(object instance, string name)
        {
            if (instance == null) return "<null>";
            try
            {
                FieldInfo field = FindField(instance.GetType(), name, false);
                if (field == null) return "<missing>";
                object value = field.GetValue(instance);
                return value == null ? "<null>" : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { return "<error:" + ex.GetType().Name + ">"; }
        }

        private static bool ReadBoolField(object instance, string name, bool fallback)
        {
            if (instance == null) return fallback;
            try
            {
                FieldInfo field = FindField(instance.GetType(), name, false);
                object value = field == null ? null : field.GetValue(instance);
                return value is bool ? (bool)value : fallback;
            }
            catch { return fallback; }
        }

        private static int ReadStaticIntField(Type type, string name)
        {
            try
            {
                FieldInfo field = FindField(type, name, true);
                if (field == null) return 0;
                object value = field.GetValue(null);
                return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch { return 0; }
        }

        private static string Unwrap(Exception ex)
        {
            TargetInvocationException tie = ex as TargetInvocationException;
            return tie != null && tie.InnerException != null ? tie.InnerException.ToString() : ex.ToString();
        }

        private static string SafeIsIconic(IntPtr hwnd)
        {
            try { return IsIconic(hwnd).ToString(); }
            catch { return "<unavailable>"; }
        }

        private static int GetControlledWindowCount()
        {
            lock (Sync) return ControlledWindows.Count;
        }

        private static void RunFreshDispatcherProbe(int generation)
        {
            Thread thread = new Thread(new ThreadStart(delegate
            {
                HwndSource source = null;
                DispatcherTimer finishTimer = null;

                try
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    SafeLog("[ResumeProbe] NEW_DISPATCHER_BEGIN generation=" + generation +
                        " thread=" + Thread.CurrentThread.ManagedThreadId);

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

                    Border visual = new Border { Width = 96, Height = 96, Background = Brushes.White };
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
                    pulseTimer.Tick += delegate { ticks++; visual.InvalidateVisual(); };
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
            try { App.Log(message); } catch { }
        }
    }

    public partial class App
    {
        private static readonly bool ResumeRenderRecoveryBootstrap = ResumeRenderRecovery.Start();
    }
}
