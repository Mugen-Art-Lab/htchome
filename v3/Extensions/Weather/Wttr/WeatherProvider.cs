using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using Home.Base;
using Weather.Base;
using System.Diagnostics;

namespace WttrIn
{
    public class WeatherProvider : IWeatherProvider
    {
        static WeatherProvider()
        {
            try
            {
                //Win7 ships with TLS 1.0 by default and wttr.in is https-only
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException) { }
        }

        public List<LocationData> GetLocations(string query, CultureInfo culture)
        {
            return GetLocations(query, culture, Shared.TemperatureScale);
        }

        public List<LocationData> GetLocations(string query, CultureInfo culture, TemperatureScale tempScale)
        {
            var result = new List<LocationData>();

            //wttr.in resolves the query to its best match itself, so the search returns a single candidate
            var report = GetReport(Uri.EscapeDataString(query.Trim()), culture, tempScale, WindSpeedScale.Kmh);
            if (report == null)
                return result;

            var l = new LocationData();
            l.City = string.IsNullOrEmpty(report.City) ? query.Trim() : report.City;
            l.Country = report.Country;
            l.Lat = report.Lat;
            l.Lon = report.Lon;
            l.Code = WttrCore.MakeLocationCode(report.Lat, report.Lon, l.City);
            l.Temperature = report.Temperature;
            l.Skycode = WttrCore.GetWeatherPic(report.WeatherCode, IsDay(report.Lat, report.Lon, TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)));
            result.Add(l);

            return result;
        }

        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale,
            WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            try
            {
                double lat, lon;
                string embeddedCity;
                if (!WttrCore.TryParseLocationCode(location.Code, out lat, out lon, out embeddedCity))
                    return null;

                if (string.IsNullOrEmpty(location.City) && !string.IsNullOrEmpty(embeddedCity))
                    location.City = embeddedCity;

                var report = GetReport(WttrCore.FormatCoordinate(lat) + "," + WttrCore.FormatCoordinate(lon), culture, tempScale, windSpeedScale);
                if (report == null)
                    return null;

                var isDay = IsDay(lat, lon, baseUtcOffset);

                var weatherReport = new WeatherData();
                weatherReport.Location = location;
                weatherReport.Temperature = report.Temperature;
                weatherReport.FeelsLike = report.FeelsLike;
                weatherReport.Humidity = report.Humidity;
                weatherReport.WindSpeed = report.WindSpeed;
                weatherReport.Curent = new ForecastData
                {
                    Text = report.Text,
                    SkyCode = WttrCore.GetWeatherPic(report.WeatherCode, isDay)
                };

                weatherReport.ForecastList = new List<ForecastData>();
                foreach (var day in report.Days)
                {
                    weatherReport.ForecastList.Add(new ForecastData
                    {
                        HighTemperature = day.HighTemperature,
                        LowTemperature = day.LowTemperature,
                        SkyCode = WttrCore.GetWeatherPic(day.WeatherCode, true),
                        Text = day.Text
                    });
                }

                return weatherReport;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return null;
        }

        private static WttrCore.Report GetReport(string urlLocation, CultureInfo culture, TemperatureScale tempScale, WindSpeedScale windSpeedScale)
        {
            try
            {
                var lang = culture.TwoLetterISOLanguageName.ToLowerInvariant();
                var json = GetWebPageContent(string.Format(WttrCore.ReportUrl, urlLocation, lang));
                if (string.IsNullOrEmpty(json))
                    return null;
                return WttrCore.ParseReport(json, tempScale == TemperatureScale.Fahrenheit, (int)windSpeedScale, lang);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }

        /// <summary>
        /// j1 has no is_day flag, so day/night is decided the same way the MSN provider does:
        /// astronomical sunrise/sunset for the location against the widget's timezone
        /// </summary>
        private static bool IsDay(double lat, double lon, TimeSpan baseUtcOffset)
        {
            try
            {
                if (lat == 0 && lon == 0)
                    return true;
                var sc = new SunCalculator(DateTime.Today, lat, lon);
                var userDateTime = DateTime.Now.ToUniversalTime().Add(baseUtcOffset);
                return userDateTime > sc.DSunRise && userDateTime < sc.DSunSet;
            }
            catch
            {
                return true;
            }
        }

        private static string GetWebPageContent(string url)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    if (GeneralHelper.Proxy != null)
                        client.Proxy = GeneralHelper.Proxy;
                    return client.DownloadString(url);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return string.Empty;
            }
        }
    }
}
