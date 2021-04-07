using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Photos.Controls;

namespace Photos.Windows
{
    /// <summary>
    /// Interaction logic for PhotoWindow.xaml
    /// </summary>
    public partial class PhotoWindow : Window
    {
        readonly Random r = new Random(Environment.TickCount);

        public PhotoWindow()
        {
            InitializeComponent();
        }

        public void AddPicture(string file)
        {
            var newPhoto = new PhotoControl(); //now we need new bottom photo
            newPhoto.Image.Source = null;
            newPhoto.Rotation.Angle = r.Next(-20, 20);
            var bi = new BitmapImage();
            bi.BeginInit();
            if (App.Pics.Count == 0 || file == null)
                bi.UriSource = new Uri("/Photos;component/Resources/preview_portrait.png", UriKind.Relative);
            else
            {
                bi.UriSource = new Uri(file, UriKind.Absolute);
            }
            bi.DecodePixelWidth = 300;
            bi.EndInit();

            newPhoto.Image.Source = bi;
            if (newPhoto.Image.Source.Height > newPhoto.Image.Source.Width) //portrait photo
            {
                newPhoto.Height = 300;
                Canvas.SetLeft(newPhoto, 50);
            }
            else
            {
                newPhoto.Width = 300;
                Canvas.SetTop(newPhoto, 50);
            }

            Root.Children.Add(newPhoto);
        }

        private void WindowMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //var s = (Storyboard) Resources["FadeInAnim"];
            //var r = new Random(Environment.TickCount);
            //var left = this.Left + r.Next(-250, 250);
            //var top = this.Top + r.Next(-250, 250);

            //((DoubleAnimation) s.Children[1]).From = this.Left;
            //((DoubleAnimation)s.Children[1]).To = left;
            //((DoubleAnimation)s.Children[2]).From = this.Top;
            //((DoubleAnimation)s.Children[2]).To = top;

            //s.Begin(this);

        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            var s = (Storyboard)Resources["FadeInAnim"];
            var left = this.Left + r.Next(-350, 350);
            var top = this.Top + r.Next(-250, 250);

            ((DoubleAnimation)s.Children[1]).From = this.Left;
            ((DoubleAnimation)s.Children[1]).To = left;
            ((DoubleAnimation)s.Children[2]).From = this.Top;
            ((DoubleAnimation)s.Children[2]).To = top;

            s.Begin(this);
        }
    }
}
