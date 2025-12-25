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

        public int SkyCode { get; set; }

        public string Url { get; set; }

        public ForecastData()
        {
            SkyCode = 1;
        }
    }
}
