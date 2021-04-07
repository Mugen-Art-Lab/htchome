using System.Collections.Generic;
using Weather = OpenWeatherMap.Forecast.Weather;

namespace OpenWeatherMap
{
    public class City
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public CitySys Sys { get; set; }

        public CityMain Main { get; set; }

        public List<Forecast.Weather> Weather { get; set; }

        public City()
        {
            Weather = new List<Forecast.Weather>();
        }
    }
}
