using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Weather.Base
{
    public class ForecastData
    {
        public int HighTemperature { get; set; }

        public int LowTemperature { get; set; }

        public string Text { get; set; }

        /// <summary>
        /// Weather pic number (see UIFramework.Weather images).
        /// 0 is a valid value and means "no icon" — providers may return it
        /// when they have no condition data; the UI renders an empty icon.
        /// </summary>
        public int SkyCode { get; set; }

        public string Url { get; set; }

        public ForecastData()
        {
            SkyCode = 1;
        }
    }
}
