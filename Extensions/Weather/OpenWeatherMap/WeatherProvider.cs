using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Home.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenWeatherMap.Forecast;
using Weather.Base;

namespace OpenWeatherMap
{
    public class WeatherProvider : IWeatherProvider
    {
        private const string RequestForLocationC = "http://api.openweathermap.org/data/2.5/find?q={0}&lang={1}&units=metric";
        private const string RequestForLocationH = "http://api.openweathermap.org/data/2.5/find?q={0}&lang={1}&units=imperial";
        private const string RequestForCelsius = "http://api.openweathermap.org/data/2.5/forecast/daily?lang={0}&id={1}&cnt=6&mode=json&units=metric";
        private const string RequestForFahrenheit = "http://api.openweathermap.org/data/2.5/forecast/daily?lang={0}&id={1}&cnt=6&mode=json&units=imperial";

        public List<LocationData> GetLocations(string query, CultureInfo culture)
        {
            var json = GeneralHelper.GetWebPageContent(string.Format(RequestForLocationC, query, culture.Name));
            if (string.IsNullOrEmpty(json))
                return null;

            var o = JObject.Parse(json);
            if (o["list"] == null)
                return null;

            var cities = JsonConvert.DeserializeObject<List<City>>(o["list"].ToString());
            return cities.Select(c => new LocationData() { Code = c.Id.ToString(), City = c.Name, Country = c.Sys?.Country, Temperature = c.Main != null ? (int)c.Main.Temp : 0, Skycode = GetWeatherPic(c.Weather.FirstOrDefault()?.Icon) }).ToList();
        }

        public List<LocationData> GetLocations(string query, CultureInfo culture, TemperatureScale tempScale)
        {
            var json = GeneralHelper.GetWebPageContent(string.Format(tempScale == TemperatureScale.Celsius ? RequestForLocationC : RequestForLocationH, query, culture.Name));
            if (string.IsNullOrEmpty(json))
                return null;

            var o = JObject.Parse(json);
            if (o["list"] == null)
                return null;

            var cities = JsonConvert.DeserializeObject<List<City>>(o["list"].ToString());
            return cities.Select(c => new LocationData() { Code = c.Id.ToString(), City = c.Name, Country = c.Sys?.Country, Temperature = c.Main != null ? (int)c.Main.Temp : 0, Skycode = GetWeatherPic(c.Weather.FirstOrDefault()?.Icon) }).ToList();
        }

        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale,
            WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            var url = string.Format(tempScale == TemperatureScale.Celsius ? RequestForCelsius : RequestForFahrenheit, culture.TwoLetterISOLanguageName, location.Code);
            var json = GeneralHelper.GetWebPageContent(url);
            if (string.IsNullOrEmpty(json))
                return null;

            var o = JObject.Parse(json);
            if (o["list"] == null)
                return null;
            var forecast = JsonConvert.DeserializeObject<ForecastResponse>(json);
            var reports = forecast.List;
            if (reports == null || reports.Count == 0)
                return null;

            var currentReport = reports.First();

            var result = new WeatherData();
            result.Curent = new ForecastData()
            {
                HighTemperature = (int)currentReport.Temp.Max,
                LowTemperature = (int)currentReport.Temp.Min,
                Text = currentReport.Weather.FirstOrDefault()?.Description,
                SkyCode = GetWeatherPic(currentReport.Weather.FirstOrDefault()?.Icon)
            };
            result.ForecastList = new List<ForecastData>();
            foreach (var report in reports.Skip(1))
            {
                result.ForecastList.Add(new ForecastData()
                {
                    HighTemperature = (int)report.Temp.Max,
                    LowTemperature = (int)report.Temp.Min,
                    Text = report.Weather.FirstOrDefault()?.Description,
                    SkyCode = GetWeatherPic(report.Weather.FirstOrDefault()?.Icon)
                });
            }
            result.Location = new LocationData()
            {
                Code = forecast.City.Id.ToString(),
                City = forecast.City.Name,
                Country = forecast.City.Sys?.Country
            };
            result.Temperature = (int)currentReport.Temp.Day;
            result.FeelsLike = result.Temperature;
            result.Humidity = (int)currentReport.Humidity;
            result.WindSpeed = (int)currentReport.Speed;
            return result;
        }

        private static int GetWeatherPic(string skycode)
        {
            if (string.IsNullOrEmpty(skycode))
                return 1;

            switch (skycode)
            {
                case "01d":
                case "01n":
                    return 1;

                case "02d":
                case "02n":
                    return 2;

                case "03d":
                case "03n":
                    return 7;

                case "04d":
                case "04n":
                    return 8;

                case "09d":
                case "09n":
                    return 12;

                case "10d":
                case "10n":
                    return 13;

                case "11d":
                case "11n":
                    return 15;

                case "13d":
                case "13n":
                    return 19;

                case "50d":
                case "50n":
                    return 11;

                default:
                    return 1;
            }
        }
    }
}
