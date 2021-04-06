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

namespace WeatherClockWidget.FullscreenAnimation
{
    /// <summary>
    /// Interaction logic for Snowflake.xaml
    /// </summary>
    public partial class Snowflake : Window
    {
        int seed;
        private double wind = 0;
        private double gravity = 2500;
        private double speedX = 1.0;
        private double scale = 1.0;

        private double lifeTime; //used for changing wind and gravity

        Random r;


        public Snowflake()
        {
            InitializeComponent();
        }


        public void Initialize(int seed)
        {
            this.seed = seed;
            r = new Random(Environment.TickCount * seed);
            Image.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\snow{0}.png", r.Next(1, 6)))));
            Storyboard s = (Storyboard)Resources["MoveAnim"];
            ((DoubleAnimation)s.Children[0]).Duration = TimeSpan.FromMilliseconds(r.Next(7500, 9500));
            ((DoubleAnimation)s.Children[0]).BeginTime = TimeSpan.FromMilliseconds(0);
            this.Left = r.Next(10, (int)(SystemParameters.WorkArea.Width - 42));
            this.Top =  20;
            ((DoubleAnimation)s.Children[0]).From = this.Top;
            ((DoubleAnimation)s.Children[0]).To = SystemParameters.WorkArea.Height;

            scale = (25 + r.NextDouble() * 75) / 100;
            Scale.ScaleX = scale;

            speedX = r.NextDouble() - r.NextDouble();
            gravity = 0.5 * scale;

            /*Storyboard s1 = (Storyboard)Resources["FadeOut"];
            s1.BeginTime = ((DoubleAnimation)s.Children[0]).BeginTime;*/
        }

        private void this_Loaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)Resources["MoveAnim"]).Begin();
        }

        void MoveSnowflake()
        {
            lifeTime += 0.001;
            this.Left += speedX + wind * scale;

            if (lifeTime > 2.0)
            {
                lifeTime = 0;

                double newX = r.NextDouble() - r.NextDouble();
                double chX = (newX - wind) / (50 + r.Next(50));
                wind += chX;
            }
        }

        private void DoubleAnimation_CurrentTimeInvalidated(object sender, EventArgs e)
        {
            MoveSnowflake();
        }
    }
}
