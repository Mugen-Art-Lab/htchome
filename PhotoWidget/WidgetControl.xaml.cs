using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using E = HTCHome.Core.Environment;

namespace PhotoWidget
{
    /// <summary>
    /// Interaction logic for WidgetControl.xaml
    /// </summary>
    public partial class WidgetControl : UserControl
    {
        private List<string> pics;
        public Random r;
        private bool switching;
        private DispatcherTimer _timer;
        private Options options;

        public WidgetControl()
        {
            InitializeComponent();
        }

        public void Load()
        {
            var nextItem = new MenuItem { Header = Widget.Instance.LocaleManager.GetString("Next") };
            nextItem.Click += NextItemClick;

            var optionsItem = new MenuItem();
            optionsItem.Header = Widget.Instance.LocaleManager.GetString("Options");
            optionsItem.Click += OptionsItemClick;

            Widget.Instance.Parent.ContextMenu.Items.Insert(0, new Separator());
            Widget.Instance.Parent.ContextMenu.Items.Insert(0, optionsItem);
            Widget.Instance.Parent.ContextMenu.Items.Insert(0, nextItem);

            pics = new List<string>();
            Scan();
            Frame.FadeCompleted += new EventHandler(FrameFadeCompleted);
            BgFrame.FadeCompleted += new EventHandler(BgFrame_FadeCompleted);

            r = new Random(Environment.TickCount);

            if (pics.Count > 0)
            {
                var i = r.Next(0, pics.Count);
                if (File.Exists(pics[i]))
                    BgFrame.Source = new BitmapImage(new Uri(pics[i]));
                i = r.Next(0, pics.Count);
                if (File.Exists(pics[i]))
                    Frame.Source = new BitmapImage(new Uri(pics[i]));
            }
            else
            {
                if (Widget.Instance.Sett.Mode == 0)
                {
                    BgFrame.Source =
                        new BitmapImage(new Uri("/Photo;component/Resources/preview_landscape.png", UriKind.Relative));
                    Frame.Source =
                        new BitmapImage(new Uri("/Photo;component/Resources/preview_landscape", UriKind.Relative));
                }
                else
                {
                    BgFrame.Source =
                        new BitmapImage(new Uri("/Photo;component/Resources/preview_portrait.png", UriKind.Relative));
                    Frame.Source =
                        new BitmapImage(new Uri("/Photo;component/Resources/preview_portrait", UriKind.Relative));
                }
            }

            options = new Options();

            if (Widget.Instance.Sett.SwitchAutomatically)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMinutes(Widget.Instance.Sett.Interval);
                _timer.Tick += new EventHandler(TimerTick);
                _timer.Start();
            }
        }

        void BgFrame_FadeCompleted(object sender, EventArgs e)
        {
            if (pics.Count > 0)
            {
                var i = r.Next(0, pics.Count);
                if (File.Exists(pics[i]))
                    BgFrame.Source = new BitmapImage(new Uri(pics[i]));
            }
            else
            {
                if (Widget.Instance.Sett.Mode == 0)
                {
                    BgFrame.Source =
                        new BitmapImage(new Uri("/Photo;component/Resources/preview_landscape.png", UriKind.Relative));
                }
                else
                {
                    BgFrame.Source =
                        new BitmapImage(new Uri("/Photo;component/Resources/preview_portrait.png", UriKind.Relative));
                }
            }
        }

        public void UpdateSettings(bool rescan)
        {
            if (_timer != null)
                _timer.Stop();

            if (Widget.Instance.Sett.SwitchAutomatically)
            {

                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMinutes(Widget.Instance.Sett.Interval);
                _timer.Tick += new EventHandler(TimerTick);
                _timer.Start();
            }

            if (rescan)
                Scan();
        }

        private void Scan()
        {
            pics.Clear();
            if (!string.IsNullOrEmpty(Widget.Instance.Sett.PicsPath) && Directory.Exists(Widget.Instance.Sett.PicsPath))
            {
                foreach (var f in Directory.GetFiles(Widget.Instance.Sett.PicsPath, "*.jpg", SearchOption.AllDirectories))
                    pics.Add(f);

                foreach (var f in Directory.GetFiles(Widget.Instance.Sett.PicsPath, "*.png", SearchOption.AllDirectories))
                    pics.Add(f);
            }
        }

        void TimerTick(object sender, EventArgs e)
        {
            SwitchPhotos();
        }

        void OptionsItemClick(object sender, RoutedEventArgs e)
        {
            if (options.IsVisible)
            {
                options.Activate();
                return;
            }
            options = new Options();

            if (E.Locale == "he-IL" || E.Locale == "ar-SA")
            {
                options.FlowDirection = FlowDirection.RightToLeft;
            }
            else
            {
                options.FlowDirection = FlowDirection.LeftToRight;
            }

            options.ShowDialog();
        }

        void FrameFadeCompleted(object sender, EventArgs e)
        {
            Frame.Source = BgFrame.Source;
            switching = false;
        }

        void NextItemClick(object sender, RoutedEventArgs e)
        {
            SwitchPhotos();
        }

        private void SwitchPhotos()
        {
            switching = true;
            Frame.FadeOut();
            BgFrame.FadeIn();
        }

        private void UserControlMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0 && !switching)
                SwitchPhotos();
        }
    }
}
