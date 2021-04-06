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
    /// Interaction logic for Raindrop.xaml
    /// </summary>
    public partial class Raindrop : Window
    {
        int seed;
        int count;

        public Raindrop()
        {
            InitializeComponent();
        }

        public void Initialize(int seed)
        {
            this.seed = seed;
            Random r = new Random(Environment.TickCount * seed);
            Image.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\raindrop_0{0}.png", r.Next(1, 4)))));
            Storyboard s = (Storyboard)Resources["MoveAnim"];
            ((DoubleAnimation)s.Children[0]).BeginTime = TimeSpan.FromMilliseconds(r.Next(700, 2000));
            this.Left = r.Next(10, (int)(SystemParameters.WorkArea.Width - 42));
            this.Top = r.Next(10, (int)(SystemParameters.WorkArea.Height - 82));
            ((DoubleAnimation)s.Children[0]).From = this.Top;
            ((DoubleAnimation)s.Children[0]).To = this.Top + 60;
            Scale.ScaleX = r.Next(4, 10) / (double)10;

            Storyboard s1 = (Storyboard)Resources["FadeOut"];
            s1.BeginTime = ((DoubleAnimation)s.Children[0]).BeginTime;
        }

        private void this_Loaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)Resources["MoveAnim"]).Begin();
            ((Storyboard)Resources["FadeOut"]).Begin();
        }

        private void DoubleAnimation_Completed(object sender, EventArgs e)
        {
            /*Random r = new Random(Environment.TickCount * seed);

            Opacity = 1;
            count++;
            if (count < 4)
            {
                Initialize(seed);
                Storyboard s = (Storyboard)Resources["FadeOut"];
                s.Begin();
            }
            else*/
                this.Close();
        }
    }
}
