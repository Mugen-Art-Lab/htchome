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
    /// Interaction logic for RainWiper.xaml
    /// </summary>
    public partial class RainWiper : UserControl
    {
        public RainWiper()
        {
            InitializeComponent();
        }

        private int count;
        public void Initialize()
        {
            Wiper.Source = new BitmapImage(new Uri("/UIFramework.Weather;component/Images/rain_wiper.png", UriKind.Relative));
            Streaks.Source = new BitmapImage(new Uri("/UIFramework.Weather;component/Images/raindrop_streaks.png", UriKind.Relative));
            Canvas.SetLeft(this, 100);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var s = (Storyboard)Resources["MoveAnim"];
            s.Begin();

            var s1 = (Storyboard)Resources["StreaksAnim"];
            s1.Begin();
        }

        private void DoubleAnimation_CurrentStateInvalidated(object sender, EventArgs e)
        {
            if (((System.Windows.Media.Animation.Clock)sender).CurrentState == ClockState.Active)
            {
                Opacity = 1;
                Streaks.Opacity = 0.5;
            }
        }

        private void DoubleAnimation_Completed(object sender, EventArgs e)
        {
            count++;
            if (count < 1) //2
            {
                var s = (Storyboard)Resources["MoveAnim"];
                s.Begin();

                var s1 = (Storyboard)Resources["StreaksAnim"];
                s1.Begin();
                Streaks.Opacity = 0.5;
            }
            else
            {
                this.Opacity = 0;
            }
        }

        private void DoubleAnimation_Completed_1(object sender, EventArgs e)
        {
            Streaks.Opacity = 0;
        }
    }
}
