using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenWeatherMap.Forecast
{
    public class ForecastResponse
    {
        public City City { get; set; }

        public List<WeatherReport> List { get; set; }

        public ForecastResponse()
        {
            City = new City();
            List = new List<WeatherReport>();
        }
    }
}
