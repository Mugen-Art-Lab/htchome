using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace WeatherClockWidget.V2.Domain
{
    public class ForecastData : IComparable
    {
        public enum SkyCodes
        {
            Sunny = 1
        }

        public ForecastData()
        {
            Description = "Clear";
            SkyCode = 1;
            Date = DateTime.Today;
        }
        /// <summary>
        /// Forecast date
        /// </summary>
        public DateTime Date { get; set; }
        /// <summary>
        /// Max temperature
        /// </summary>
        public int TemperatureHigh { get; set; }
        /// <summary>
        /// Min temperature
        /// </summary>
        public int TemperatureLow { get; set; }
        /// <summary>
        /// Max felt temperature
        /// </summary>
        public int TemperatureFeltHigh { get; set; }
        /// <summary>
        /// Min felt temperature
        /// </summary>
        public int TemperatureFeltLow { get; set; }
        /// <summary>
        /// FOrecat description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Sky code (icon number)
        /// </summary>
        public int SkyCode { get; set; }
        /// <summary>
        /// Go curent forecast site url
        /// </summary>
        public Uri Url { get; set; }
        /// <summary>
        /// Wind Direction in degree
        /// </summary>
        public Int32 WindDirection { get; set; }
        /// <summary>
        /// Wind speed in m/s
        /// </summary>
        public Int32 WindSpeed { get; set; }
        /// <summary>
        /// Atmospheric pressure in mm Hg
        /// </summary>
        public Int32 Pressure { get; set; }
        /// <summary>
        /// Humidity is the amount of water vapor in the air (percent)
        /// </summary>
        public Int32 Humidity { get; set; }
        /// <summary>
        /// Distance at which an object or light can be clearly discerned (km)
        /// </summary>
        public Int32 Visible { get; set; }
        /// <summary>
        /// Oxygen content of the atmosphere (gramm/м3)
        /// </summary>
        public Int32 Oxygen { get; set; }
        /// <summary>
        /// UV index (0-16)
        /// </summary>
        public Byte UV { get; set; }
        /// <summary>
        /// Sun rise time for curent location (GMT)
        /// </summary>
        public TimeSpan SunRise { get; set; }
        /// <summary>
        /// Sun set time for curent location (GMT)
        /// </summary>
        public TimeSpan SunSet { get; set; }

        public int CompareTo(object obj)
        {
            if (obj is ForecastData)
            {
                ForecastData otherForecast = (ForecastData)obj;
                return this.Date.CompareTo(otherForecast.Date);
            }
            else
            {
                throw new ArgumentException("object is not a Forecast");
            }
        }
    }
}
