using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WeatherClockWidget.V2
{
    /// <summary>
    /// Universal class to support older versions providers
    /// </summary>
    public class WeatherProviderTemplate : WeatherClockWidget.V2.IWeatherProvider, WeatherClockWidget.IWeatherProvider
    {
        public virtual List<WeatherClockWidget.V2.Domain.LocationData> GetLocations(string query)
        {
            throw new NotImplementedException("Error override WeatherClockWidget.V2.WeatherProviderTemplate.GetLocations method");
        }

        public virtual WeatherClockWidget.V2.Domain.WeatherReportData GetWeatherReports(System.Globalization.CultureInfo ci, WeatherClockWidget.V2.Domain.LocationData location, bool isMetric)
        {

            throw new NotImplementedException("Error override WeatherClockWidget.V2.WeatherProviderTemplate.GetWeatherReports method");
        }

        #region For old version providers
        public WeatherClockWidget.Domain.Coordinates GetCoordinates(string locationCode)
        {
            List<WeatherClockWidget.V2.Domain.LocationData> locList = this.GetLocations(locationCode);
            foreach (WeatherClockWidget.V2.Domain.LocationData ld in this.GetLocations(locationCode))
                if (ld.Code == locationCode)
                {
                    return new WeatherClockWidget.Domain.Coordinates() { X = ld.Lat, Y = ld.Lon };
                }
            return new WeatherClockWidget.Domain.Coordinates() { X = 0, Y = 0 };
        }

        public List<WeatherClockWidget.Domain.CityLocation> GetLocation(string s)
        {
            List<WeatherClockWidget.Domain.CityLocation> locList = new List<WeatherClockWidget.Domain.CityLocation>();
            foreach (WeatherClockWidget.V2.Domain.LocationData ld in GetLocations(s))
            {
                locList.Add(new WeatherClockWidget.Domain.CityLocation() { City = ld.City, Code = ld.Code });
            }
            return locList;
        }

        public WeatherClockWidget.Domain.WeatherReport GetWeatherReport(string locale, string locationcode, int degreeType)
        {
            WeatherClockWidget.Domain.WeatherReport report = new WeatherClockWidget.Domain.WeatherReport();
            System.Globalization.CultureInfo ci = new System.Globalization.CultureInfo("en-US");
            try { ci = new System.Globalization.CultureInfo(locale); }
            catch { }
            WeatherClockWidget.V2.Domain.WeatherReportData reportNew = GetWeatherReports(ci, new WeatherClockWidget.V2.Domain.LocationData() { Code = locationcode }, degreeType == 0);
            report.Location = reportNew.Location.City;
            report.NowTemp = reportNew.Temperature;
            report.High = reportNew.Curent.TemperatureHigh;
            report.Low = reportNew.Curent.TemperatureLow;
            report.NowSkyCode = reportNew.Curent.SkyCode;
            report.NowSky = reportNew.Curent.Description;
            report.Url = reportNew.Curent.Url.ToString();

            reportNew.ForecastList.Sort();
            report.Forecast = new List<WeatherClockWidget.Domain.DayForecast>();
            for (int f = 0; f < 5; f++)
            {
                WeatherClockWidget.V2.Domain.ForecastData fd = reportNew.ForecastList[f];
                report.Forecast.Add(new WeatherClockWidget.Domain.DayForecast()
                {
                    HighTemperature = fd.TemperatureHigh,
                    LowTemperature = fd.TemperatureLow,
                    SkyCode = fd.SkyCode,
                    Text = fd.Description,
                    Url = fd.Url.ToString()
                });
            }

            return report;
        }
        #endregion
    }
}
