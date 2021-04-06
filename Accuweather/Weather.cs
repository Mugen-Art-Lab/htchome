/*
 * ================================================================================================
 * 
 *                          THIS CODE PROVIDED ONLY FOR EDUCATIONAL PURPOSES
 * 
 * ================================================================================================
 * (C) Stealth 2010
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

using HTCHome.Core;

using WeatherClockWidget;
using WeatherClockWidget.Domain;
using System.IO;
using System.Text;

namespace Accuweather
{
    public class Weather : IWeatherProvider
    {
        private const string RequestForCelsius =
            "http://karls.accu-weather.com/widget/karls/weather-data.asp?location={0}";
        private const string RequestForFahrenheit =
            "http://karls.accu-weather.com/widget/karls/weather-data.asp?location={0}&metric=1";
        private const string RequestForLocation =
            "http://karls.accu-weather.com/widget/karls/city-find.asp?location={0}";

        private string[] cities; // localized cities names
        private string[] weather; // localized weather strings

        public Coordinates GetCoordinates(string locationCode)
        {
            var reader = new XmlTextReader(string.Format(RequestForFahrenheit, locationCode));
            var coord = new Coordinates();
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals("lat"))
                {
                    coord.X = Math.Round(reader.ReadElementContentAsDouble(), 1);
                }
                if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals("lon"))
                {
                    coord.Y = Math.Round(reader.ReadElementContentAsDouble(), 1);
                }
            }
            return coord;
        }

        public List<CityLocation> GetLocation(string s)
        {
            string city = GetEnglishCityName(s);
            var l = new CityLocation();
            var result = new List<CityLocation>();
            var reader = new XmlTextReader(string.Format(RequestForLocation, city));

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals("location"))
                {
                    reader.MoveToAttribute("city");
                    l.City = reader.Value;
                    reader.MoveToAttribute("state");
                    l.City += " - " + reader.Value;
                    reader.MoveToAttribute("location");
                    l.Code = reader.Value;

                    result.Add(l);
                }
            }

            reader.Close();
            return result;
        }

        public WeatherReport GetWeatherReport(string locale, string locationcode, int degreeType)
        {
            if (string.IsNullOrEmpty(locationcode))
                locationcode = "10001";
            string url = string.Format(degreeType == 0 ? RequestForFahrenheit : RequestForCelsius, locationcode);

            if (cities == null && File.Exists(HTCHome.Core.Environment.Path + "\\WeatherClock\\WeatherProviders\\" + locale + "\\Accuweather.cities"))
            {
                cities = File.ReadAllLines(HTCHome.Core.Environment.Path + "\\WeatherClock\\WeatherProviders\\" + locale + "\\Accuweather.cities", Encoding.UTF8);
            }

            if (weather == null && File.Exists(HTCHome.Core.Environment.Path + "\\WeatherClock\\WeatherProviders\\" + locale + "\\Accuweather.weather"))
            {
                weather = File.ReadAllLines(HTCHome.Core.Environment.Path + "\\WeatherClock\\WeatherProviders\\" + locale + "\\Accuweather.weather", Encoding.UTF8);
            }

            string s = GeneralHelper.GetXml(url);
            //without this LINQ doesn't want to parse it
            s = s.Replace("<adc_database xmlns=\"http://www.accuweather.com\">", "<adc_database>");

            //parse current weather
            XDocument doc = XDocument.Parse(s);
            string location = doc.Descendants("local").FirstOrDefault().Element("city").Value;

            var conditions = from x in doc.Descendants("currentconditions")
                             select
                                 new {
                                         temp = x.Descendants("temperature").First().Value,
                                         text = x.Descendants("weathertext").First().Value,
                                         weathericon = x.Descendants("weathericon").First().Value,
                                         url = x.Descendants("url").First().Value
                                     };

            var result = new WeatherReport();

            var currentCondition = conditions.FirstOrDefault();
            if (currentCondition != null)
            {
                result.Location = GetCityName(location);
                result.NowTemp = Convert.ToInt32(currentCondition.temp);
                result.NowSky = GetWeatherString(currentCondition.text);
                result.NowSkyCode = Convert.ToInt32(currentCondition.weathericon);
                result.Url = currentCondition.url;
            }

            //parse forecast
            var days = from x in doc.Descendants("forecast").First().Descendants("day")
                       select new
                       {
                           h = x.Descendants("daytime").First().Element("hightemperature").Value,
                           l = x.Descendants("daytime").First().Element("lowtemperature").Value,
                           text = x.Descendants("daytime").First().Element("txtshort").Value,
                           icon = x.Descendants("daytime").First().Element("weathericon").Value,
                           url = x.Element("url").Value
                       };

            //what the hell is it?
            /*var days = from x in doc.Descendants("forecast").First().Descendants("day")
                       let xElement = x.Descendants("daytime").First()
                       let weatherIcon = xElement.Element("weathericon")
                       let txtLong = xElement.Element("txtlong")
                       let loTemp = xElement.Element("lowtemperature")
                       let hiTemp = xElement.Element("hightemperature")
                       select
                           new {
                                   h = hiTemp == null ? "0" : hiTemp.Value,
                                   l = loTemp == null ? "0" : loTemp.Value,
                                   text = txtLong == null ? "0" : txtLong.Value,
                                   weathericon = weatherIcon.Value
                               };*/

            List<DayForecast> f = new List<DayForecast>();
            foreach (var d in days)
            {
                f.Add(new DayForecast());
                f[f.Count - 1].HighTemperature = Convert.ToInt32(d.h);
                f[f.Count - 1].LowTemperature = Convert.ToInt32(d.l);
                f[f.Count - 1].Text = GetWeatherString(d.text);
                f[f.Count - 1].SkyCode = Convert.ToInt32(d.icon);
                f[f.Count - 1].Url = d.url;
            }

            result.Forecast = f;
            return result;
        }

        // returns localized city name
        private string GetCityName(string city)
        {
            if (cities != null)
            {
                //var c = from x in cities where x.StartsWith(city) select x;
                var c = Array.Find(cities, x => x.StartsWith(city));
                if (c == null)
                    return city;

                string result = c.Split(new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries)[1];
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
            return city;
        }

        // returns english city name from localized string
        private string GetEnglishCityName(string city)
        {
            if (cities != null)
            {
                //var c = from x in cities where x.StartsWith(city) select x;
                var c = Array.Find(cities, x => x.ToLower().EndsWith(city.ToLower()));
                if (c == null)
                    return city;

                string result = c.Split(new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries)[0];
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
            return city;
        }

        // returns localized weather string
        private string GetWeatherString(string w)
        {
            if (weather != null)
            {
                //var c = from x in cities where x.StartsWith(city) select x;
                var c = Array.Find(weather, x => x.StartsWith(w));
                if (c == null)
                    return w;

                string result = c.Split(new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries)[1];
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
            return w;
        }
    }
}