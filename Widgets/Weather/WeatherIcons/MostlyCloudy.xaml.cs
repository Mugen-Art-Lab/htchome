using System;
using System.Collections.Generic;
using System.Globalization;
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
using Weather.Base;

namespace Weather.WeatherIcons
{
    /// <summary>
    /// Interaction logic for Sun.xaml
    /// </summary>
    public partial class MostlyCloudy : IAnimatedWeatherIcon
    {
        public MostlyCloudy()
        {
            InitializeComponent();
        }

        public void FadeIn()
        {
            var s = (Storyboard) Resources["FadeIn"];
            s.Begin();
        }

        public void FadeOut()
        {
            var s = (Storyboard)Resources["FadeOut"];
            s.Begin();
        }
    }
}
