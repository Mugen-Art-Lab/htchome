using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Weather.WeatherIcons
{
    public interface IAnimatedWeatherIcon
    {
        void FadeIn();
        void FadeOut();
    }
}
