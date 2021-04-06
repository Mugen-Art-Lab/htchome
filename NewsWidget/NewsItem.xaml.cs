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
using System.Windows.Media.Animation;
using HTCHome.Core;
using System.Net;
using System.IO;
using System.Windows.Markup;
using E = HTCHome.Core.Environment;

namespace NewsWidget
{
    /// <summary>
    /// Interaction logic for NewsItem.xaml
    /// </summary>
    public partial class NewsItem : UserControl
    {
        public DateTime Date;

        public string Link;
        public string Source;

        public string TextShort;
        public string TextFull;

        //public Feed feed;

        public string Text
        {
            get { return TextFull; }
            set
            {
                TextFull = value;

                //ContentDocument.Blocks.Clear();
                //ContentDocument.Blocks.Add((Block)XamlReader.Parse(HTXConverter2.HtmlToXamlConverter.ConvertHtmlToXaml(value, false)));
                ContentTextBlock.Text = value;
            }
        }

        public string Title
        {
            get { return TitleTextBlock.Text; }
            set { TitleTextBlock.Text = value; }
        }

        public static void HyperlinkMouseDown(object sender, MouseButtonEventArgs e)
        {
            WinAPI.ShellExecute(IntPtr.Zero, "open", ((Hyperlink)sender).NavigateUri.ToString(), "", "", 1);
        }

        string img;
        public string ImageSource
        {
            get { return img; }
            set
            {
                if (!string.IsNullOrEmpty(value) && value.StartsWith("http://"))
                {
                    IconImage.Source = new BitmapImage(new Uri(value));
                    IconImage.Visibility = System.Windows.Visibility.Visible;
                }

                img = value;
            }
        }

        private int _order = 0;
        public int Order
        {
            get { return _order; }
            set
            {
                _order = value;
            }
        }

        public NewsItem()
        {
            InitializeComponent();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            /*if (ContentPanel.Visibility == System.Windows.Visibility.Visible)
                WinAPI.ShellExecute(IntPtr.Zero, "open", Link, "", "", 1);*/
        }

    }
}
