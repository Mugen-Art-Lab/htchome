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

        private Options options;

        public List<Source> Sources;

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

        public void Load()
        {
            Initialize();

            Sources = new List<Source>();

            if (Widget.Instance.Sett.Sources != null)
            {
                foreach (string s in Widget.Instance.Sett.Sources)
                {
                    var source = new Source { Url = s };
                    source.GetNewsFinished += GetNewsFinished;
                    Sources.Add(source);
                }
            }

            //lastFeedsData = LastFeedsData.Read(E.Path + "\\News\\LastFeeds.data");

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(Widget.Instance.Sett.Interval);
            timer.Tick += new EventHandler(TimerTick);
            timer.Start();

            TimerTick(null, EventArgs.Empty);

            Scale.ScaleX = Widget.Instance.Sett.ScaleFactor;

            var optionsItem = new MenuItem();
            optionsItem.Header = Widget.Instance.LocaleManager.GetString("Options");
            optionsItem.Click += new RoutedEventHandler(OptionsItemClick);
            Widget.Instance.Parent.ContextMenu.Items.Insert(0, optionsItem);

            var clearItem = new MenuItem();
            clearItem.Header = Widget.Instance.LocaleManager.GetString("Clear");
            clearItem.Click += new RoutedEventHandler(clearItem_Click);
            Widget.Instance.Parent.ContextMenu.Items.Insert(0, clearItem);

            options = new Options(this);

            NewsText.Text = Widget.Instance.LocaleManager.GetString("News");
        }

        void TimerTick(object sender, EventArgs e)
        {
            foreach (var source in Sources)
            {
                source.GetNews();
            }
        }

        void clearItem_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        void OptionsItemClick(object sender, RoutedEventArgs e)
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

        public void GetNewsFinished(object sender, EventArgs e)
        {
            var s = (Source)sender;
            foreach (var feed in s.Feed.Items)
            {
                NewsPanel.Dispatcher.Invoke((Action)delegate
                                                         {
                                                             var item = new NewsItem();
                                                             item.Title = feed.Title.Text;
                                                             item.Text = StripTags(feed.Summary.Text);
                                                             NewsPanel.Children.Insert(0, item);
                                                         },
                                            null);

            }
        }

        private string StripTags(string input)
        {
            var regHtml = new System.Text.RegularExpressions.Regex("<[^>]*>");
            return regHtml.Replace(input, "");
        }
    }
}
