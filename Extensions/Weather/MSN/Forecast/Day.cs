using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MSN.Forecast
{
    public class Day
    {
        public Daily Daily { get; set; }

        public Day()
        {
            Daily = new Daily();
        }
    }
}
