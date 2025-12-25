using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Home.Base;

namespace Weather.Base
{
    public class WeatherData : XmlSerializable
    {
        public LocationData Location = new LocationData(); //name of the current location
        public int Temperature = 0; //curent temperature
        public int FeelsLike; //feels like temperature
        public int WindSpeed;
        public int Humidity;
        public ForecastData Curent = new ForecastData(); //curent forecast
        public List<ForecastData> ForecastList = new List<ForecastData>();
    }
}
