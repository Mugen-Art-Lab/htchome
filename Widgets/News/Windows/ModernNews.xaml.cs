using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Argotic.Syndication;
using Home.Base;
using News.Controls;

namespace News.Windows
{
    /// <summary>
    /// Interaction logic for ClassicNews.xaml
    /// </summary>
    public partial class ModernNews : Window
    {
        private Options optionsWindow;
        private IntPtr handle;
        private DispatcherTimer timer;

        public ModernNews()
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
                this.Left = SystemParameters.WorkArea.Width / 2 - 150;
                this.Top = SystemParameters.WorkArea.Height / 2 - 150;
            }

            this.Opacity = App.Settings.Opacity;
            this.ShowInTaskbar = !App.Settings.UseTrayIcon;
            Scale.ScaleX = App.Settings.Scale;

            if (App.Settings.TopMost)
            {
                this.Topmost = true;
                TopMostItem.IsChecked = true;
            }


            if (App.Settings.Pin)
                PinItem.IsChecked = true;

            if (App.Settings.UseAero)
            {
                UpdateAero();
            }

            App.NewsLine.RefreshFinished += NewsLineRefreshFinished;

            foreach (var feed in App.Settings.Feeds)
            {
                App.NewsLine.AddSource(feed);
            }

            RefreshTime.Text = DateTime.Now.ToShortTimeString();
            if (App.Settings.Feeds.Count > 0)
                Refresh();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(App.Settings.NewsInterval);
            timer.Tick += TimerTick;
            timer.Start();
        }

        void TimerTick(object sender, EventArgs e)
        {
            Refresh();
        }

        void NewsLineRefreshFinished(IEnumerable<Domain.FeedItem> newItems)
        {
            var index = newItems.Count();
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                                    {
                                        //NewsList.Items.Clear();
                                        //foreach (var feed in App.NewsLine.Feeds)
                                        //{
                                        foreach (var rssItem in newItems/*feed.Items*/)
                                        {
                                            var feedControl = new ModernFeedItem();
                                            feedControl.OriginalContent = rssItem.Description;
                                            feedControl.Title = HttpUtility.HtmlDecode(rssItem.Title);
                                            feedControl.Description = Utilities.StripTags(rssItem.Description);
                                            feedControl.ChannelName = rssItem.Channel;//feed.Title;
                                            feedControl.Order = index;
                                            feedControl.Url = rssItem.Url;
                                            if (rssItem.PublicationDate.ToLocalTime().Day == DateTime.Now.Day)
                                                feedControl.DateBox.Text = rssItem.PublicationDate.ToLocalTime().ToShortTimeString();
                                            else
                                                feedControl.DateBox.Text =
                                                    rssItem.PublicationDate.ToLocalTime().ToString("d MMMM hh:mm tt");
                                            var picUrl = Utilities.GetFeedPciture(rssItem.Description);
                                            if (!string.IsNullOrEmpty(picUrl) && picUrl.StartsWith("http://") && (picUrl.EndsWith(".jpg") || picUrl.EndsWith(".png")))
                                            {
                                                feedControl.Picture = picUrl;

                                            }
                                            feedControl.CompactMode = App.Settings.CompactMode;
                                            NewsList.Items.Insert(0, feedControl);
                                            while (NewsList.Items.Count > App.Settings.MaxItems)
                                            {
                                                NewsList.Items.RemoveAt(NewsList.Items.Count - 1);
                                            }
                                            index--;
                                        }
                                        //}
                                        RefreshTime.Text = DateTime.Now.ToShortTimeString();
                                        App.UnreadNewsCount += newItems.Count();
                                    });
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
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

        private void MouseEnterCompleted(object sender, EventArgs e)
        {
            this.Opacity = 1;
        }

        private void MouseLeaveCompleted(object sender, EventArgs e)
        {
            this.Opacity = App.Settings.Opacity;
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
            this.ShowInTaskbar = !App.Settings.UseTrayIcon;
            this.Opacity = App.Settings.Opacity;
            Scale.ScaleX = App.Settings.Scale;

            if (App.Settings.UseAero)
            {
                UpdateAero();
            }

            timer.Stop();
            timer.Interval = TimeSpan.FromMinutes(App.Settings.NewsInterval);
            timer.Start();

            foreach (ModernFeedItem item in NewsList.Items)
            {
                item.CompactMode = App.Settings.CompactMode;
            }
        }

        private void CloseItemClick(object sender, RoutedEventArgs e)
        {
            Close();
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

            var rgn = WinAPI.CreateRoundRectRgn(0, (int)(15 * App.Settings.Scale * dpiY), (int)(this.Width * App.Settings.Scale * dpiX),
                (int)(this.Height * App.Settings.Scale * dpiY), (int)(12 * App.Settings.Scale * dpiX), (int)(dpiY * App.Settings.Scale * 12));
            Dwm.MakeGlassRegion(ref handle, rgn);
        }

        private void RefreshItemClick(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void ClearItemClick(object sender, RoutedEventArgs e)
        {
            NewsList.Items.Clear();
            App.NewsLine.Clear();
            App.UnreadNewsCount = 0;
        }

        private void RefreshStatusPanelMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            App.NewsLine.Refresh();
            RefreshTime.Text = Properties.Resources.Refreshing;
        }
    }
}
