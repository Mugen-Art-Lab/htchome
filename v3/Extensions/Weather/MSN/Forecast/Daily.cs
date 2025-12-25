using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MSN.Forecast
{
    public class Daily
    {
        public Weather Day { get; set; }

        public Weather Night { get; set; }

        public int Icon { get; set; }

        public double TempHi { get; set; }

        public double TempLo { get; set; }

        public Daily()
        {
            Day = new Weather();

            Night = new Weather();
        }
    }
}
