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
using HTCHome.Core;

namespace BookmarksWidget
{
    /// <summary>
    /// Interaction logic for Bookmarks.xaml
    /// </summary>
    public partial class Bookmarks : UserControl
    {
        public Bookmarks()
        {
            InitializeComponent();
        }

        private void Initialize()
        {
            Body.Source = new BitmapImage(new Uri(E.Path + "\\Bookmarks\\Resources\\body.png"));
            Header.Source = new BitmapImage(new Uri(E.Path + "\\Bookmarks\\Resources\\header.png"));
            Footer.Source = new BitmapImage(new Uri(E.Path + "\\Bookmarks\\Resources\\footer.png"));
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Initialize();

            HeaderTextBlock.Text = Widget.LocaleManager.GetString("Bookmarks");

            if (Widget.Sett.bookmarks != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    ((BookmarkVisual)BookmarksGrid.Children[i]).SetBookmark(Widget.Sett.bookmarks[i]);
                }
            }

            Scale.ScaleX = Widget.Sett.scaleFactor;
        }

        private void BookmarksGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source.GetType() == typeof(BookmarkVisual))
            {
                if (((BookmarkVisual)e.Source).bookmark == null || string.IsNullOrEmpty(((BookmarkVisual)e.Source).bookmark.Url))
                {
                    SetBookmarkWindow w = new SetBookmarkWindow();
                    w.Left = this.PointToScreen(Mouse.GetPosition(this)).X - 10;
                    w.Top = this.PointToScreen(Mouse.GetPosition(this)).Y - 10;
                    w.ShowDialog();
                    Bookmark b = new Bookmark();
                    b.Url = w.Url;
                    ((BookmarkVisual)e.Source).SetBookmark(b);
                }
                else
                    WinAPI.ShellExecute(IntPtr.Zero, "open", ((BookmarkVisual)e.Source).bookmark.Url, string.Empty, string.Empty, 0);
            }
        }

        public void Unload()
        {
            Bookmark[] bookmarks = new Bookmark[6];

            for (int i = 0; i < 6; i++)
            {
                bookmarks[i] = ((BookmarkVisual)BookmarksGrid.Children[i]).bookmark;
            }

            Widget.Sett.bookmarks = new List<Bookmark>(bookmarks);
        }
    }
}
