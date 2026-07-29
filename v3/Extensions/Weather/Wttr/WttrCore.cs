using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace WttrIn
{
    /// <summary>
    /// Pure logic: urls, location code format, j1 json parsing and WWO code mapping.
    /// No WPF or Weather.Base dependencies so it can be unit-tested cross-platform.
    /// </summary>
    internal static class WttrCore
    {
        //https://github.com/chubin/wttr.in — free, no API key; j1 = JSON, lang = localized descriptions
        internal const string ReportUrl = "https://wttr.in/{0}?format=j1&lang={1}";

        internal class Report
        {
            public string City;
            public string Country;
            public double Lat;
            public double Lon;
            public int Temperature;
            public int FeelsLike;
            public int Humidity;
            public int WindSpeed;
            public int WeatherCode;
            public string Text;
            public List<ReportDay> Days = new List<ReportDay>();
        }

        internal class ReportDay
        {
            public int HighTemperature;
            public int LowTemperature;
            public int WeatherCode;
            public string Text;
        }

        internal static string FormatCoordinate(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Location code stored in widget settings: "lat,lon,city" in invariant culture
        /// (same format as the OpenMeteo provider). The city name rides inside the code
        /// because the widget only persists the code between restarts.
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

        /// <summary>
        /// Parses the ?format=j1 response. wttr.in resolves the query to its best-match
        /// location itself (nearest_area), so the report carries the resolved place too.
        /// </summary>
        internal static Report ParseReport(string json, bool fahrenheit, int windUnit, string twoLetterLang)
        {
            var o = JObject.Parse(json);

            var current = (o["current_condition"] as JArray)?[0];
            if (current == null)
                return null;

            var report = new Report();
            report.Temperature = (int)current[fahrenheit ? "temp_F" : "temp_C"];
            report.FeelsLike = (int)current[fahrenheit ? "FeelsLikeF" : "FeelsLikeC"];
            report.Humidity = (int)current["humidity"];
            report.WindSpeed = ConvertWind(current, windUnit);
            report.WeatherCode = (int)current["weatherCode"];
            report.Text = GetLocalizedText(current, twoLetterLang);

            var area = (o["nearest_area"] as JArray)?[0];
            if (area != null)
            {
                report.City = (string)(area["areaName"] as JArray)?[0]?["value"];
                report.Country = (string)(area["country"] as JArray)?[0]?["value"];
                double lat, lon;
                if (double.TryParse((string)area["latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out lat))
                    report.Lat = lat;
                if (double.TryParse((string)area["longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
                    report.Lon = lon;
            }

            var days = o["weather"] as JArray;
            if (days == null)
                return report;

            foreach (var day in days)
            {
                var reportDay = new ReportDay();
                reportDay.HighTemperature = (int)day[fahrenheit ? "maxtempF" : "maxtempC"];
                reportDay.LowTemperature = (int)day[fahrenheit ? "mintempF" : "mintempC"];

                //no day-level condition in j1: take the midday slot as representative
                var hourly = day["hourly"] as JArray;
                var noon = hourly != null && hourly.Count > 4 ? hourly[4] : hourly != null && hourly.Count > 0 ? hourly[hourly.Count / 2] : null;
                if (noon != null)
                {
                    reportDay.WeatherCode = (int)noon["weatherCode"];
                    reportDay.Text = GetLocalizedText(noon, twoLetterLang);
                }
                else
                {
                    reportDay.WeatherCode = report.WeatherCode;
                    reportDay.Text = report.Text;
                }

                report.Days.Add(reportDay);
            }

            return report;
        }

        private static int ConvertWind(JToken current, int windUnit)
        {
            //0 = mph, 1 = km/h, 2 = m/s (see Weather.Base.WindSpeedScale)
            if (windUnit == 0)
                return (int)current["windspeedMiles"];
            var kmh = (int)current["windspeedKmph"];
            if (windUnit == 2)
                return (int)Math.Round(kmh / 3.6);
            return kmh;
        }

        private static string GetLocalizedText(JToken condition, string twoLetterLang)
        {
            if (!string.IsNullOrEmpty(twoLetterLang) && !string.Equals(twoLetterLang, "en", StringComparison.OrdinalIgnoreCase))
            {
                var localized = (string)(condition["lang_" + twoLetterLang.ToLowerInvariant()] as JArray)?[0]?["value"];
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            return (string)(condition["weatherDesc"] as JArray)?[0]?["value"] ?? string.Empty;
        }

        /// <summary>
        /// Maps WWO weather codes (wttr.in upstream) to internal weather pic numbers
        /// (see UIFramework.Weather images and WeatherConverter.ConvertSkyCodeToWeatherState).
        /// Code list: https://www.worldweatheronline.com/developer/api/docs/weather-icons.aspx
        /// </summary>
        internal static int GetWeatherPic(int wwoCode, bool isDay)
        {
            switch (wwoCode)
            {
                case 113: //sunny / clear
                    return isDay ? 1 : 33;
                case 116: //partly cloudy
                    return isDay ? 2 : 34;
                case 119: //cloudy
                    return isDay ? 4 : 36;
                case 122: //overcast
                    return 8;
                case 143: //mist
                case 248: //fog
                case 260: //freezing fog
                    return isDay ? 11 : 37;
                case 263: //patchy light drizzle
                case 266: //light drizzle
                case 281: //freezing drizzle
                case 284: //heavy freezing drizzle
                case 185: //patchy freezing drizzle possible
                case 293: //patchy light rain
                case 296: //light rain
                case 299: //moderate rain at times
                case 302: //moderate rain
                    return 12;
                case 176: //patchy rain possible
                case 353: //light rain shower
                    return isDay ? 13 : 40;
                case 305: //heavy rain at times
                case 308: //heavy rain
                case 311: //light freezing rain
                case 314: //moderate or heavy freezing rain
                case 356: //moderate or heavy rain shower
                case 359: //torrential rain shower
                    return 18;
                case 179: //patchy snow possible
                case 182: //patchy sleet possible
                case 317: //light sleet
                case 320: //moderate or heavy sleet
                case 323: //patchy light snow
                case 326: //light snow
                case 329: //patchy moderate snow
                case 332: //moderate snow
                case 350: //ice pellets
                case 362: //light sleet showers
                case 365: //moderate or heavy sleet showers
                case 368: //light snow showers
                case 374: //light showers of ice pellets
                    return 22;
                case 227: //blowing snow
                case 230: //blizzard
                case 335: //patchy heavy snow
                case 338: //heavy snow
                case 371: //moderate or heavy snow showers
                case 377: //moderate or heavy showers of ice pellets
                    return 24;
                case 200: //thundery outbreaks possible
                case 386: //patchy light rain with thunder
                case 389: //moderate or heavy rain with thunder
                case 392: //patchy light snow with thunder
                case 395: //moderate or heavy snow with thunder
                    return 15;
                default:
                    return isDay ? 1 : 33;
            }
        }
    }
}
