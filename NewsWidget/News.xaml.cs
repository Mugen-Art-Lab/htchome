using System;
using System.Collections.Generic;
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
using E = HTCHome.Core.Environment;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Threading;
using HTCHome.Core;
using System.Windows.Media.Animation;
using System.Text.RegularExpressions;
using System.Web;

namespace NewsWidget
{
    /// <summary>
    /// Interaction logic for News.xaml
    /// </summary>
    public partial class News : UserControl
    {
        private DispatcherTimer timer;
        private DispatcherTimer scrollTimer;
        private DispatcherTimer scrollTimer2;

        //this is unfinished
        private double scrollOffset = 0;
        private const double scrollBy = 48;
        private double scrollTo = 0;
        private double currentScrollPosition = 0;
        private double scrollStep = 2;
        private int direction = 0;
        ///////////////////////

        private DateTime lastNewTime;
        private string lastSource;

        private Options options;

        public List<Source> sources;

        private LastFeedsData lastFeedsData;

        public News()
        {
            InitializeComponent();
        }

        private void Initialize()
        {
            Body.Source = new BitmapImage(new Uri(E.Path + "\\News\\Resources\\body.png"));
            Header.Source = new BitmapImage(new Uri(E.Path + "\\News\\Resources\\header.png"));
            Footer.Source = new BitmapImage(new Uri(E.Path + "\\News\\Resources\\footer.png"));
            NewsPaperIcon.Source = new BitmapImage(new Uri(E.Path + "\\News\\Resources\\newspaper.png"));
            RefreshIcon.Source = new BitmapImage(new Uri(E.Path + "\\News\\Resources\\refresh.png"));
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Initialize();

            sources = new List<Source>();

            if (Widget.Sett.source != null)
            {
                foreach (string s in Widget.Sett.source)
                {
                    Source source = new Source();
                    source.Url = s;
                    source.GetNewsFinished += new EventHandler(source_GetNewsFinished);
                    sources.Add(source);
                }
            }

            lastFeedsData = LastFeedsData.Read(E.Path + "\\News\\LastFeeds.data");

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(Widget.Sett.interval);
            timer.Tick += new EventHandler(timer_Tick);
            timer.Start();

            BgRect.Opacity = Widget.Sett.opacity;
            LoadLastFeeds();
            //timer_Tick(null, EventArgs.Empty);

            Scale.ScaleX = Widget.Sett.scaleFactor;

            MenuItem optionsItem = new MenuItem();
            optionsItem.Header = Widget.LocaleManager.GetString("Options");
            optionsItem.Click += new RoutedEventHandler(optionsItem_Click);
            Widget.Parent.ContextMenu.Items.Insert(0, optionsItem);

            MenuItem clearItem = new MenuItem();
            clearItem.Header = Widget.LocaleManager.GetString("Clear");
            clearItem.Click += new RoutedEventHandler(clearItem_Click);
            Widget.Parent.ContextMenu.Items.Insert(0, clearItem);

            options = new Options(this);

            NewsText.Text = Widget.LocaleManager.GetString("News");
        }

        void clearItem_Click(object sender, RoutedEventArgs e)
        {
            NewsPanel.Children.Clear();
        }

        private void LoadLastFeeds()
        {
            if (lastFeedsData != null && lastFeedsData.sources != null)
            {
                foreach (Source s in lastFeedsData.sources)
                {
                    s.lastFeedDate = new DateTime();
                    source_GetNewsFinished(s, EventArgs.Empty);
                    if (s.feeds != null && s.feeds.Count > 0)
                    {
                        Source first = sources.Find(x => x.Url.Equals(s.Url));
                        if (first != null)
                        {
                            first.lastFeedDate = s.feeds[0].PubDate;
                        }
                    }
                }

                lastFeedsData.sources.Clear();
            }

            timer_Tick(null, EventArgs.Empty);
        }

        public void source_GetNewsFinished(object sender, EventArgs e)
        {
            int count = 1;
            foreach (Feed f in ((Source)sender).feeds)
            {
                if (DateTime.Compare(f.PubDate, ((Source)sender).lastFeedDate) == 1) //if item's date later than last new, add item to panel 
                {
                    NewsPanel.Dispatcher.Invoke((Action)delegate
                    {
                        NewsItem item = new NewsItem();
                        item.Header.Text = f.Title;
                        item.Text = f.Description;
                        item.Link = f.Link;
                        item.feed = f;
                        item.Source = ((Source)sender).Url;
                        item.MouseLeftButtonDown += new MouseButtonEventHandler(ContentText_MouseLeftButtonDown);
                        item.MouseLeftButtonUp += new MouseButtonEventHandler(item_MouseLeftButtonUp);
                        if (f.PubDate.Day < DateTime.Now.Day || f.PubDate.Month < DateTime.Now.Month)
                            item.Footer.Text = f.PubDate.ToShortDateString() + " " + f.PubDate.ToShortTimeString() + " from " + ((Source)sender).Title;
                        else
                            item.Footer.Text = f.PubDate.ToShortTimeString() + " from " + ((Source)sender).Title;

                        if (f.IsReaded)
                        {
                            item.UnreadMarker.Visibility = Visibility.Collapsed;
                            item.Header.Foreground = item.Footer.Foreground;
                        }

                        if (f.Description.Contains("<img"))
                        {
                            item.ImageSource = GetImageSource(f.Description);
                        }


                        NewsPanel.Children.Add(item);
                        if (NewsPanel.Children.Count > Widget.Sett.maxItemsCount)
                        {
                            NewsPanel.Children.RemoveAt(NewsPanel.Children.Count - 1);
                            //((Source)sender).feeds.RemoveAt(((Source)sender).feeds.Count - 1);
                        }
                    }, null);
                }

                count++;

                if (count > Widget.Sett.newsCount)
                    break;
            }

            while (((Source)sender).feeds.Count > Widget.Sett.maxItemsCount)
            {
                ((Source)sender).feeds.RemoveAt(((Source)sender).feeds.Count - 1);
            }

            if (((Source)sender).feeds != null && ((Source)sender).feeds.Count > 0)
                ((Source)sender).lastFeedDate = ((Source)sender).feeds[0].PubDate;

            ((Storyboard)RefreshIcon.Resources["RefreshAnim"]).Stop();
        }

