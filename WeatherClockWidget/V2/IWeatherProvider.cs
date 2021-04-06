using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WeatherClockWidget.V2
{
    public interface IWeatherProvider
    {
        /// <summary>
        /// Get location list
        /// </summary>
        /// <param name="query">Location Name</param>
        /// <returns></returns>
        List<WeatherClockWidget.V2.Domain.LocationData> GetLocations(string query);
        /// <summary>
        /// Get weather report
        /// </summary>
        /// <param name="ci">Culture info (weather report language)</param>
        /// <param name="location">Location code</param>
        /// <param name="isMetric">Is metric data</param>
        /// <returns></returns>
        WeatherClockWidget.V2.Domain.WeatherReportData GetWeatherReports(System.Globalization.CultureInfo ci, WeatherClockWidget.V2.Domain.LocationData location, bool isMetric);
    }
}
