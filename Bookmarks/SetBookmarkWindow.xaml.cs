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
using System.Windows.Shapes;

namespace BookmarksWidget
{
    /// <summary>
    /// Interaction logic for SetBookmarkWindow.xaml
    /// </summary>
    public partial class SetBookmarkWindow : Window
    {
        public string Url;
        public SetBookmarkWindow()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Url = Link.Text;
        }

        private void Link_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.Close();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Link.Focus();
            Link.CaretIndex = Link.Text.Length;
        }



    }
}
