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
    /// Interaction logic for RainCloud.xaml
    /// </summary>
    public partial class RainCloud : UserControl
    {
        private int direction;

        public RainCloud()
        {
            InitializeComponent();
        }

        public void Initialize(int direction)
        {
            if (direction == 0)
            {
                Image.Source = new BitmapImage(new Uri("/UIFramework.Weather;component/Images/cloud_4.png", UriKind.Relative));
                Canvas.SetTop(this, -50);
                //Scale.ScaleX = 0.7;
                //Scale.ScaleY = 0.7;
            }
            else
            {
                Image.Source = new BitmapImage(new Uri("/UIFramework.Weather;component/Images/cloud_8.png", UriKind.Relative));
                //Scale.ScaleX = 0.75;
                //Scale.ScaleY = 0.65;
                Canvas.SetTop(this, -50);
            }
            this.direction = direction;
            /*((DoubleAnimation)s.Children[1]).From = left;
            ((DoubleAnimation)s.Children[1]).To = left + 70;
            this.Top = top - 70;*/
        }

        private void DoubleAnimation_Completed_1(object sender, EventArgs e)
        {
            if (direction == 0)
            {
                Storyboard fadeOut = (Storyboard)Resources["FadeOutL"];
                fadeOut.Begin();
            }
            else
            {
                Storyboard fadeOut = (Storyboard)Resources["FadeOutR"];
                fadeOut.Begin();
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (direction == 0)
            {
                Storyboard fadeIn = (Storyboard)Resources["FadeInL"];
                fadeIn.Begin();
            }
            else
            {
                Storyboard fadeIn = (Storyboard)Resources["FadeInR"];
                fadeIn.Begin();
            }
        }
    }
}
