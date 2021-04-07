using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Home.Base;

namespace Clock.Windows
{
    /// <summary>
    /// Interaction logic for AnalogClock.xaml
    /// </summary>
    public partial class AnalogClockBig : Window
    {
        private IntPtr handle;
        private DispatcherTimer clockTimer;
        private TimeSpan baseUtcOffset;
        private AnalogClockOptions optionsWindow;

        private bool isNight;
        public bool IsNight
        {
            get { return isNight; }
            set
            {
                if (value)
                {
                    Bg.Source = new BitmapImage(new Uri("/Resources/AnalogClockBig/clock_base_night.png", UriKind.Relative));
                    Day.Foreground = (SolidColorBrush)Resources["NightFontColor"];
                    Month.Foreground = (SolidColorBrush) Resources["NightFontColor"];
                }
                else
                {
                    Bg.Source = new BitmapImage(new Uri("/Resources/AnalogClockBig/clock_base_day.png", UriKind.Relative));
                    Day.Foreground = (SolidColorBrush)Resources["DayFontColor"];
                    Month.Foreground = (SolidColorBrush)Resources["DayFontColor"];
                }
                isNight = value;
            }
        }

        public AnalogClockBig()
        {
            InitializeComponent();
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            handle = new WindowInteropHelper(this).Handle;

            if (!App.Settings.DisableUnminimizer)
            {
                var u = new Unminimizer();
                u.Initialize(handle);
            }

            Dwm.RemoveFromAeroPeek(handle);
            Dwm.RemoveFromAltTab(handle);
            Dwm.RemoveFromFlip3D(handle);

            this.Left = App.Settings.Left;
            this.Top = App.Settings.Top;

            if (this.Left == -100.0f || this.Top == -100.0f)
            {
                this.Left = SystemParameters.WorkArea.Width / 2 - this.Width / 2;
                this.Top = SystemParameters.WorkArea.Height / 2 - this.Height / 2;
            }

            this.Opacity = App.Settings.Opacity;

            if (App.Settings.ShowClockName)
            {
                ClockNameGrid.Visibility = System.Windows.Visibility.Visible;
                ClockNameTextBlock.Text = App.Settings.ClockName;
            }
            else
                ClockNameGrid.Visibility = System.Windows.Visibility.Collapsed;

            Scale.ScaleX = App.Settings.Scale;

            if (App.Settings.UseAero)
            {
                UpdateAero();
            }

            if (App.Settings.TopMost)
            {
                this.Topmost = true;
                TopMostItem.IsChecked = true;
            }

            if (App.Settings.Pin)
                PinItem.IsChecked = true;

            this.ShowInTaskbar = !App.Settings.UseTrayIcon;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            Activate();

            if (App.Settings.UseCustomTime)
            {
                baseUtcOffset = TimeZoneInfo.FindSystemTimeZoneById(App.Settings.FirstTimeZoneId).GetUtcOffset(DateTime.Now);
            }
            else
            {
                baseUtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
            }

            if (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour > 5 && DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour < 18 && IsNight)
            {
                IsNight = false;
            }
            else if ((DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour <= 5 || DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour >= 18) && !IsNight)
            {
                IsNight = true;
            }

            var s = (Storyboard)Resources["LoadAnim"];
            HourRotate.Angle = (App.Settings.LastHour * 30) + (App.Settings.LastMinute * 0.5);
            MinuteRotate.Angle = App.Settings.LastMinute * 6;
            SecondRotate.Angle = App.Settings.LastSecond * 6;
            ((DoubleAnimation)s.Children[0]).To = (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute * 0.5);
            ((DoubleAnimation)s.Children[1]).To = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute * 6;
            ((DoubleAnimation)s.Children[2]).To = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Second * 6;
            s.Begin();

            Day.Text = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).ToString("ddd");
            Month.Text = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).ToString("MMM").ToUpper() + " " + DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Day.ToString();

