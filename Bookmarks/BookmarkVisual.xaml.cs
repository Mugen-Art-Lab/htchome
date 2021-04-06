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
using System.ComponentModel;
using System.Drawing;
using Forms = System.Windows.Forms;
using System.IO;
using System.Windows.Threading;
using System.Threading;

namespace BookmarksWidget
{
    /// <summary>
    /// Interaction logic for BookmarkVisual.xaml
    /// </summary>
    public partial class BookmarkVisual : UserControl
    {
        public Bookmark bookmark = new Bookmark();

        public BookmarkVisual()
        {
            InitializeComponent();
        }

        public void Initialize()
        {
            Icon.Source = new BitmapImage(new Uri(E.Path + "\\Bookmarks\\Resources\\thumbnail_addbookmark.png"));
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!IsInDesignMode() && (bookmark == null || string.IsNullOrEmpty(bookmark.ThumbnailName)))
            {
                Initialize();
            }

            if (!IsInDesignMode())
            {
                ChangeItem.Header = Widget.LocaleManager.GetString("Change");
                ClearItem.Header = Widget.LocaleManager.GetString("Clear");
            }
        }

        private bool IsInDesignMode()
        {
            return (bool)DependencyPropertyDescriptor.FromProperty(
                DesignerProperties.IsInDesignModeProperty,
                typeof(FrameworkElement)).Metadata.DefaultValue;
        }

        public void SetBookmark(Bookmark b)
        {
            if (b == null || string.IsNullOrEmpty(b.Url))
                return;

            bookmark = b;

            if (string.IsNullOrEmpty(b.ThumbnailName))
            {
                bookmark.ThumbnailName = b.Url.Replace("http://", "");
                while (bookmark.ThumbnailName.Contains('/'))
                    bookmark.ThumbnailName = bookmark.ThumbnailName.Replace('/', '-');
                ThreadStart threadStarter = delegate
                {
                    try
                    {
                        GrabDataSolutions.Images.WebScreenshotExtractor extractor = new GrabDataSolutions.Images.WebScreenshotExtractor(new Uri(b.Url), 320, 240, 150, 150);
                        extractor.ScrollingEnabled = false;
                        //bookmark.Url = url;


                        /*if (Widget.Sett.bookmarks.IndexOf(bookmark) >= 0)
                            Widget.Sett.bookmarks[Widget.Sett.bookmarks.IndexOf(bookmark)] = bookmark;
                        else
                            Widget.Sett.bookmarks.Add(bookmark);*/

                        extractor.MakeScreenshot();
                        extractor.SaveImage(E.Path + "\\Bookmarks\\Thumbnails\\" + bookmark.ThumbnailName + ".png");

                        Icon.Dispatcher.Invoke((Action)delegate
                        {
                            Icon.Source = new BitmapImage(new Uri(E.Path + "\\Bookmarks\\Thumbnails\\" + bookmark.ThumbnailName + ".png"));
                        }, null);
                    }
                    catch (Exception ex)
                    {

                    }
                };

                Thread thread = new Thread(threadStarter);
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            else
            {
                Icon.Dispatcher.Invoke((Action)delegate
                {
                    Icon.Source = new BitmapImage(new Uri(E.Path + "\\Bookmarks\\Thumbnails\\" + bookmark.ThumbnailName + ".png"));
                }, null);
            }
        }

        private void ChangeItem_Click(object sender, RoutedEventArgs e)
        {
            SetBookmarkWindow w = new SetBookmarkWindow();
            w.Left = this.PointToScreen(Mouse.GetPosition(this)).X - 10;
            w.Top = this.PointToScreen(Mouse.GetPosition(this)).Y - 10;
            w.ShowDialog();
            Bookmark b = new Bookmark();
            b.Url = w.Url;
            SetBookmark(b);
        }

        private void ClearItem_Click(object sender, RoutedEventArgs e)
        {
            Icon.Source = new BitmapImage(new Uri(E.Path + "\\Bookmarks\\Resources\\thumbnail_addbookmark.png"));
            bookmark = null;
        }
    }
}
