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

namespace Clock.WeatherAnimation
{
    /// <summary>
    /// Interaction logic for Cloud.xaml
    /// </summary>
    public partial class Cloud : UserControl
    {
        public Cloud()
        {
            InitializeComponent();
        }

        private int count;

        public void Initialize(int seed)
        {
            Random r = new Random(Environment.TickCount * seed);
            Image.Source = new BitmapImage(new Uri(string.Format("/UIFramework.Weather;component/Images/cloud_{0}.png", r.Next(1, 9)), UriKind.Relative));
            Storyboard s = (Storyboard)Resources["MoveAnim"];
            //s.BeginTime = TimeSpan.FromMilliseconds(r.Next(0, 100) + seed * 1000);
            ((DoubleAnimation)s.Children[0]).Duration = TimeSpan.FromMilliseconds(r.Next(5000, 12000));
            //Scale.ScaleX = r.Next(5, 10) / 10.0;
            Canvas.SetTop(this, r.Next(-50, 150 - (int)(256 * Scale.ScaleX)));

            Storyboard fadeIn = (Storyboard)Resources["FadeIn"];
            fadeIn.Begin();

            Storyboard fadeOut = (Storyboard)Resources["FadeOut"];
            fadeOut.BeginTime = ((DoubleAnimation)s.Children[0]).Duration.TimeSpan - TimeSpan.FromMilliseconds(1000);
        }

        private void this_Loaded(object sender, RoutedEventArgs e)
        {
            Storyboard s = (Storyboard)Resources["MoveAnim"];
            s.Begin();
        }

        private void DoubleAnimation_Completed(object sender, EventArgs e)
        {
            count++;
            if (count < 5)
            {
                Initialize(Environment.TickCount);
                Storyboard s = (Storyboard)Resources["MoveAnim"];
                s.Begin();
            }
            else
                if (this.Parent != null)
                {
                    ((Canvas)this.Parent).Children.Remove(this);
                }
        }

        private void DoubleAnimation_Completed_1(object sender, EventArgs e)
        {
            Storyboard fadeOut = (Storyboard)Resources["FadeOut"];
            fadeOut.Begin();
        }

    }
}
