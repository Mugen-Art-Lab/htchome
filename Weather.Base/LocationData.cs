using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace Weather.Base
{
    public class LocationData
    {
        /// <summary>
        /// location code
        /// </summary>
        public string Code;

        /// <summary>
        /// Location name
        /// </summary>
        public string City;

        /// <summary>
        /// Country name
        /// </summary>
        public string Country;
        /// <summary>
        /// Latitude
        /// </summary>
        public double Lat;
        /// <summary>
        /// Longtitude
        /// </summary>
        public double Lon;

        //for weather in search results
        [XmlIgnore]
        public string CityText { get; set; }
        [XmlIgnore]
        public string CountryText { get; set; }
        [XmlIgnore]
        public int Temperature;
        [XmlIgnore]
        public string TemperatureText { get; set; }
        [XmlIgnore]
        public int Skycode;
        [XmlIgnore]
        public BitmapImage Icon { get; set; }
    }
}
