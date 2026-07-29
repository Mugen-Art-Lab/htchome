using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace OpenMeteo
{
    /// <summary>
    /// Pure logic: urls, location code format, json parsing, WMO code mapping and captions.
    /// No WPF or Weather.Base dependencies so it can be unit-tested cross-platform.
    /// </summary>
    internal static class OpenMeteoCore
    {
        //https://open-meteo.com/en/docs — free for non-commercial use, no API key
        internal const string GeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search?name={0}&count=8&language={1}&format=json";
        internal const string ForecastUrl = "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code,is_day&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=6&timezone=auto&temperature_unit={2}&wind_speed_unit={3}";
        internal const string SearchResultsWeatherUrl = "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,weather_code,is_day&temperature_unit={2}";

        internal class GeoLocation
        {
            public string City;
            public string Country;
            public double Lat;
            public double Lon;
            public string Code;
        }

        internal class CurrentConditions
        {
            public int Temperature;
            public int SkyCode;
        }

        internal class Report
        {
            public int Temperature;
            public int FeelsLike;
            public int Humidity;
            public int WindSpeed;
            public string Text;
            public int SkyCode;
            public List<ReportDay> Days = new List<ReportDay>();
        }

        internal class ReportDay
        {
            public int HighTemperature;
            public int LowTemperature;
            public string Text;
            public int SkyCode;
        }

        internal static string FormatCoordinate(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Location code stored in widget settings: "lat,lon,city" in invariant culture,
        /// e.g. "52.374,4.8897,Амстердам". The city name rides inside the code because
        /// the widget only persists the code, and the forecast response has no place name
        /// to restore the location label from after a restart (unlike MSN).
        /// </summary>
        internal static string MakeLocationCode(double lat, double lon, string city)
        {
            var code = FormatCoordinate(lat) + "," + FormatCoordinate(lon);
            if (!string.IsNullOrEmpty(city))
                code += "," + city;
            return code;
        }

        internal static bool TryParseLocationCode(string code, out double lat, out double lon, out string city)
        {
            lat = 0;
            lon = 0;
            city = null;
            if (string.IsNullOrEmpty(code))
                return false;
            var parts = code.Split(',');
            if (parts.Length < 2)
                return false;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
                return false;
            if (parts.Length > 2)
                city = string.Join(",", parts, 2, parts.Length - 2); //city names may contain commas
            return true;
        }

        internal static List<GeoLocation> ParseGeocodingResults(string json)
        {
            var result = new List<GeoLocation>();

            var o = JObject.Parse(json);
            var results = o["results"] as JArray;
            if (results == null)
                return result;

            foreach (var el in results)
            {
                var l = new GeoLocation();
                l.City = (string)el["name"];
                l.Country = (string)el["country"] ?? (string)el["admin1"];
                l.Lat = (double)el["latitude"];
                l.Lon = (double)el["longitude"];
                l.Code = MakeLocationCode(l.Lat, l.Lon, l.City);
                result.Add(l);
            }

            return result;
        }

        /// <summary>
        /// Parses the batch current-weather response for search results.
        /// A single location comes back as an object, multiple locations as an array.
        /// </summary>
        internal static List<CurrentConditions> ParseSearchResultsWeather(string json)
        {
            var result = new List<CurrentConditions>();

            var token = JToken.Parse(json);
            var reports = token as JArray ?? new JArray(token);

            foreach (var report in reports)
            {
                var current = report["current"];
                if (current == null)
                {
                    result.Add(null);
                    continue;
                }
                result.Add(new CurrentConditions
                {
                    Temperature = (int)Math.Round((double)current["temperature_2m"]),
                    SkyCode = GetWeatherPic((int)current["weather_code"], (int)current["is_day"] == 1)
                });
            }

            return result;
        }

        internal static Report ParseForecast(string json, string twoLetterLang)
        {
            var o = JObject.Parse(json);

            var current = o["current"];
            if (current == null)
                return null;

            var currentCode = (int)current["weather_code"];
            var isDay = (int)current["is_day"] == 1;

            var report = new Report();
            report.Temperature = (int)Math.Round((double)current["temperature_2m"]);
            report.FeelsLike = (int)Math.Round((double)current["apparent_temperature"]);
            report.Humidity = (int)Math.Round((double)current["relative_humidity_2m"]);
            report.WindSpeed = (int)Math.Round((double)current["wind_speed_10m"]);
            report.Text = GetCaption(currentCode, twoLetterLang);
            report.SkyCode = GetWeatherPic(currentCode, isDay);

            var daily = o["daily"];
            var codes = daily?["weather_code"] as JArray;
            var highs = daily?["temperature_2m_max"] as JArray;
            var lows = daily?["temperature_2m_min"] as JArray;
            if (codes == null || highs == null || lows == null)
                return report;

            for (int i = 0; i < codes.Count && i < 6; i++)
            {
                var dayCode = (int)codes[i];
                report.Days.Add(new ReportDay
                {
                    HighTemperature = (int)Math.Round((double)highs[i]),
                    LowTemperature = (int)Math.Round((double)lows[i]),
                    SkyCode = GetWeatherPic(dayCode, true),
                    Text = GetCaption(dayCode, twoLetterLang)
                });
            }

            return report;
        }

        /// <summary>
        /// Maps WMO weather interpretation codes (WW) to internal weather pic numbers
        /// (see UIFramework.Weather images and WeatherConverter.ConvertSkyCodeToWeatherState)
        /// </summary>
        internal static int GetWeatherPic(int wmoCode, bool isDay)
        {
            switch (wmoCode)
            {
                case 0: //clear sky
                    return isDay ? 1 : 33;
                case 1: //mainly clear
                    return isDay ? 2 : 34;
                case 2: //partly cloudy
                    return isDay ? 4 : 36;
                case 3: //overcast
                    return 8;
                case 45: //fog
                case 48: //depositing rime fog
                    return isDay ? 11 : 37;
                case 51: //drizzle: light
                case 53: //drizzle: moderate
                case 55: //drizzle: dense
                case 56: //freezing drizzle: light
                case 57: //freezing drizzle: dense
                case 61: //rain: slight
                case 63: //rain: moderate
                    return 12;
                case 80: //rain showers: slight
                case 81: //rain showers: moderate
                    return isDay ? 13 : 40;
                case 65: //rain: heavy
                case 66: //freezing rain: light
                case 67: //freezing rain: heavy
                case 82: //rain showers: violent
                    return 18;
                case 71: //snow fall: slight
                case 73: //snow fall: moderate
                case 77: //snow grains
                case 85: //snow showers: slight
                    return 22;
                case 75: //snow fall: heavy
                case 86: //snow showers: heavy
                    return 24;
                case 95: //thunderstorm
                case 96: //thunderstorm with slight hail
                case 99: //thunderstorm with heavy hail
                    return 15;
                default:
                    return isDay ? 1 : 33;
            }
        }

        /// <summary>
        /// Open-Meteo returns no condition texts, so captions are generated locally (en/ru)
        /// </summary>
        internal static string GetCaption(int wmoCode, string twoLetterLang)
        {
            var ru = string.Equals(twoLetterLang, "ru", StringComparison.OrdinalIgnoreCase);
            switch (wmoCode)
            {
                case 0:
                    return ru ? "Ясно" : "Clear";
                case 1:
                    return ru ? "Преимущественно ясно" : "Mainly clear";
                case 2:
                    return ru ? "Переменная облачность" : "Partly cloudy";
                case 3:
                    return ru ? "Пасмурно" : "Overcast";
                case 45:
                case 48:
                    return ru ? "Туман" : "Fog";
                case 51:
                case 53:
                case 55:
                    return ru ? "Морось" : "Drizzle";
                case 56:
                case 57:
                    return ru ? "Ледяная морось" : "Freezing drizzle";
                case 61:
                    return ru ? "Небольшой дождь" : "Light rain";
                case 63:
                    return ru ? "Дождь" : "Rain";
                case 65:
                    return ru ? "Сильный дождь" : "Heavy rain";
                case 66:
                case 67:
                    return ru ? "Ледяной дождь" : "Freezing rain";
                case 71:
                    return ru ? "Небольшой снег" : "Light snow";
                case 73:
                    return ru ? "Снег" : "Snow";
                case 75:
                    return ru ? "Сильный снег" : "Heavy snow";
                case 77:
                    return ru ? "Снежные зёрна" : "Snow grains";
                case 80:
                case 81:
                    return ru ? "Ливневый дождь" : "Rain showers";
                case 82:
                    return ru ? "Сильный ливень" : "Violent rain showers";
                case 85:
                case 86:
                    return ru ? "Снегопад" : "Snow showers";
                case 95:
                    return ru ? "Гроза" : "Thunderstorm";
                case 96:
                case 99:
                    return ru ? "Гроза с градом" : "Thunderstorm with hail";
                default:
                    return string.Empty;
            }
        }
    }
}
