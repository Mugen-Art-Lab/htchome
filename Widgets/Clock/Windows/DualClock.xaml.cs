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
    public partial class DualClock : Window
    {
        private IntPtr handle;
        private DispatcherTimer clockTimer;
        private TimeSpan firstClockBaseUtcOffset;
        private TimeSpan secondClockBaseUtcOffset;
        private DualClockOptions optionsWindow;

        private bool firstClockIsNight;
        public bool FirstClockIsNight
        {
            get { return firstClockIsNight; }
            set
            {
                if (value)
                {
                    FirstClockBg.Source = new BitmapImage(new Uri("/Resources/DualClock/clock_base_night.png", UriKind.Relative));
                }
                else
                {
                    FirstClockBg.Source = new BitmapImage(new Uri("/Resources/DualClock/clock_base_day.png", UriKind.Relative));
                }
                firstClockIsNight = value;
            }
        }

        private bool secondClockIsNight;
        public bool SecondClockIsNight
        {
            get { return secondClockIsNight; }
            set
            {
                if (value)
                {
                    SecondClockBg.Source = new BitmapImage(new Uri("/Resources/DualClock/clock_base_night.png", UriKind.Relative));
                }
                else
                {
                    SecondClockBg.Source = new BitmapImage(new Uri("/Resources/DualClock/clock_base_day.png", UriKind.Relative));
                }
                secondClockIsNight = value;
            }
        }

        public DualClock()
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

            FirstClockNameTextBlock.Text = App.Settings.FirstClockName;
            SecondClockNameTextBlock.Text = App.Settings.SecondClockName;

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

            firstClockBaseUtcOffset = TimeZoneInfo.FindSystemTimeZoneById(App.Settings.FirstTimeZoneId).GetUtcOffset(DateTime.Now);
            secondClockBaseUtcOffset = TimeZoneInfo.FindSystemTimeZoneById(App.Settings.SecondTimeZoneId).GetUtcOffset(DateTime.Now);

            if (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour > 5 && DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour < 18 && FirstClockIsNight)
                FirstClockIsNight = false;
            else if ((DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour <= 5 || DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour >= 18) && !FirstClockIsNight)
                FirstClockIsNight = true;

            if (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour > 5 && DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour < 18 && SecondClockIsNight)
                SecondClockIsNight = false;
            else if ((DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour <= 5 || DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour >= 18) && !SecondClockIsNight)
                SecondClockIsNight = true;

            var s = (Storyboard)Resources["FirstClockLoadAnim"];
            ((DoubleAnimation)s.Children[0]).To = (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Minute * 0.5);
            ((DoubleAnimation)s.Children[1]).To = DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Minute * 6;
            ((DoubleAnimation)s.Children[2]).To = DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Second * 6;
            s.Begin();

            var s1 = (Storyboard)Resources["SecondClockLoadAnim"];
            ((DoubleAnimation)s1.Children[0]).To = (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Minute * 0.5);
            ((DoubleAnimation)s1.Children[1]).To = DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Minute * 6;
            ((DoubleAnimation)s1.Children[2]).To = DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Second * 6;
            s1.Begin();
        }

        void ClockTimerTick(object sender, EventArgs e)
        {
            FirstClockHourRotate.Angle = (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Minute * 0.5);
            FirstClockMinuteRotate.Angle = DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Minute * 6;
            FirstClockSecondRotate.Angle = DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Second * 6;

            SecondClockHourRotate.Angle = (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Minute * 0.5);
            SecondClockMinuteRotate.Angle = DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Minute * 6;
            SecondClockSecondRotate.Angle = DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Second * 6;

            if (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour > 5 && DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour < 18 && FirstClockIsNight)
                FirstClockIsNight = false;
            else if ((DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour <= 5 || DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour >= 18) && !FirstClockIsNight)
                FirstClockIsNight = true;

            if (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour > 5 && DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour < 18 && SecondClockIsNight)
                SecondClockIsNight = false;
            else if ((DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour <= 5 || DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour >= 18) && !SecondClockIsNight)
                SecondClockIsNight = true;
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
            App.Settings.Save(App.ConfigFile);
        }

        private void FirstClockLoadAnimCompleted(object sender, EventArgs e)
        {
            clockTimer = new DispatcherTimer();
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Tick += ClockTimerTick;
            clockTimer.Start();

            FirstClockHourRotate.Angle = (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Minute * 0.5);
            FirstClockMinuteRotate.Angle = DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Minute * 6;
            FirstClockSecondRotate.Angle = DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Second * 6;
        }

        private void SecondClockLoadAnimCompleted(object sender, EventArgs e)
        {
            SecondClockHourRotate.Angle = (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Minute * 0.5);
            SecondClockMinuteRotate.Angle = DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Minute * 6;
            SecondClockSecondRotate.Angle = DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Second * 6;
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

            optionsWindow = new DualClockOptions();
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
            firstClockBaseUtcOffset = TimeZoneInfo.FindSystemTimeZoneById(App.Settings.FirstTimeZoneId).GetUtcOffset(DateTime.Now);
            secondClockBaseUtcOffset = TimeZoneInfo.FindSystemTimeZoneById(App.Settings.SecondTimeZoneId).GetUtcOffset(DateTime.Now);

            var updateTime = (bool)sender;
            if (updateTime)
            {
                clockTimer.Tick -= ClockTimerTick;
                clockTimer.Stop();
                clockTimer = null;

                var s = (Storyboard)Resources["FirstClockLoadAnim"];
                ((DoubleAnimation)s.Children[0]).To = (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Minute * 0.5);
                ((DoubleAnimation)s.Children[1]).To = DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Minute * 6;
                ((DoubleAnimation)s.Children[2]).To = DateTime.Now.ToUniversalTime().Add(firstClockBaseUtcOffset).Second * 6;
                s.Begin();

                var s1 = (Storyboard)Resources["SecondClockLoadAnim"];
                ((DoubleAnimation)s1.Children[0]).To = (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Hour * 30) + (DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Minute * 0.5);
                ((DoubleAnimation)s1.Children[1]).To = DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Minute * 6;
                ((DoubleAnimation)s1.Children[2]).To = DateTime.Now.ToUniversalTime().Add(secondClockBaseUtcOffset).Second * 6;
                s1.Begin();
            }

            Scale.ScaleX = App.Settings.Scale;

            if (App.Settings.UseAero)
                UpdateAero();
            else
            {
                Dwm.RemoveGlassRegion(ref handle);
            }

            FirstClockNameTextBlock.Text = App.Settings.FirstClockName;
            SecondClockNameTextBlock.Text = App.Settings.SecondClockName;

            this.ShowInTaskbar = !App.Settings.UseTrayIcon;
            this.Opacity = App.Settings.Opacity;
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
            rgn = WinAPI.CreateRoundRectRgn((int)(38 * App.Settings.Scale * dpiX), (int)(12 * App.Settings.Scale * dpiY), 
                (int)((this.Width - 36) * App.Settings.Scale * dpiX), (int)((this.Height - 40) * App.Settings.Scale * dpiY), (int)(12 * App.Settings.Scale * dpiX), (int)(12 * App.Settings.Scale * dpiY));
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
