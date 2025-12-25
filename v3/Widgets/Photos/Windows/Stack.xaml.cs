using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using Photos.Controls;

namespace Photos.Windows
{
    /// <summary>
    /// Interaction logic for Stocks.xaml
    /// </summary>
    public partial class Stack : Window
    {
        private Options optionsWindow;
        private DispatcherTimer switchTimer;

        public static UInt32 SPIF_UPDATEINIFILE = 0x1;

        public static UInt32 SPI_SETDESKWALLPAPER = 20;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern Int32 SystemParametersInfo(UInt32 uiAction, UInt32 uiParam, String pvParam, UInt32 fWinIni);

        public Stack()
        {
            InitializeComponent();
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;

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
                this.Left = SystemParameters.WorkArea.Width / 2 - 150;
                this.Top = SystemParameters.WorkArea.Height / 2 - 150;
            }

            if (App.Pics.Count > 1)
            {
                if (App.Settings.SwitchRandom)
                {
                    var r = new Random(Environment.TickCount);
                    App.Index = r.Next(App.Pics.Count);
                    PhotoStack.AddNewPhoto(true, App.Pics[App.Index]);
                    App.Index = r.Next(App.Pics.Count);
                    PhotoStack.AddNewPhoto(false, App.Pics[App.Index]);
                }
                else
                {
                    PhotoStack.AddNewPhoto(true, App.Pics[1]);
                    PhotoStack.AddNewPhoto(false, App.Pics[0]);
                    App.Index = 1;
                }
            }
            else
            {
                PhotoStack.AddNewPhoto(true);
                PhotoStack.AddNewPhoto(false);
            }

            if (App.Settings.SwitchAutomatically)
            {
                switchTimer = new DispatcherTimer();
                switchTimer.Interval = TimeSpan.FromMinutes(App.Settings.SwitchInterval);
                switchTimer.Tick += SwitchTimerTick;
                switchTimer.Start();
            }

            if (App.Settings.Debug)
                TestItem.Visibility = System.Windows.Visibility.Visible;

            this.Opacity = App.Settings.Opacity;
            //Scale.ScaleX = App.Settings.Scale;
            //Rotate.Angle = App.Settings.Angle;
            ((RotateTransform)PhotoStack.RenderTransform).Angle = App.Settings.Angle;
            this.ShowInTaskbar = !App.Settings.UseTrayIcon;

            if (App.Settings.TopMost)
            {
                this.Topmost = true;
                TopMostItem.IsChecked = true;
            }


            if (App.Settings.Pin)
                PinItem.IsChecked = true;
        }

        void SwitchTimerTick(object sender, EventArgs e)
        {
            PhotoStack.MoveForward();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //this.Width = App.Settings.Scale * App.Settings.MaxSize * 2;
            //this.Height = this.Width;
            Activate();
        }

        private void WindowMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !App.Settings.Pin && Mouse.Captured == null)
                DragMove();
        }

        private void WindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            App.Settings.Left = this.Left;
            App.Settings.Top = this.Top;
            App.Settings.Save(App.ConfigFile);
        }

        private void PinItemClick(object sender, RoutedEventArgs e)
        {
            App.Settings.Pin = PinItem.IsChecked;
            App.Settings.Save(App.ConfigFile);
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
            App.Settings.Save(App.ConfigFile);
        }

        private void OptionsItemClick(object sender, RoutedEventArgs e)
        {
            if (optionsWindow != null && optionsWindow.IsVisible)
            {
                optionsWindow.Activate();
                return;
            }

            optionsWindow = new Options();
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
            //Scale.ScaleX = App.Settings.Scale;
            PhotoStack.Resize();

            this.ShowInTaskbar = !App.Settings.UseTrayIcon;
            this.Opacity = App.Settings.Opacity;
            //Rotate.Angle = App.Settings.Angle;
            ((RotateTransform)PhotoStack.RenderTransform).Angle = App.Settings.Angle;
            //this.Width = App.Settings.Scale * App.Settings.MaxSize * 2;
            //this.Height = this.Width;
        }

        private void CloseItemClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TestItemClick(object sender, RoutedEventArgs e)
        {
            var r = new Random(Environment.TickCount);
            for (var i = 0; i < 5; i++)
            {
                var w = new PhotoWindow();
                var file = App.Pics[i];
                w.AddPicture(file);
                var left = this.Left + r.Next(-250, 250);
                var top = this.Top + r.Next(-250, 250);
                w.Left = this.Left;
                w.Top = this.Top;
                w.Show();
            }
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

        private void SetWallpaperItemClick(object sender, RoutedEventArgs e)
        {
            var path = PhotoStack.CurrentPicturePath;
            if (!string.IsNullOrEmpty(path))
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE);
        }

        //private double x;
        //Vector mouseDownVector;
        //Point arrowCenterPoint = new Point();
        //private double mouseDownAngle;
        //private void PhotoStackMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    arrowCenterPoint = new Point(ActualWidth / 2, ActualHeight / 2);
        //    var mouseDownPoint = e.GetPosition(this);
        //    mouseDownVector = mouseDownPoint - arrowCenterPoint;
        //    mouseDownAngle = Rotate.Angle;
        //    Mouse.Capture(PhotoStack);
        //    x = e.MouseDevice.GetPosition(PhotoStack).X;
        //}

        //private void PhotoStackMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    Mouse.Capture(null);
        //}

        //private void PhotoStackMouseMove(object sender, MouseEventArgs e)
        //{
        //    if (e.LeftButton == MouseButtonState.Pressed && Mouse.Captured == PhotoStack)
        //    {
        //        Point curPos = e.GetPosition(this);
        //        Vector currentVector = curPos - arrowCenterPoint;
        //        Rotate.Angle = Vector.AngleBetween(mouseDownVector, currentVector) + mouseDownAngle;
        //    }
        //}
    }
}
