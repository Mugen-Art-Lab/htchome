using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Home.Base;
using Weather.Base;
using System.Diagnostics;

namespace OpenMeteo
{
    public class WeatherProvider : IWeatherProvider
    {
        static WeatherProvider()
        {
            try
            {
                //Win7 ships with TLS 1.0 by default and open-meteo.com is https-only
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

            var url = string.Format(OpenMeteoCore.GeocodingUrl, Uri.EscapeDataString(query), culture.TwoLetterISOLanguageName.ToLowerInvariant());
            var json = GetWebPageContent(url);
            if (string.IsNullOrEmpty(json))
                return result;

            foreach (var geo in OpenMeteoCore.ParseGeocodingResults(json))
            {
                result.Add(new LocationData
                {
                    City = geo.City,
                    Country = geo.Country,
                    Lat = geo.Lat,
                    Lon = geo.Lon,
                    Code = geo.Code
                });
            }

            AddWeatherToSearchResults(result, tempScale);

            return result;
        }

        /// <summary>
        /// Fills in current temperature and icon for the location search results (single batch request)
        /// </summary>
        private static void AddWeatherToSearchResults(List<LocationData> locations, TemperatureScale tempScale)
        {
            if (locations.Count == 0)
                return;

            try
            {
                var lats = string.Join(",", locations.Select(x => OpenMeteoCore.FormatCoordinate(x.Lat)));
                var lons = string.Join(",", locations.Select(x => OpenMeteoCore.FormatCoordinate(x.Lon)));
                var unit = tempScale == TemperatureScale.Fahrenheit ? "fahrenheit" : "celsius";

                var json = GetWebPageContent(string.Format(OpenMeteoCore.SearchResultsWeatherUrl, lats, lons, unit));
                if (string.IsNullOrEmpty(json))
                    return;

                var conditions = OpenMeteoCore.ParseSearchResultsWeather(json);
                for (int i = 0; i < locations.Count && i < conditions.Count; i++)
                {
                    if (conditions[i] == null)
                        continue;
                    locations[i].Temperature = conditions[i].Temperature;
                    locations[i].Skycode = conditions[i].SkyCode;
                }
            }
            catch (Exception ex)
            {
                //search must still work without temperatures in the result list
                Debug.WriteLine(ex);
            }
        }

        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale,
            WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            try
            {
                double lat, lon;
                string embeddedCity;
                if (!OpenMeteoCore.TryParseLocationCode(location.Code, out lat, out lon, out embeddedCity))
                    return null;

                //the widget passes a code-only location on refresh; restore the label from the code
                //(the same way MSN restores it from its response, but Open-Meteo reports no place names)
                if (string.IsNullOrEmpty(location.City) && !string.IsNullOrEmpty(embeddedCity))
                    location.City = embeddedCity;

                var tempUnit = tempScale == TemperatureScale.Fahrenheit ? "fahrenheit" : "celsius";
                var windUnit = windSpeedScale == WindSpeedScale.Mph ? "mph" : windSpeedScale == WindSpeedScale.Ms ? "ms" : "kmh";

                var url = string.Format(OpenMeteoCore.ForecastUrl,
                    OpenMeteoCore.FormatCoordinate(lat), OpenMeteoCore.FormatCoordinate(lon), tempUnit, windUnit);

                var json = GetWebPageContent(url);
                if (string.IsNullOrEmpty(json))
                    return null;

                var report = OpenMeteoCore.ParseForecast(json, culture.TwoLetterISOLanguageName);
                if (report == null)
                    return null;

                var weatherReport = new WeatherData();
                weatherReport.Location = location;
                weatherReport.Temperature = report.Temperature;
                weatherReport.FeelsLike = report.FeelsLike;
                weatherReport.Humidity = report.Humidity;
                weatherReport.WindSpeed = report.WindSpeed;
                weatherReport.Curent = new ForecastData
                {
                    Text = report.Text,
                    SkyCode = report.SkyCode
                };

                weatherReport.ForecastList = report.Days.Select(day => new ForecastData
                {
                    HighTemperature = day.HighTemperature,
                    LowTemperature = day.LowTemperature,
                    SkyCode = day.SkyCode,
                    Text = day.Text
                }).ToList();

                return weatherReport;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return null;
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
