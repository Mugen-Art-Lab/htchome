using HTCHome.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using WeatherClockWidget;
using WeatherClockWidget.Domain;

namespace MSN
{
    public class Weather : IWeatherProvider
    {
        private const string RequestForCelsius = "http://service.weather.microsoft.com/{0}/weather/current/{1}?units=C";
        private const string RequestForFahrenheit = "http://service.weather.microsoft.com/{0}/weather/current/{1}?units=F";
        private const string RequestForLocation = "http://weather.service.msn.com/find.aspx?weasearchstr={0}&culture={1}&src=outlook";
        private const string ForecastUrlForCelsius = "http://service.weather.microsoft.com/{0}/weather/forecast/daily/{1}?units=C&nl=true";
        private const string ForecastUrlForFahrenheit = "http://service.weather.microsoft.com/{0}/weather/forecast/daily/{1}?units=F&nl=true";

        public Coordinates GetCoordinates(string locationCode)
        {
            var f = new NumberFormatInfo {CurrencyDecimalSeparator = "."};

            var reader = new XmlTextReader(string.Format(RequestForCelsius, "", locationCode));

            var coord = new Coordinates();
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || !reader.Name.Equals("weather"))
                {
                    continue;
                }

                reader.MoveToAttribute("lat");
                coord.X = Math.Round(Convert.ToDouble(reader.Value, f), 1);
                reader.MoveToAttribute("long");
                coord.Y = Math.Round(Convert.ToDouble(reader.Value, f), 1);
            }
            return coord;
        }

        public List<CityLocation> GetLocation(string s)
        {
            var l = new CityLocation();
            var result = new List<CityLocation>();
            var cultureName = CultureInfo.CurrentCulture.Name;

            var reader = new XmlTextReader(string.Format(RequestForLocation, s, cultureName));
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals("weather"))
                {
                    reader.MoveToAttribute("weatherlocationname");
                    l.City = reader.Value;
                    reader.MoveToAttribute("weatherlocationcode");
                    l.Code = reader.Value;

                    result.Add(l);
                }
            }

            reader.Close();
            return result;
        }

        WeatherReport IWeatherProvider.GetWeatherReport(string locale, string locationcode, int degreeType)
        {
            var isCelcius = degreeType == 0;
            string url = string.Format(isCelcius ? RequestForCelsius : RequestForFahrenheit, locale, locationcode);

            string s = GeneralHelper.GetXml(url); // it's not xml actually, method name is misleading

            if (string.IsNullOrEmpty(s))
                return null;

            var o = JObject.Parse(s);
            var currentWeather = o.SelectToken("responses[0].weather[0].current");
            if (currentWeather == null)
                return null;

            var result = new WeatherReport();
            result.NowTemp = currentWeather["temp"].Value<int>();
            result.NowSky = currentWeather["cap"].Value<string>();
            result.NowSkyCode = GetWeatherPic(currentWeather["icon"].Value<int>(), 4, 22); // TODO: sunset/sunrise should not be hardcoded

            var location = o.SelectToken("responses[0].source.location");

            if (location != null)
            {
                result.Location = location["Name"].Value<string>();
            }

            url = string.Format(isCelcius ? ForecastUrlForCelsius : ForecastUrlForFahrenheit, locale, locationcode);

            s = GeneralHelper.GetXml(url);
            if (string.IsNullOrEmpty(s))
                return result;

            o = JObject.Parse(s);
            var daysToken = o.SelectToken("responses[0].weather[0].days");
            if (daysToken == null)
                return result;



            //parse forecast
            var days = from x in daysToken
                       select
                           new
                           {
                               l = x["daily"]["tempLo"].Value<float>(),
                               h = x["daily"]["tempHi"].Value<float>(),
                               skycode = x["daily"]["icon"].Value<int>(),
                               text = x["daily"]["pvdrCap"].Value<string>()
                           };

            List<DayForecast> f = new List<DayForecast>();
            foreach (var d in days.Take(5).ToList())
            {
                f.Add(new DayForecast());
                f[f.Count - 1].HighTemperature = Convert.ToInt32(d.h);
                f[f.Count - 1].LowTemperature = Convert.ToInt32(d.l);
                f[f.Count - 1].Text = d.text;
                f[f.Count - 1].SkyCode = GetWeatherPic(Convert.ToInt32(d.skycode), -1, 25);
            }

            result.Forecast = f;

            return result;
        }

        public static int GetWeatherPic(int skycode, int sunrise, int sunset)
        {
            switch (skycode)
            {
                case 1:
                case 3:
                case 4:
                    if (DateTime.Now.Hour >= sunset || DateTime.Now.Hour <= sunrise)
                        return 35;
                    else
                        return 3;
                case 5:
                    if (DateTime.Now.Hour >= sunset || DateTime.Now.Hour <= sunrise)
                        return 38;
                    else
                        return 6;
                case 19:
                case 23:
                    return 13;
                case 50:
                case 8:
                    return 12;
                case 48:
                    if (DateTime.Now.Hour >= sunset || DateTime.Now.Hour <= sunrise)
                        return 37;
                    else
                        return 11;
                case 27:
                    return 15;
                case 28:
                    return 4;
                case 30:
                    if (DateTime.Now.Hour >= sunset || DateTime.Now.Hour <= sunrise)
                        return 34;
                    else
                        return 2;
                case 14:
                    return 18;
                case 20:
                    return 24;
                case 15:
                    return 22;
                default:
                    if (DateTime.Now.Hour >= sunset || DateTime.Now.Hour <= sunrise)
                        return 33;
                    else
                        return 1;
            }
        }
    }
}