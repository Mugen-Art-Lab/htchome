using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HTCHome.Core;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace HTCHome
{
    /// <summary>
    /// Interaction logic for Widget.xaml
    /// </summary>
    public partial class Widget : Window
    {
        private IntPtr handle;
        private IWidget widget;
        public string path;

        public string WidgetName
        {
            get;
            set;
        }

        public string WidgetIcon
        {
            get;
            set;
        }

        public bool IsWidgetLoaded
        {
            get;
            set;
        }

        public bool HasErrors
        {
            get;
            set;
        }

        public Widget()
        {
            InitializeComponent();
        }

        public void Initalize(string path)
        {
            this.path = path;

            Assembly assembly = Assembly.LoadFrom(path);

            Type widgetType = null;

            try
            {
                widgetType = assembly.GetTypes().FirstOrDefault(type => typeof(IWidget).IsAssignableFrom(type));
            }
            catch (Exception ex)
            {
                App.Log(ex.ToString());
            }

            if (widgetType == null)
            {
                App.Log(path + " is not a widget.");
                HasErrors = true;
                return;
            }

            widget = Activator.CreateInstance(widgetType) as IWidget;
            WidgetName = widget.GetWidgetName();
            WidgetIcon = widget.GetIcon();

            widget.UpdateAeroEvent += widget_UpdateAero;

            CloseItem.Header = App.LocaleManager.GetString("Close");
            CloseHomeItem.Header = App.LocaleManager.GetString("CloseHome");

            GalleryItem.Header = App.LocaleManager.GetString("Widgets");
            HomeOptionsItem.Header = App.LocaleManager.GetString("HomeOptions");
            PinItem.Header = App.LocaleManager.GetString("Pin");
            TopMostItem.Header = App.LocaleManager.GetString("TopMost");
            SizeItem.Header = App.LocaleManager.GetString("Size");
            OpacityItem.Header = App.LocaleManager.GetString("Opacity");
        }

        void widget_UpdateAero(object sender, EventArgs e)
        {
            if (HTCHome.Properties.Settings.Default.EnableGlass)
            {
                WinAPI.RemoveGlassRegion(ref handle);
                WinAPI.MakeGlassRegion(ref handle, widget.GetRegion());
            }
        }

        public void Load()
        {
            widget.SetParent(this);
            UserControl w = widget.Load();
            w.SizeChanged += new SizeChangedEventHandler(w_SizeChanged);
            this.Width = w.Width;
            this.Height = w.Height;

            Storyboard loadAnim = Resources["LoadAnim"] as Storyboard;
            //((DoubleAnimation)loadAnim.Children[0]).To = widget.GetWindowPosition().Y;
            //((DoubleAnimation)loadAnim.Children[0]).From = widget.GetWindowPosition().Y + 70;

            this.Left = widget.GetWindowPosition().X;
            this.Top = widget.GetWindowPosition().Y;
            if (this.Left == -100 || this.Top == -100)
            {
                this.Left = SystemParameters.WorkArea.Width / 2 - w.Width / 2;
                this.Top = SystemParameters.WorkArea.Height / 2 - w.Height / 2 - 30;
            }
            this.Show();

            SizeSlider.Value = widget.GetScalefactor() * 100;
            OpacitySlider.Value = widget.GetOpacity() * 100;

            this.Topmost = widget.GetTopMost();
            TopMostItem.IsChecked = this.Topmost;

            PinItem.IsChecked = widget.GetPin();

            WinAPI.SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1); //наверное так делать не стоит, но зато теперь те, кто говорит "ОМГ он жрет столько памяти!!!11" могут успокоиться
            loadAnim.Begin(this);
        }

        void item_Unchecked(object sender, RoutedEventArgs e)
        {
            int index = AddWidgetPanel.Children.IndexOf((UIElement)sender);
            if (App.widgets[index].IsWidgetLoaded)
            {
                App.widgets[index].IsWidgetLoaded = false;
                App.widgets[index].Unload();
                App.widgets[index].Close();
            }
        }

        void item_Checked(object sender, RoutedEventArgs e)
        {
            int index = AddWidgetPanel.Children.IndexOf((UIElement)sender);
            if (!App.widgets[index].IsWidgetLoaded || !App.widgets[index].IsVisible)
            {
                var w = new Widget();
                w.Initalize(App.widgets[index].path);
                App.widgets[index] = w;
                App.widgets[index].Load();
            }
        }

        void w_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Width = e.NewSize.Width;
            this.Height = e.NewSize.Height;
        }

        public void Unload()
        {
            widget.SetWindowPosition(this.Left, this.Top);
            widget.Unload();
            //IsWidgetLoaded = false;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !PinItem.IsChecked)
            {
                DragMove();
                widget.SetWindowPosition(this.Left, this.Top);
            }
        }

        private void CloseItem_Click(object sender, RoutedEventArgs e)
        {
            IsWidgetLoaded = false;
            Unload();
            this.Close();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            handle = new WindowInteropHelper(this).Handle;

            WinAPI.RemoveFromAeroPeek(handle);
            WinAPI.RemoveFromAltTab(handle);
            WinAPI.RemoveFromFlip3D(handle);

            ResumeRenderRecovery.Register(this, handle);

            MainGrid.Children.Add(widget.GetWidgetControl());
            IsWidgetLoaded = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ResumeRenderRecovery.Unregister(this, handle);

            if (IsWidgetLoaded)
            {
                Unload();
            }

            int count = 0;
            foreach (Widget w in App.widgets)
            {
                if (w.IsLoaded)
                    count++;
            }
            if (count == 1)
                App.Current.Shutdown();
        }

        private void TopMostItem_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            widget.SetTopMost(true);
        }

        private void TopMostItem_Unchecked(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
            widget.SetTopMost(false);
        }

        private void SizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            double scale = 1.0f - ((SizeItem.Items.IndexOf(sender)) / 10.0f);
            widget.SetScalefactor(scale);
            foreach (MenuItem item in SizeItem.Items)
            {
                if (sender != item)
                    item.IsChecked = false;
                else
                    item.IsChecked = true;
            }

            if (HTCHome.Properties.Settings.Default.EnableGlass)
            {
                WinAPI.RemoveGlassRegion(ref handle);
                WinAPI.MakeGlassRegion(ref handle, widget.GetRegion());
            }
        }

        private void PinItem_Checked(object sender, RoutedEventArgs e)
        {
            widget.SetPin(true);
        }

        private void PinItem_Unchecked(object sender, RoutedEventArgs e)
        {
            widget.SetPin(false);
        }

        private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            /*SizeSlider.ToolTip = SizeSlider.Value.ToString();
            ((ToolTip)SizeSlider.ToolTip).IsOpen = true;*/
            if (widget != null)
            {
                widget.SetScalefactor(SizeSlider.Value / 100);
                if (HTCHome.Properties.Settings.Default.EnableGlass)
                {
                    WinAPI.RemoveGlassRegion(ref handle);
                    WinAPI.MakeGlassRegion(ref handle, widget.GetRegion());
                }
            }
        }

        private void HomeOptionsItem_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ShowOptions();
        }

        private void CloseHomeItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            var files = from x in ((string[])e.Data.GetData(DataFormats.FileDrop, true))
                        where x.EndsWith(".hhskin") || x.EndsWith(".hhext")
                        select x;
            if (files != null)
            {
                foreach (string f in files)
                {
                    try
                    {
                        App.Unpack(App.Path, f);
                        if (f.EndsWith(".hhskin"))
                            MessageBox.Show(App.LocaleManager.GetString("SkinInstalled"), "", MessageBoxButton.OK, MessageBoxImage.Information);
                        else
                            MessageBox.Show(App.LocaleManager.GetString("ExtensionInstalled"), "", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        if (f.EndsWith(".hhskin"))
                            MessageBox.Show(App.LocaleManager.GetString("SkinNotInstalledNoAccess"), "", MessageBoxButton.OK, MessageBoxImage.Error);
                        else
                            MessageBox.Show(App.LocaleManager.GetString("ExtensionNotInstalledNoAccess"), "", MessageBoxButton.OK, MessageBoxImage.Error);
                        App.Log("Can't install exntension " + f + "\n" + ex.ToString());
                    }
                    catch (Exception ex)
                    {
                        if (f.EndsWith(".hhskin"))
                            MessageBox.Show(App.LocaleManager.GetString("SkinNotInstalled"), "", MessageBoxButton.OK, MessageBoxImage.Error);
                        else
                            MessageBox.Show(App.LocaleManager.GetString("ExtensionNotInstalled"), "", MessageBoxButton.OK, MessageBoxImage.Error);
                        App.Log("Can't install exntension " + f + "\n" + ex.ToString());
                    }
                }
            }
        }

        private void Storyboard_Completed(object sender, EventArgs e)
        {
            if (HTCHome.Properties.Settings.Default.EnableGlass)
                WinAPI.MakeGlassRegion(ref handle, widget.GetRegion());
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            AddWidgetPanel.Children.Clear();
            foreach (Widget widget1 in App.widgets)
            {
                var item = new ToggleButton();
                item.ToolTip = widget1.WidgetName;
                item.Margin = new Thickness(0, 0, 5, 0);
                Image icon = new Image();
                icon.Source = new BitmapImage(new Uri(widget1.WidgetIcon));
                icon.Width = 20;
                icon.Height = 20;
                item.Content = icon;
                if (widget1.IsLoaded)
                    item.IsChecked = true;
                item.Checked += item_Checked;
                item.Unchecked += item_Unchecked;
                AddWidgetPanel.Children.Add(item);
            }
        }

        private void GalleryItemClick(object sender, RoutedEventArgs e)
        {
            if (App.Gallery != null && App.Gallery.IsVisible)
            {
                App.Gallery.Close();
                return;
            }
            App.Gallery = new Gallery.Gallery()
                              {
                                  Left = 0,
                                  Top = 0,
                                  Width = SystemParameters.PrimaryScreenWidth,
                                  Height = SystemParameters.PrimaryScreenHeight
                              };
            App.Gallery.ShowDialog();
        }

        private void OpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (widget != null)
            {
                widget.SetOpacity(OpacitySlider.Value / 100);
                MainGrid.Opacity = OpacitySlider.Value / 100;
            }
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            var mouseEnterAnim = Resources["MouseEnter"] as Storyboard;
            mouseEnterAnim.Begin(MainGrid);
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            var mouseLeaveAnim = Resources["MouseLeave"] as Storyboard;
            ((DoubleAnimation)mouseLeaveAnim.Children[0]).To = OpacitySlider.Value / 100;
            mouseLeaveAnim.Begin(MainGrid);
        }

        private void MouseEnter_Completed(object sender, EventArgs e)
        {
            MainGrid.Opacity = 1;
        }

        private void MouseLeave_Completed(object sender, EventArgs e)
        {
            var mouseLeaveAnim = Resources["MouseLeave"] as Storyboard;
            MainGrid.Opacity = (double)((DoubleAnimation)mouseLeaveAnim.Children[0]).To;
        }
    }

    // Targeted experiment for the post-hibernate WPF failure. Unlike the old
    // process-level SoftwareOnly experiment, this waits until Windows has resumed
    // and display changes have settled. If WPF is still stuck at Tier 0, it asks
    // the existing HwndTarget to rebuild itself in software mode. No window is
    // hidden, moved, recreated or restarted, so HWND/Z-order stay untouched.
    internal static class ResumeRenderRecovery
    {
        private const int WM_POWERBROADCAST = 0x0218;
        private const int PBT_APMSUSPEND = 0x0004;
        private const int PBT_APMRESUMESUSPEND = 0x0007;
        private const int PBT_APMRESUMEAUTOMATIC = 0x0012;

        private static readonly object Sync = new object();
        private static readonly List<WeakReference> Windows = new List<WeakReference>();
        private static bool started;
        private static int resumeGeneration;
        private static int attemptedGeneration;
        private static DateTime lastResumeUtc = DateTime.MinValue;
        private static DateTime lastDisplayChangeUtc = DateTime.MinValue;
        private static readonly int UpdateWindowSettingsMessage = RegisterWindowMessage("UpdateWindowSettings");
        private static readonly int NeedsRePresentOnWakeMessage = RegisterWindowMessage("NeedsRePresentOnWake");

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern uint RegisterWindowMessage(string lpString);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        public static void Register(Widget window, IntPtr hwnd)
        {
            try
            {
                HwndSource source = HwndSource.FromHwnd(hwnd);
                if (source != null)
                    source.AddHook(WindowHook);

                lock (Sync)
                {
                    Windows.Add(new WeakReference(window));
                    if (!started)
                    {
                        started = true;
                        Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
                        Microsoft.Win32.SystemEvents.DisplaySettingsChanging += SystemEvents_DisplaySettingsChanging;
                        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
                        RenderCapability.TierChanged += RenderCapability_TierChanged;
                    }
                }

                LogTarget("REGISTER", window);
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeRepair] register failed: " + ex);
            }
        }

        public static void Unregister(Widget window, IntPtr hwnd)
        {
            try
            {
                HwndSource source = HwndSource.FromHwnd(hwnd);
                if (source != null)
                    source.RemoveHook(WindowHook);
            }
            catch { }

            lock (Sync)
            {
                for (int i = Windows.Count - 1; i >= 0; i--)
                {
                    Widget item = Windows[i].Target as Widget;
                    if (!Windows[i].IsAlive || object.ReferenceEquals(item, window))
                        Windows.RemoveAt(i);
                }
            }
        }

        private static IntPtr WindowHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                if (msg == UpdateWindowSettingsMessage || msg == NeedsRePresentOnWakeMessage)
                {
                    SafeLog("[ResumeRepair] WPF_PRIVATE msg=" +
                        (msg == UpdateWindowSettingsMessage ? "UpdateWindowSettings" : "NeedsRePresentOnWake") +
                        " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                        " tier=" + (RenderCapability.Tier >> 16));
                    LogTargetForHwnd("private-message", hwnd);
                }
                else if (msg == WM_POWERBROADCAST)
                {
                    long value = wParam.ToInt64();
                    if (value == PBT_APMSUSPEND || value == PBT_APMRESUMESUSPEND || value == PBT_APMRESUMEAUTOMATIC)
                    {
                        SafeLog("[ResumeRepair] WINDOW_POWER hwnd=0x" + hwnd.ToInt64().ToString("X") +
                            " wParam=0x" + value.ToString("X"));
                        LogTargetForHwnd("window-power", hwnd);
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeRepair] hook failed: " + ex.GetType().FullName + ": " + ex.Message);
            }
            return IntPtr.Zero;
        }

        private static void SystemEvents_PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
            {
                RunOnUi(delegate
                {
                    SafeLog("[ResumeRepair] SUSPEND tier=" + (RenderCapability.Tier >> 16));
                    LogAllTargets("suspend");
                });
                return;
            }

            if (e.Mode != Microsoft.Win32.PowerModes.Resume)
                return;

            int generation;
            lock (Sync)
            {
                DateTime now = DateTime.UtcNow;
                if ((now - lastResumeUtc).TotalSeconds > 8 || resumeGeneration == 0)
                    resumeGeneration++;
                lastResumeUtc = now;
                generation = resumeGeneration;
            }

            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] RESUME generation=" + generation + " tier=" + (RenderCapability.Tier >> 16));
                LogAllTargets("resume");
                ScheduleRecoveryCheck(generation, 3000, "resume+3s");
                ScheduleRecoveryCheck(generation, 9000, "resume+9s");
                ScheduleRecoveryCheck(generation, 15000, "resume+15s");
            });
        }

        private static void SystemEvents_DisplaySettingsChanging(object sender, EventArgs e)
        {
            lock (Sync)
                lastDisplayChangeUtc = DateTime.UtcNow;
            RunOnUi(delegate
            {
                SafeLog("[ResumeRepair] DISPLAY changing tier=" + (RenderCapability.Tier >> 16));
                LogAllTargets("display-changing");
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
                SafeLog("[ResumeRepair] DISPLAY changed generation=" + generation + " tier=" + (RenderCapability.Tier >> 16));
                LogAllTargets("display-changed");
                if (generation > 0)
                    ScheduleRecoveryCheck(generation, 3500, "display+3.5s");
            });
        }

        private static void RenderCapability_TierChanged(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                int generation;
                lock (Sync)
                    generation = resumeGeneration;
                SafeLog("[ResumeRepair] TIER_CHANGED generation=" + generation + " tier=" + (RenderCapability.Tier >> 16));
                LogAllTargets("tier-changed");
                if (generation > 0)
                    ScheduleRecoveryCheck(generation, 1500, "tier-change+1.5s");
            });
        }

        private static void ScheduleRecoveryCheck(final int generation, int delayMs, string reason)
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(delayMs);
            timer.Tag = reason;
            timer.Tick += delegate(object sender, EventArgs e)
            {
                timer.Stop();
                TryRecovery(generation, reason);
            };
            timer.Start();
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
                double sinceDisplay = displayUtc == DateTime.MinValue ? 999 : (DateTime.UtcNow - displayUtc).TotalSeconds;
                int tier = RenderCapability.Tier >> 16;

                SafeLog("[ResumeRepair] CHECK reason=" + reason +
                    " generation=" + generation +
                    " tier=" + tier +
                    " sinceResume=" + sinceResume.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                    "s sinceDisplay=" + sinceDisplay.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s");

                if (tier > 0)
                {
                    SafeLog("[ResumeRepair] HEALTHY: hardware tier recovered; no intervention");
                    return;
                }

                if (sinceResume < 8 || sinceDisplay < 3)
                {
                    ScheduleRecoveryCheck(generation, 3000, "settle-retry");
                    return;
                }

                if (attempted == generation)
                    return;

                lock (Sync)
                    attemptedGeneration = generation;

                SafeLog("[ResumeRepair] ATTEMPT generation=" + generation +
                    ": Tier remained 0 after display settle; switching existing HwndTarget(s) to SoftwareOnly");
                LogAllTargets("pre-rebind");

                foreach (Widget window in GetLiveWindows())
                {
                    if (window == null || !window.IsLoaded || !window.IsVisible)
                        continue;

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
                        InvalidateRect(hwnd, IntPtr.Zero, false);
                        window.InvalidateVisual();
                        UIElement content = window.Content as UIElement;
                        if (content != null)
                            content.InvalidateVisual();

                        SafeLog("[ResumeRepair] REBIND_OK hwnd=0x" + hwnd.ToInt64().ToString("X") +
                            " renderMode=" + before + "->" + target.RenderMode +
                            " tier=" + (RenderCapability.Tier >> 16));
                        LogTarget("post-rebind", window);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        SafeLog("[ResumeRepair] REBIND_OOM hwnd=0x" + hwnd.ToInt64().ToString("X") + ": " + ex);
                        LogTarget("rebind-oom", window);
                    }
                    catch (Exception ex)
                    {
                        SafeLog("[ResumeRepair] REBIND_FAILED hwnd=0x" + hwnd.ToInt64().ToString("X") + ": " + ex);
                        LogTarget("rebind-failed", window);
                    }
                }

                ScheduleVerification(generation, 1000, "rebind+1s");
                ScheduleVerification(generation, 5000, "rebind+5s");
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeRepair] CHECK failed: " + ex);
            }
        }

        private static void ScheduleVerification(final int generation, int delayMs, string reason)
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
                SafeLog("[ResumeRepair] VERIFY reason=" + reason + " tier=" + (RenderCapability.Tier >> 16));
                LogAllTargets(reason);
            };
            timer.Start();
        }

        private static void RunOnUi(Action action)
        {
            try
            {
                Application app = Application.Current;
                if (app == null || app.Dispatcher == null)
                    return;
                app.Dispatcher.BeginInvoke(action);
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeRepair] dispatcher failed: " + ex.Message);
            }
        }

        private static List<Widget> GetLiveWindows()
        {
            List<Widget> result = new List<Widget>();
            lock (Sync)
            {
                for (int i = Windows.Count - 1; i >= 0; i--)
                {
                    Widget window = Windows[i].Target as Widget;
                    if (!Windows[i].IsAlive || window == null)
                    {
                        Windows.RemoveAt(i);
                        continue;
                    }
                    result.Add(window);
                }
            }
            return result;
        }

        private static void LogAllTargets(string reason)
        {
            foreach (Widget window in GetLiveWindows())
                LogTarget(reason, window);
        }

        private static void LogTargetForHwnd(string reason, IntPtr hwnd)
        {
            foreach (Widget window in GetLiveWindows())
            {
                try
                {
                    if (new WindowInteropHelper(window).Handle == hwnd)
                    {
                        LogTarget(reason, window);
                        return;
                    }
                }
                catch { }
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
                    SafeLog("[ResumeRepair] TARGET reason=" + reason + " hwnd=0x" + hwnd.ToInt64().ToString("X") + " target=null");
                    return;
                }

                SafeLog("[ResumeRepair] TARGET reason=" + reason +
                    " hwnd=0x" + hwnd.ToInt64().ToString("X") +
                    " tier=" + (RenderCapability.Tier >> 16) +
                    " renderMode=" + target.RenderMode +
                    " internals={" + TargetInternals(target) + "}");
            }
            catch (Exception ex)
            {
                SafeLog("[ResumeRepair] TARGET reason=" + reason + " failed: " + ex.GetType().FullName + ": " + ex.Message);
            }
        }

        private static string TargetInternals(HwndTarget target)
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

            List<string> parts = new List<string>();
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (string name in names)
            {
                try
                {
                    FieldInfo field = type.GetField(name, flags);
                    if (field == null)
                    {
                        parts.Add(name + "=<missing>");
                        continue;
                    }
                    object value = field.GetValue(target);
                    parts.Add(name + "=" + (value == null ? "null" : value.ToString()));
                }
                catch (Exception ex)
                {
                    parts.Add(name + "=<" + ex.GetType().Name + ">");
                }
            }
            return string.Join(",", parts.ToArray());
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
}
