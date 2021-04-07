using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Home.Base;
using News.Windows;

namespace News.Controls
{
    /// <summary>
    /// Interaction logic for FeedItem.xaml
    /// </summary>
    public partial class FeedItem : UserControl
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private WebClient webClient;

        public string OriginalContent { get; set; }

        public string Title
        {
            get { return TitleBox.Text; }
            set { TitleBox.Text = value; }
        }

        public string Description
        {
            get { return DescriptionBox.Text; }
            set { DescriptionBox.Text = value; }
        }

        public string Url { get; set; }

        private bool readed;
        public bool Readed
        {
            get { return readed; }
            set
            {
                if (value != readed)
                {
                    readed = value;
                    TitleBox.Foreground = Brushes.LightGray;
                    DescriptionBox.Foreground = Brushes.LightGray;
                    FromBox.Foreground = Brushes.LightGray;
                    App.UnreadNewsCount--;
                }
            }
        }

        private string picture;
        public string Picture
        {
            set
            {
                picture = value;
                Image.Visibility = Visibility.Visible;
                webClient = new WebClient();
                webClient.DownloadFileAsync(new Uri(value), Path.GetTempPath() + Path.GetFileName(value));
                webClient.DownloadFileCompleted += WDownloadFileCompleted;
            }
        }

        public string ChannelName
        {
            get { return ChannelBox.Text; }
            set { ChannelBox.Text = value; }
        }

        private int order = 0;
        public int Order
        {
            get { return order; }
            set
            {
                order = value;
                TranslateAnim.BeginTime = TimeSpan.FromMilliseconds(550 + 150 * value);
                OpacityAnim.BeginTime = TimeSpan.FromMilliseconds(550 + 150 * value);
            }
        }

        private bool compactMode;
        public bool CompactMode
        {
            get { return compactMode; }
            set
            {
                if (value != compactMode)
                {
                    compactMode = value;
                    if (value)
                    {
                        ContentPanel.Visibility = System.Windows.Visibility.Collapsed;
                        FromBox.Visibility = System.Windows.Visibility.Collapsed;
                    }
                    else
                    {
                        ContentPanel.Visibility = System.Windows.Visibility.Visible;
                        FromBox.Visibility = System.Windows.Visibility.Visible;
                    }
                }
            }
        }

        public FeedItem()
        {
            InitializeComponent();
        }

        void WDownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            webClient.DownloadFileCompleted -= WDownloadFileCompleted;
            var path = Path.GetTempPath() + Path.GetFileName(picture);
            if (File.Exists(path))
            {
                var f = new FileInfo(path);
                if (f.Length > 0)
                {
                    try
                    {
                        Image.Source = new BitmapImage(new Uri(path));
                        Image.Visibility = System.Windows.Visibility.Visible;
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Can't set feed item image source. Image " + path + "(original: " + picture + ")" + ex);
                    }
                }
            }
        }

        private void UserControlMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Readed = true;
            if (!App.Settings.ShowPreviewInBrowser)
            {
                var p = new PreviewWindow();
                p.Header = Title;
                p.Description = OriginalContent;
                p.Url = Url;
                p.ShowDialog();
            }
            else
            {
                WinAPI.ShellExecute(IntPtr.Zero, "open", Url, string.Empty, string.Empty, 0);
            }

        }
    }
}