            if (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour < 12)
                AmPm.Text = "AM";
            else
                AmPm.Text = "PM";
        }

        void ClockTimerTick(object sender, EventArgs e)
        {
            HourRotate.Angle = (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute * 0.5);
            MinuteRotate.Angle = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute * 6;
            SecondRotate.Angle = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Second * 6;

            Day.Text = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).ToString("ddd");
            Month.Text = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).ToString("MMM").ToUpper() + " " + DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Day.ToString();

            if (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour < 12)
                AmPm.Text = "AM";
            else
                AmPm.Text = "PM";

            if (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour > 5 && DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour < 18 && IsNight)
            {
                IsNight = false;
            }
            else if ((DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour <= 5 || DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour >= 18) && !IsNight)
            {
                IsNight = true;
            }
        }

        private void WindowMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !App.Settings.Pin)
            {
                DragMove();
            }
        }

        private void WindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            App.Settings.Left = this.Left;
            App.Settings.Top = this.Top;
            App.Settings.Save(E.Root + "\\Clock.AnalogClock.config");
        }

        private void LoadAnimCompleted(object sender, EventArgs e)
        {
            clockTimer = new DispatcherTimer();
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Tick += ClockTimerTick;
            clockTimer.Start();

            HourRotate.Angle = (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute * 0.5);
            MinuteRotate.Angle = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute * 6;
            SecondRotate.Angle = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Second * 6;
        }

        private void CloseItemClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OptionsItemClick(object sender, RoutedEventArgs e)
        {
            if (optionsWindow != null && optionsWindow.IsVisible)
            {
                optionsWindow.Activate();
                return;
            }

            optionsWindow = new AnalogClockOptions();
            optionsWindow.UpdateSettings += OptionsWindowUpdateSettings;

            optionsWindow.Width = App.Settings.OptionsWidth;
            optionsWindow.Height = App.Settings.OptionsHeight;

            if (App.Settings.Language == "he-IL" || App.Settings.Language == "ar-SA")
            {
                optionsWindow.FlowDirection = System.Windows.FlowDirection.RightToLeft;
            }
            else
            {
                optionsWindow.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            }

            optionsWindow.ShowDialog();
        }

        void OptionsWindowUpdateSettings(object sender, EventArgs e)
        {
            if (App.Settings.UseCustomTime)
            {
                baseUtcOffset = TimeZoneInfo.FindSystemTimeZoneById(App.Settings.FirstTimeZoneId).GetUtcOffset(DateTime.Now);
            }
            else
            {
                baseUtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
            }

            var updateTime = (bool) sender;
            if (updateTime)
            {
                clockTimer.Tick -= ClockTimerTick;
                clockTimer.Stop();
                clockTimer = null;

                var s = (Storyboard)Resources["LoadAnim"];
                ((DoubleAnimation)s.Children[0]).To = (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute * 0.5);
                ((DoubleAnimation)s.Children[1]).To = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute * 6;
                ((DoubleAnimation)s.Children[2]).To = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Second * 6;
                s.Begin();
            }

            if (App.Settings.ShowClockName)
            {
                ClockNameGrid.Visibility = System.Windows.Visibility.Visible;
                ClockNameTextBlock.Text = App.Settings.ClockName;
            }
            else
                ClockNameGrid.Visibility = System.Windows.Visibility.Collapsed;

            Scale.ScaleX = App.Settings.Scale;
            
            if (App.Settings.UseAero)
                UpdateAero();
            else
            {
                Dwm.RemoveGlassRegion(ref handle);
            }

            this.ShowInTaskbar = !App.Settings.UseTrayIcon;
            this.Opacity = App.Settings.Opacity;
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            App.Settings.LastHour = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Hour;
            App.Settings.LastMinute = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Minute;
            App.Settings.LastSecond = DateTime.Now.ToUniversalTime().Add(baseUtcOffset).Second;
            App.Settings.Save(App.ConfigFile);
        }

        private void UpdateAero()
        {
            double dpiX = 0.0f;
            double dpiY = 0.0f;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            var rgn = IntPtr.Zero;
            if (App.Settings.ShowClockName)
                rgn = WinAPI.CreateEllipticRgn((int)(20 * App.Settings.Scale * dpiX), 0, (int)((this.Width - 20) * App.Settings.Scale * dpiX), (int)((this.Height - 40) * App.Settings.Scale * dpiY));
            else
                rgn = WinAPI.CreateEllipticRgn(0, 0, (int)(this.Width * App.Settings.Scale * dpiX), (int)(this.Height * App.Settings.Scale * dpiY));
            Dwm.MakeGlassRegion(ref handle, rgn);
        }

        private void TopMostItemClick(object sender, RoutedEventArgs e)
        {
            if (App.Settings.TopMost)
            {
                App.Settings.TopMost = false;
                this.Topmost = false;
            }
            else
            {
                App.Settings.TopMost = true;
                this.Topmost = true;
            }
        }

        private void PinItemClick(object sender, RoutedEventArgs e)
        {
            App.Settings.Pin = PinItem.IsChecked;
            App.Settings.Save(App.ConfigFile);
        }

        private void MouseEnterCompleted(object sender, EventArgs e)
        {
            this.Opacity = 1;
        }

        private void MouseLeaveCompleted(object sender, EventArgs e)
        {
            this.Opacity = App.Settings.Opacity;
        }

        private void ThisMouseEnter(object sender, MouseEventArgs e)
        {
            var mouseEnterAnim = (Storyboard)Resources["MouseEnter"];
            mouseEnterAnim.Begin(this);
        }

        private void ThisMouseLeave(object sender, MouseEventArgs e)
        {
            var mouseLeaveAnim = (Storyboard)Resources["MouseLeave"];
            ((DoubleAnimation)mouseLeaveAnim.Children[0]).To = App.Settings.Opacity;
            mouseLeaveAnim.Begin(this);
        }
    }
}