        void optionsItem_Click(object sender, RoutedEventArgs e)
        {
            if (options.IsVisible)
            {
                options.Activate();
                return;
            }
            options = new Options(this);

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


        public void UpdateSettings()
        {
            BgRect.Opacity = Widget.Sett.opacity;

            if (Widget.Sett.interval != timer.Interval.Minutes)
            {
                timer.Stop();
                timer.Interval = TimeSpan.FromMinutes(Widget.Sett.interval);
                timer.Start();
            }

            /*if (Widget.Sett.source != lastSource)
            {
                lastNewTime = new DateTime();
                NewsPanel.Children.Clear();
            }*/
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            /*foreach (string s in Widget.Sett.source)
            {
                ThreadStart threadStarter = delegate { GetNews(s); };
                var thread = new Thread(threadStarter);
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            ((Storyboard)RefreshIcon.Resources["RefreshAnim"]).Begin();
            RefreshTime.Text = DateTime.Now.ToShortTimeString();*/
            foreach (Source s in sources)
                s.GetNews();
            ((Storyboard)RefreshIcon.Resources["RefreshAnim"]).Begin();
            RefreshTime.Text = DateTime.Now.ToShortTimeString();
        }

        private double lastY;
        void item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //RefreshTime.Text = e.GetPosition(this).Y.ToString();
            if (e.GetPosition(this).Y == lastY)
            {
                ((NewsItem)sender).UnreadMarker.Visibility = System.Windows.Visibility.Collapsed;
                ((NewsItem)sender).Header.Foreground = ((NewsItem)sender).Footer.Foreground;

                ((NewsItem)sender).feed.IsReaded = true;

                var p = new NewsItem()
                {
                    Header =
                    {
                        Text = ((NewsItem)sender).Header.Text
                    },
                    Text = ((NewsItem)sender).Text,
                    Footer =
                    {
                        Text = ((NewsItem)sender).Footer.Text
                    },
                    ImageSource = ((NewsItem)sender).ImageSource,
                    Link = ((NewsItem)sender).Link
                };

                p.HeaderIcon.Visibility = System.Windows.Visibility.Collapsed;
                p.UnreadMarker.Visibility = System.Windows.Visibility.Collapsed;
                p.ContentDocument.FontSize = Widget.Sett.previewFontSize;
                p.ContentPanel.Visibility = System.Windows.Visibility.Visible;
                p.ContentPanel.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(ContentPanel_PreviewMouseLeftButtonDown);
                p.ContentPanel.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ContentPanel_PreviewMouseLeftButtonUp);

                PreviewPanel.Children.Clear();
                PreviewPanel.Children.Add(p);

                var s = (Storyboard)Resources["Swap1"];
                s.Begin();
            }
        }

        void ContentPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastY = e.GetPosition(this).Y;
        }

        void ContentPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.GetPosition(this).Y == lastY)
            {
                var s = (Storyboard)Resources["Swap2"];
                s.Begin();
            }
        }

        void ContentText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastY = e.GetPosition(this).Y;

            /*var s = (Storyboard)Resources["Swap2"];
            s.Begin();*/
        }

        private static string GetImageTag(string s)
        {
            return Regex.Match(s, "<img([^>]+)>").Value;
        }

        private static string GetImageSource(string s)
        {
            var x = XElement.Parse(GetImageTag(s));
            if (x.Attribute("src") != null)
                return x.Attribute("src").Value;
            else
                return string.Empty;
        }

        //removes all html tags from string
        private static string StripTags(string s)
        {
            return Regex.Replace(s, "<[^>]*>", "");
        }

        public void Unload()
        {
            lastFeedsData.sources = sources;
            lastFeedsData.Write(E.Path + "\\News\\LastFeeds.data");
        }

        private void StackPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            timer_Tick(null, EventArgs.Empty);
        }

        private void Swap1_Completed(object sender, EventArgs e)
        {
            Grid.SetZIndex(ScrollViewer, 0);
            Grid.SetZIndex(ScrollViewer2, 1);
        }

        private void Swap2_Completed(object sender, EventArgs e)
        {
            Grid.SetZIndex(ScrollViewer, 1);
            Grid.SetZIndex(ScrollViewer2, 0);
            ScrollViewer2.ScrollToHome();
        }
    }
}
