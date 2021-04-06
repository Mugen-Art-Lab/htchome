using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WeatherClockWidget.V2.Domain
{
    public struct LocationData
    {
        /// <summary>
        /// location code
        /// </summary>
        public string Code;
        /// <summary>
        /// Location name
        /// </summary>
        public string City;

        public double Lat;
        public double Lon;
        /// <summary>
        /// Above mean sea level (AMSL) in metric
        /// </summary>
        public Int32 Height;
    }
}
