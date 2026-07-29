using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Home.Base;
using Weather.Base;
using System.Diagnostics;
using MSN.Forecast;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MSN
{
    public class WeatherProvider : IWeatherProvider
    {
        private const string RequestForLocation = "https://weather.service.msn.com/find.aspx?weasearchstr={0}&culture={1}&src=outlook";
        private const string RequestForCelsius = "https://service.weather.microsoft.com/{0}/weather/current/{1}?units=C";//"http://weather.service.msn.com/data.aspx?culture={0}&wealocations={1}&weadegreetype=C&src=outlook";
        private const string RequestForFahrenheit = "https://service.weather.microsoft.com/{0}/weather/current/{1}?units=F";//"http://weather.service.msn.com/data.aspx?culture={0}&wealocations={1}&weadegreetype=F&src=outlook";
        private const string ForecastUrlForCelsius = "https://service.weather.microsoft.com/{0}/weather/forecast/daily/{1}?units=C&nl=true";//"http://weather.msn.com/local.aspx?wealocations={0}&weadegreetype=C&src=outlook";
        private const string ForecastUrlForFahrenheit = "https://service.weather.microsoft.com/{0}/weather/forecast/daily/{1}?units=F&nl=true";//"http://weather.service.msn.com/local.aspx?wealocations={0}&weadegreetype=F&src=outlook";

        public List<LocationData> GetLocations(string query, CultureInfo culture)
        {
            var result = new List<LocationData>();

            var xml = XElement.Parse(GeneralHelper.GetWebPageContent(string.Format(RequestForLocation, query, culture.Name)));
            var resultTempsScale = TemperatureScale.Null;
            foreach (var el in xml.Elements("weather"))
            {
                var l = new LocationData();
                var locString = el.Attribute("weatherlocationname")?.Value;
                if (string.IsNullOrEmpty(locString))
                    continue;
                if (locString.Contains(","))
                {
                    l.City = locString.Substring(0, locString.IndexOf(","));
                    l.Country = locString.Substring(locString.IndexOf(",") + 2);
                }
                else
                    l.City = locString;
                l.Code = el.Attribute("weatherlocationcode").Value;
                if (!string.IsNullOrEmpty(l.City))
                    l.Code += "|" + l.City; //embed the picked name so GetWeatherReport can restore the label (issue #4)
                if (resultTempsScale == TemperatureScale.Null)
                {
                    var scale = el.Attribute("degreetype").Value;
                    if (scale.ToLower() == "c")
                        resultTempsScale = TemperatureScale.Celsius;
                    else
                        resultTempsScale = TemperatureScale.Fahrenheit;
                }
                try
                {
                    l.Temperature = Convert.ToInt32(el.Element("current").Attribute("temperature").Value);
                    if (Shared.TemperatureScale == TemperatureScale.Celsius)
                        l.Temperature = (int)WeatherConverter.FahrenheitToCelsius(l.Temperature, resultTempsScale);
                    else
                        l.Temperature = (int)WeatherConverter.CelsiusToFahrenheit(l.Temperature, resultTempsScale);
                    //l.Temperature = Convert.ToInt32(el.Element("current").Attribute("temperature").Value);
                    l.Skycode = Convert.ToInt32(el.Element("current").Attribute("skycode").Value);
                    l.Skycode = GetWeatherPic(Convert.ToInt32(l.Skycode));
                }
                catch { }
                result.Add(l);
            }
            return result;
        }

        public List<LocationData> GetLocations(string query, CultureInfo culture, TemperatureScale tempScale)
        {
            var result = new List<LocationData>();

            var xml = XElement.Parse(GeneralHelper.GetWebPageContent(string.Format(RequestForLocation, query, culture.Name)));
            var resultTempsScale = TemperatureScale.Null;
            foreach (var el in xml.Elements("weather"))
            {
                var l = new LocationData();
                var locString = el.Attribute("weatherlocationname")?.Value;
                if (string.IsNullOrEmpty(locString))
                    continue;
                if (locString.Contains(","))
                {
                    l.City = locString.Substring(0, locString.IndexOf(","));
                    l.Country = locString.Substring(locString.IndexOf(",") + 2);
                }
                else
                    l.City = locString;
                l.Code = el.Attribute("weatherlocationcode").Value;
                if (!string.IsNullOrEmpty(l.City))
                    l.Code += "|" + l.City; //embed the picked name so GetWeatherReport can restore the label (issue #4)
                //if (resultTempsScale == TemperatureScale.Null)
                //{
                var scale = el.Attribute("degreetype").Value;
                if (scale.ToLower() == "c")
                    resultTempsScale = TemperatureScale.Celsius;
                else
                    resultTempsScale = TemperatureScale.Fahrenheit;
                //}
                try
                {
                    l.Temperature = Convert.ToInt32(el.Element("current").Attribute("temperature").Value);
                    if (tempScale == TemperatureScale.Celsius)
                        l.Temperature = (int)WeatherConverter.FahrenheitToCelsius(l.Temperature, resultTempsScale);
                    else
                        l.Temperature = (int)WeatherConverter.CelsiusToFahrenheit(l.Temperature, resultTempsScale);
                    l.Skycode = Convert.ToInt32(el.Element("current").Attribute("skycode").Value);
                    l.Skycode = GetWeatherPic(Convert.ToInt32(l.Skycode));
                }
                catch { }
                result.Add(l);
            }
            return result;
        }

        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale,
            WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            try
            {
                //the code may carry the user-picked city name after '|' (see GetLocations);
                //MSN resolves wc-codes to its nearest station city (often transliterated), so prefer the picked name
                var locationCode = location.Code;
                string pickedCity = null;
                var separatorIndex = locationCode == null ? -1 : locationCode.IndexOf('|');
                if (separatorIndex >= 0)
                {
                    pickedCity = locationCode.Substring(separatorIndex + 1);
                    locationCode = locationCode.Substring(0, separatorIndex);
                }

                var url = string.Format(tempScale == TemperatureScale.Celsius ? RequestForCelsius : RequestForFahrenheit, culture.Name, locationCode);

                var json = GeneralHelper.GetWebPageContent(url);
                if (string.IsNullOrEmpty(json))
                    return null;

                var o = JObject.Parse(json);

                if (o.SelectToken("responses[0].weather[0].current") == null)
                    return null;

                var current = JsonConvert.DeserializeObject<Forecast.Weather>(o.SelectToken("responses[0].weather[0].current").ToString());

                var weatherReport = new WeatherData();

                weatherReport.Temperature = (int)current.Temp;
                weatherReport.FeelsLike = (int)current.Feels;
                weatherReport.Humidity = (int)current.Rh;
                weatherReport.Curent = new ForecastData();
                weatherReport.Curent.Text = current.Cap;
                weatherReport.Curent.SkyCode = current.Icon;

                if (!string.IsNullOrEmpty(pickedCity))
                    weatherReport.Location.City = pickedCity;

                if (o.SelectToken("responses[0].source") != null)
                {
                    var source = JsonConvert.DeserializeObject<Source>(o.SelectToken("responses[0].source").ToString());

                    var sunrise = DateTime.Today.AddHours(4);
                    var sunset = DateTime.Today.AddHours(22);
                    if (source.Coordinates.Lat != Double.MinValue && source.Coordinates.Lon != Double.MinValue)
                    {
                        var sc = new SunCalculator(DateTime.Today, source.Coordinates.Lat, source.Coordinates.Lon);
                        sunrise = sc.DSunRise;
                        sunset = sc.DSunSet;
                    }
                    weatherReport.Curent.SkyCode = GetWeatherPic(current.Icon, sunrise, sunset, baseUtcOffset);
                    if (weatherReport.Location != null && string.IsNullOrEmpty(weatherReport.Location.City))
                        weatherReport.Location.City = source.Location?.Name;
                }
                
                if (weatherReport.Location == null)
                    weatherReport.Location = location;

                url = string.Format(tempScale == TemperatureScale.Celsius ? ForecastUrlForCelsius : ForecastUrlForFahrenheit, culture.Name, locationCode);

                json = GeneralHelper.GetWebPageContent(url);
                if (string.IsNullOrEmpty(json))
                    return weatherReport;

                o = JObject.Parse(json);

                if (o.SelectToken("responses[0].weather[0].days") == null)
                    return weatherReport;

                var forecastData = JsonConvert.DeserializeObject<List<Day>>(o.SelectToken("responses[0].weather[0].days").ToString());
                weatherReport.ForecastList = new List<ForecastData>();

                for (int i = 0; i < forecastData.Count && i < 5; i++)
                {
                    var f = forecastData[i];

                    var forecast = new ForecastData();
                    forecast.HighTemperature = (int)f.Daily.TempHi;
                    forecast.LowTemperature = (int)f.Daily.TempLo;
                    forecast.SkyCode = GetWeatherPic(f.Daily.Icon);
                    forecast.Text = f.Daily.Day.Cap;
                    weatherReport.ForecastList.Add(forecast);
                }

                return weatherReport;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return null;
        }

        //public WeatherData GetWeatherReportOld(CultureInfo culture, LocationData location, TemperatureScale tempScale, WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        //{
        //    try
        //    {


        //        var url = string.Format(tempScale == TemperatureScale.Celsius ? RequestForCelsius : RequestForFahrenheit, culture.Name, location.Code);

        //        var content = GeneralHelper.GetWebPageContent(url);
        //        if (string.IsNullOrEmpty(content))
        //            return null;
        //        var doc = XDocument.Parse(content);

        //        parse current weather
        //        var weather = from x in doc.Descendants("weather")
        //                      let xElement = x.Element("current")
        //                      where xElement != null
        //                      select
        //                          new
        //                          {
        //                              feelslike = xElement.Attribute("feelslike").Value,
        //                              windspeed = xElement.Attribute("windspeed").Value,
        //                              humidity = xElement.Attribute("humidity").Value,
        //                              temp = xElement.Attribute("temperature").Value,
        //                              text = xElement.Attribute("skytext").Value,
        //                              skycode = xElement.Attribute("skycode").Value,
        //                              windstring = xElement.Attribute("winddisplay").Value
        //                          };

        //        var result = new WeatherData();
        //        var currentWeather = weather.FirstOrDefault();
        //        if (currentWeather == null)
        //            return null;

        //        var t = 0;
        //        int.TryParse(currentWeather.temp, out t);
        //        result.Temperature = t;

        //        int.TryParse(currentWeather.feelslike, out t);
        //        result.FeelsLike = t;

        //        int.TryParse(currentWeather.humidity, out t);
        //        result.Humidity = t;
        //        var wScale = DetectWindSpeedScale(currentWeather.windstring);
        //        var wind = currentWeather.windspeed;
        //        if (!string.IsNullOrEmpty(wind))
        //        {
        //            switch (windSpeedScale)
        //            {
        //                case WindSpeedScale.Mph:
        //                    result.WindSpeed = (int)Math.Round(WeatherConverter.WindSpeedConvertToMph(Convert.ToInt32(currentWeather.windspeed), wScale), 0);
        //                    break;
        //                case WindSpeedScale.Kmh:
        //                    result.WindSpeed = (int)Math.Round(WeatherConverter.WindSpeedConvertToKmh(Convert.ToInt32(currentWeather.windspeed), wScale), 0);
        //                    break;
        //                case WindSpeedScale.Ms:
        //                    result.WindSpeed = (int)Math.Round(WeatherConverter.WindSpeedConvertToMs(Convert.ToInt32(currentWeather.windspeed), wScale), 0);
        //                    break;

        //            }
        //        }
        //        else result.WindSpeed = 0;

        //        result.Curent = new ForecastData()
        //                            {
        //                                Text = currentWeather.text,
        //                                SkyCode = GetWeatherPic(Convert.ToInt32(currentWeather.skycode), 4, 22)
        //                            };

        //        result.Location = location;
        //        if (doc.Descendants("weather").FirstOrDefault() == null)
        //            return null;
        //        var locString = doc.Descendants("weather").FirstOrDefault().Attribute("weatherlocationname").Value;
        //        if (locString.Contains(","))
        //        {
        //            result.Location.City = locString.Substring(0, locString.IndexOf(","));
        //            result.Location.Country = locString.Substring(locString.IndexOf(",") + 2);
        //        }
        //        else
        //        {
        //            result.Location.City = locString;
        //        }

        //        parse coordinates
        //        var coords = from x in doc.Descendants("weather")
        //                     select new
        //                     {
        //                         lon = x.Attribute("long").Value,
        //                         lat = x.Attribute("lat").Value
        //                     };
        //        double lat, lon = double.MinValue;
        //        double.TryParse(coords.FirstOrDefault().lat, NumberStyles.Float, CultureInfo.GetCultureInfo("en-US").NumberFormat, out lat);
        //        double.TryParse(coords.FirstOrDefault().lon, NumberStyles.Float, CultureInfo.GetCultureInfo("en-US").NumberFormat, out lon);
        //        result.Location.Lat = lat;
        //        result.Location.Lon = lon;

        //        var sunrise = DateTime.Today.AddHours(4);
        //        var sunset = DateTime.Today.AddHours(22);
        //        if (lat != Double.MinValue && lon != Double.MinValue)
        //        {
        //            var sc = new SunCalculator(DateTime.Today, lat, lon);
        //            sunrise = sc.DSunRise;
        //            sunset = sc.DSunSet;
        //        }
        //        result.Curent.SkyCode = GetWeatherPic(Convert.ToInt32(currentWeather.skycode), sunrise, sunset, baseUtcOffset);

        //        parse forecast
        //        var days = from x in doc.Descendants("forecast")
        //                   select
        //                       new
        //                       {
        //                           l = x.Attribute("low").Value,
        //                           h = x.Attribute("high").Value,
        //                           skycode = x.Attribute("skycodeday").Value,
        //                           text = x.Attribute("skytextday").Value
        //                       };

        //        var forecastUrl = string.Format(tempScale == TemperatureScale.Celsius ? ForecastUrlForCelsius : ForecastUrlForFahrenheit, location.Code);

        //        var f = new List<ForecastData>();
        //        foreach (var d in days)
        //        {
        //            f.Add(new ForecastData());
        //            var temp = 0;
        //            int.TryParse(d.h, out temp);
        //            f[f.Count - 1].HighTemperature = temp;
        //            int.TryParse(d.l, out temp);
        //            f[f.Count - 1].LowTemperature = temp;
        //            f[f.Count - 1].Text = d.text;
        //            f[f.Count - 1].SkyCode = GetWeatherPic(Convert.ToInt32(d.skycode));
        //            f[f.Count - 1].Url = forecastUrl;
        //        }

        //        if (f.Count > 0)
        //        {
        //            result.Curent.HighTemperature = f[0].HighTemperature;
        //            result.Curent.LowTemperature = f[0].LowTemperature;
        //        }

        //        result.ForecastList = f;

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex);
        //    }
        //    return null;
        //}

        //detects wind speed scale provided by MSN
        public WindSpeedScale DetectWindSpeedScale(string windDisplayString)
        {
            if (windDisplayString.Contains("km/h") || windDisplayString.Contains("км/ч"))
                return WindSpeedScale.Kmh;
            if (windDisplayString.Contains("m/s") || windDisplayString.Contains("м/с"))
                return WindSpeedScale.Ms;
            if (windDisplayString.Contains("mph"))
                return WindSpeedScale.Mph;
            return WindSpeedScale.Kmh;
        }

        //simple get weather pic method which returns icon associated with given skycode

        public static int GetWeatherPic(int skycode)
        {
            switch (skycode)
            {
                case 1:
                case 3:
                case 4:
                    return 3;
                case 5:
                    return 6;
                case 19:
                case 23:
                    return 13;
                case 50:
                case 8:
                    return 12;
                case 48:
                    return 11;
                case 27:
                    return 15;
                case 28:
                    return 4;
                case 30:
                    return 2;
                case 14:
                    return 18;
                case 20:
                    return 24;
                case 15:
                    return 22;
                default:
                    return 1;
            }
        }

        //public static int GetWeatherPic(int skycode)
        //{
        //    switch (skycode)
        //    {
        //        case 26:
        //            return 2;
        //        case 27:
        //            return 3;
        //        case 28:
        //            return 6;
        //        case 35:
        //        case 39:
        //            return 12;
        //        case 45:
        //        case 46:
        //            return 8;
        //        case 19:
        //        case 20:
        //        case 21:
        //        case 22:
        //            return 11;
        //        case 29:
        //        case 30:
        //            return 3;
        //        case 33:
        //            return 6;
        //        case 5:
        //        case 13:
        //        case 14:
        //        case 15:
        //        case 16:
        //            return 22;
        //        case 18:
        //        case 25:
        //        case 41:
        //        case 42:
        //        case 43:
        //            return 25;
        //        case 1:
        //        case 2:
        //        case 3:
        //        case 4:
        //        case 37:
        //        case 38:
        //        case 47:
        //            return 15;
        //        case 31:
        //        case 32:
        //        case 34:
        //        case 36:
        //        case 44:
        //            return 1;
        //        case 23:
        //        case 24:
        //            return 32;
        //        case 9:
        //        case 10:
        //        case 11:
        //        case 12:
        //        case 40:
        //            return 18;
        //        case 6:
        //        case 7:
        //        case 8:
        //        case 17:
        //            return 15;
        //        default:
        //            return 1;
        //    }
        //}

        //advanced get weather pic method which returns icon according to time of a day
        public static int GetWeatherPic(int skycode, DateTime sunrise, DateTime sunset, TimeSpan baseUtcOffset)
        {
            var userDateTime = DateTime.Now.ToUniversalTime().Add(baseUtcOffset);
            var isDay = userDateTime > sunrise && userDateTime < sunset;
            //note if offset differs to now for a day it will always returns night icon

            switch (skycode)
            {
                case 1:
                case 3:
                case 4:
                    return isDay ? 3 : 35;
                case 5:
                case 31:
                    return isDay ? 6 : 38;
                case 19:
                case 23:
                    return isDay ? 13 : 40;
                case 50:
                case 8:
                    return 12;
                case 48:
                    return isDay ? 11 : 37;
                case 27:
                    return 15;
                case 28:
                    return isDay ? 4 : 36;
                case 30:
                    return isDay ? 2 : 34;
                case 14:
                    return 18;
                case 20:
                    return 24;
                case 15:
                    return 22;
                default:
                    return isDay ? 1 : 33;
            }

            //switch (skycode)
            //{
            //    case 26:
            //        return isDay ? 2 : 34;
            //    case 27:
            //        return isDay ? 3 : 35;
            //    case 28:
            //        return isDay ? 6 : 38;
            //    case 35:
            //    case 39:
            //        return 12;
            //    case 45:
            //    case 46:
            //        return 8;
            //    case 19:
            //    case 20:
            //    case 21:
            //    case 22:
            //        return isDay ? 11 : 37;
            //    case 29:
            //    case 30:
            //        return isDay ? 3 : 35;
            //    case 33:
            //        return isDay ? 6 : 38;
            //    case 5:
            //    case 13:
            //    case 14:
            //    case 15:
            //    case 16:
            //        return 22;
            //    case 18:
            //    case 25:
            //    case 41:
            //    case 42:
            //    case 43:
            //        return 25;
            //    case 1:
            //    case 2:
            //    case 3:
            //    case 4:
            //    case 37:
            //    case 38:
            //    case 47:
            //        return 15;
            //    case 31:
            //    case 32:
            //    case 34:
            //    case 36:
            //    case 44:
            //        return isDay ? 1 : 33;
            //    case 23:
            //    case 24:
            //        return 32;
            //    case 9:
            //    case 10:
            //    case 11:
            //    case 12:
            //    case 40:
            //        return 18;
            //    case 6:
            //    case 7:
            //    case 8:
            //    case 17:
            //        return 15;
            //    default:
            //        return isDay ? 1 : 33;
            //}
        }
    }
}
