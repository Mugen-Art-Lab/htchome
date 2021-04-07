using System.Collections.Generic;

namespace OpenWeatherMap.Forecast
{
    public class WeatherReport
    {
        public double Dt { get; set; }

        public Temp Temp { get; set; }

        public double Pressure { get; set; }

        public double Humidity { get; set; }

        public List<Weather> Weather { get; set; }

        public double Speed { get; set; }

        public double Deg { get; set; }

        public double Clouds { get; set; }

        public WeatherReport()
        {
            Weather = new List<Weather>();
            Temp = new Temp();
        }
    }
}
