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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Home.Base;

namespace Clock.Controls
{
    /// <summary>
    /// Interaction logic for ForecastItem.xaml
    /// </summary>
    public partial class ForecastItem : UserControl
    {
        public ForecastItem()
        {
            InitializeComponent();
        }

        public string Url { get; set; }

        private Point mouseCoords;
        private void UserControlMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mouseCoords = e.MouseDevice.GetPosition(this);
        }

        private void UserControlMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (mouseCoords == e.MouseDevice.GetPosition(this) && !string.IsNullOrEmpty(Url))
            {
                WinAPI.ShellExecute(IntPtr.Zero, "open", Url, string.Empty, string.Empty, 0);
            }
        }

        private void UserControlLoaded(object sender, RoutedEventArgs e)
        {
            if (App.Settings != null && App.Settings.DisableShadows)
            {
                DayName.Effect = null;
                Temperature.Effect = null;
            }
        }

        private int order = 0;
        public int Order
        {
            get { return order; }
            set
            {
                order = value;
                var s = (Storyboard)Icon.Resources["FlipAnim1"];
                s.BeginTime = TimeSpan.FromMilliseconds(250 * value);
            }
        }

        private int temp;
        public void FlipWeather(int i)
        {
            //skycode 0 means "no icon" (forecast placeholder or a provider without data)
            if (i == 0)
            {
                temp = 0;
                Icon.Source = null;
                return;
            }
            if (i != temp)
            {
                temp = i;
                var s = (Storyboard)Icon.Resources["FlipAnim1"];
                s.Begin(this);
            }
        }

        private void DoubleAnimationCompleted(object sender, EventArgs e)
        {

            Icon.Source = temp == 0 ? null : new BitmapImage(new Uri(string.Format("/UIFramework.Weather;Component/Images/weather_{0}.png", temp), UriKind.Relative));
            var s = (Storyboard)Icon.Resources["FlipAnim2"];
            s.Begin(this);
        }
    }
}
