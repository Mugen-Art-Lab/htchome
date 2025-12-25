using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MSN.Forecast
{
    public class Weather
    {
        /// <summary>
        /// Weather text
        /// </summary>
        public string Cap { get; set; }

        public double Temp { get; set; }
        
        /// <summary>
        /// Feels like
        /// </summary>
        public double Feels { get; set; }

        public int Icon { get; set; }

        public string Sky { get; set; }

        /// <summary>
        /// Humidity
        /// </summary>
        public double Rh { get; set; }

        /// <summary>
        /// Wind speed
        /// </summary>
        public double WindSpd { get; set; }
    }
}
