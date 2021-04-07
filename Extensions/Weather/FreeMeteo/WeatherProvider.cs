using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Weather.Base;
using System.Globalization;
using System.Net;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using WeatherProviders;

namespace FreeMeteo3
{
    public class WeatherProvider : IWeatherProvider
    {
        const String prefix = "freemeteo";
        Regex rePrefix = new Regex(prefix + "\\|(.+)$");
        private const string RequestForCelsiusCurent = "http://freemeteo.com/default.asp?pid=15&gid={0}&la={1}&sub_units=1";
        private const string RequestForFahrenheitCurent = "http://freemeteo.com/default.asp?pid=15&gid={0}&la={1}&sub_units=2";
        private const string RequestForCelsiusForecast = "http://freemeteo.com/default.asp?pid=23&gid={0}&la={1}&sub_units=1";
        private const string RequestForFahrenheitForecast = "http://freemeteo.com/default.asp?pid=23&gid={0}&la={1}&sub_units=2";
        private const string RequestForLocation = "http://freemeteo.com/ajax/ajaxProxy.asp?c=PointFinder&m=getContent&page={2}&Country=0&City={0}&isP=0&sortBy=7&sortOrder=desc&la={1}&ajax=1";
        private const string UrlDetail = "http://freemeteo.com/default.asp?pid=22&la={1}&gid={0}&nDate={2}";
        NumberFormatInfo nfi = new NumberFormatInfo() { CurrencyDecimalSeparator = "." };
        #region GetLocations
        public List<LocationData> GetLocations(string query, CultureInfo culture, TemperatureScale tempScale)
        {
            return GetLocations(query, culture);
        }
        public List<LocationData> GetLocations(string query, CultureInfo culture)
        {
            if (!String.IsNullOrEmpty(query) && !String.IsNullOrEmpty(query.Trim()))
            {
                String Query = query.Trim();
                #region Начало
                Int32 language = GetLaguageId(culture);
                Int32 page = 1;
                String reListTXT = "<A href=\"default.asp\\?pid=[\\d]+&gid=([\\d]+)&la=[\\d]+[^\"]*\">([^\\<]+)</a>\\s*(<span[^>]+>[^<]+</span>)*\\s*</td>\\s*" +
                    "<td class=\"results_blue\" title=\"([^\"]*)\"[^>]*>([^\\<]+)</td>\\s*" +
                    "<td class=\"results_blue\"[^>]*>([^\\<]*)</td>\\s*" +
                    "<td class=\"results_blue_sm\"[^>]*>([^\\<]*)</td>\\s*" +
                    "<td class=\"results_blue_sm\"[^>]*>([^\\<]*)</td>\\s*" +
                    "<td class=\"results_blue_sm_r\"[^>]*>([^\\<]*)</td>\\s*";
                Regex reList = new Regex(reListTXT, RegexOptions.IgnoreCase);
                Regex rePages = new Regex("<font class=\"nav_num\">\\s*[\\d]+/([\\d]*)\\s*</font>", RegexOptions.IgnoreCase);
                #region Данные с первой страницы
                String SearchUrl = String.Format(RequestForLocation, Helper.UrlEncode(query), language.ToString(), page.ToString());
                String sData = Helper.GetRequest(new Uri(SearchUrl), Encoding.UTF8, 15000);
                if (reList.IsMatch(sData))
                {
                    List<LocationData> cList = new List<LocationData>();
                    foreach (Match m in reList.Matches(sData))
                        cList.Add(new LocationData()
                        {
                            City = m.Groups[2].Value.Trim(),
                            Country = m.Groups[4].Value.Trim() + (m.Groups[6].Value.Trim() != String.Empty ? ", " + m.Groups[6].Value.Trim() : String.Empty),
                            Code = prefix + "|" + Convert.ToInt32(m.Groups[1].Value.Trim()).ToString(),
                            Lat = Convert.ToDouble(m.Groups[7].Value.Trim(), nfi),
                            Lon = Convert.ToDouble(m.Groups[8].Value.Trim(), nfi)
                        });
                    #region Если есть еще страницы, то их собираем
                    if (rePages.IsMatch(sData))
                    {
                        Int32 pages = Convert.ToInt32(rePages.Match(sData).Groups[1].Value.Trim());
                        for (Int32 p = page; p < pages; p++)
                        {
                            SearchUrl = String.Format(RequestForLocation, Helper.UrlEncode(query), language.ToString(), page.ToString());
                            sData = Helper.GetRequest(new Uri(SearchUrl), Encoding.UTF8, 15000);
                            if (reList.IsMatch(sData))
                                foreach (Match m in reList.Matches(sData))
                                    cList.Add(new LocationData()
                                    {
                                        City = m.Groups[2].Value.Trim(),
                                        Country = String.Format("{1}, {3}", m.Groups[2].Value.Trim(), m.Groups[4].Value.Trim(), m.Groups[5].Value.Trim(), m.Groups[6].Value.Trim()),
                                        Code = prefix + "|" + Convert.ToInt32(m.Groups[1].Value.Trim()).ToString(),
                                        Lat = Convert.ToDouble(m.Groups[7].Value.Trim(), nfi),
                                        Lon = Convert.ToDouble(m.Groups[8].Value.Trim(), nfi)
                                    });
                        }
                    }
                    #endregion
                    return cList;
                }
                #endregion
                #endregion
            }
            return null;
        }
        #endregion
        #region GetWeatherReport
        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale, WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            Int32 language = GetLaguageId(culture);
            #region 
            if (!rePrefix.IsMatch(location.Code))
            {
                String tRequest = Helper.GetRequest(new Uri("http://freemeteo.com/index.asp"), Encoding.UTF8, 15000);
                Regex reGetID = new Regex("<a.+?sgid=(\\d+).*?class=\"weatherforyoursite\"\\s*>", RegexOptions.IgnoreCase);
                if (reGetID.IsMatch(tRequest))
                    location.Code = prefix + "|" + reGetID.Match(tRequest).Groups[1].Value;
            }
            String SearchUrl = String.Format(tempScale == TemperatureScale.Celsius ? RequestForCelsiusCurent : RequestForFahrenheitCurent, rePrefix.Match(location.Code).Groups[1], language);
            String sData = Helper.GetRequest(new Uri(SearchUrl), Encoding.UTF8, 15000);
            if (string.IsNullOrEmpty(sData))
                return null;
            Regex name = new Regex("<DIV class=report_img_new_city>(.+?)</div>", RegexOptions.IgnoreCase);
            Regex re_tags = new Regex("</?[^>]+>", RegexOptions.IgnoreCase);
            Regex icon = new Regex("\\.\\./templates/default/icons/([^\\.]+).swf", RegexOptions.IgnoreCase);
            Regex temp = new Regex(tempScale == TemperatureScale.Celsius ? "<FONT class=temperature>([^<]+)<" : "<FONT class=temperature>(\\d+)\\s*&deg;F<", RegexOptions.IgnoreCase);
            Regex wdesc = new Regex("<DIV class=report_img_new_bottom>([^<]+)<", RegexOptions.IgnoreCase);
            //Regex dop = new Regex("<div class=\"details\">\\s*<b>[^<]*</b>\\s*(\\d+)\\s*%\\s*<br>\\s*<b>[^<]*</b>.*?(\\d+).*?<br>", RegexOptions.IgnoreCase);
            Regex dop = new Regex("<div class=\"details\">\\s*<b>[^<]*</b>\\s*(\\d+)\\s*%\\s*<br>\\s*<b>[^<]*</b>([^<]+)<br>", RegexOptions.IgnoreCase);
            Regex coord = new Regex("<span class=\"location\">[^:]+:\\s*(\\-*[\\d]+\\.[\\d]+)\\s*[^:]+:\\s*(\\-*[\\d]+\\.[\\d]+)", RegexOptions.IgnoreCase);

            if (name.IsMatch(sData) && icon.IsMatch(sData) && temp.IsMatch(sData) && wdesc.IsMatch(sData) && dop.IsMatch(sData))
            {
                location.City = re_tags.Replace(name.Match(sData).Groups[1].Value.Trim(), String.Empty).Split(',')[0].Trim();
                if (String.IsNullOrEmpty(location.Country)) location.Country = re_tags.Replace(name.Match(sData).Groups[1].Value.Trim(), String.Empty).Split(',')[1].Trim();
                WeatherData result = new WeatherData();
                result.Location = location;
                result.Temperature = Convert.ToInt32(temp.Match(sData).Groups[1].Value.Trim());
                result.Curent.Text = wdesc.Match(sData).Groups[1].Value.Trim();
                if (coord.IsMatch(sData) && ((location.Lat == Double.MinValue && location.Lon == Double.MinValue) || (location.Lat == 0 && location.Lon == 0)))
                {
                    location.Lat = Convert.ToDouble(coord.Match(sData).Groups[1].Value.Trim(), nfi);
                    location.Lon = Convert.ToDouble(coord.Match(sData).Groups[2].Value.Trim(), nfi);
                }
                DateTime sunrise = DateTime.Today.AddHours(4);
                DateTime sunset = DateTime.Today.AddHours(22);
                if (location.Lat != Double.MinValue && location.Lon != Double.MinValue)
                {
                    SunCalculator sc = new SunCalculator(DateTime.Today, location.Lat, location.Lon);
                    sunrise = sc.DSunRise;
                    sunset = sc.DSunSet;
                }
                Int32.TryParse(dop.Match(sData).Groups[1].Value, out result.Humidity);
                Regex digits = new Regex(".*?(\\d+)", RegexOptions.IgnoreCase);
                if (digits.IsMatch(dop.Match(sData).Groups[2].Value))
                    Int32.TryParse(digits.Match(dop.Match(sData).Groups[2].Value).Groups[1].Value, out result.WindSpeed);
                Double ft = TeFelt(tempScale == TemperatureScale.Celsius ? (double)result.Temperature : (result.Temperature - 32.0) / 1.8, result.Humidity, WeatherConverter.WindSpeedConvertToMs(result.WindSpeed, WindSpeedScale.Kmh));
                result.FeelsLike = Convert.ToInt32(tempScale == TemperatureScale.Celsius ? ft : 1.8 * ft + 32.0);
                #region Wind Speed
                WindSpeedScale wScale = WindSpeedScale.Kmh;
                if (wScale != windSpeedScale)
                    switch (windSpeedScale)
                    {
                        case WindSpeedScale.Mph:
                            result.WindSpeed = (int)Math.Round(WeatherConverter.WindSpeedConvertToMph(result.WindSpeed, wScale), 0);
                            break;
                        case WindSpeedScale.Kmh:
                            result.WindSpeed = (int)Math.Round(WeatherConverter.WindSpeedConvertToKmh(result.WindSpeed, wScale), 0);
                            break;
                        case WindSpeedScale.Ms:
                            result.WindSpeed = (int)Math.Round(WeatherConverter.WindSpeedConvertToMs(result.WindSpeed, wScale), 0);
                            break;
                    }
                #endregion
                result.Curent.SkyCode = GetWeatherPic(icon.Match(sData).Groups[1].Value.Trim(), sunrise, sunset);
                result.Curent.Url = SearchUrl;
                #region Пргноз
                SearchUrl = String.Format(tempScale == TemperatureScale.Celsius ? RequestForCelsiusForecast : RequestForFahrenheitForecast, rePrefix.Match(location.Code).Groups[1], language);
                sData = Helper.HtmlDecode(Helper.GetRequest(new Uri(SearchUrl), Encoding.UTF8, 15000));

                String reTxt = "<img src=\"../templates/default/iconsgif/([\\d]+)n*.gif\" width=\"50\" height=\"41\"></TD>\\s*<TD class=\"tbl_stations_content\">" +
                    "([^<]+)<br>\\s*<span\\s*style=\"text-align:right;width:100\\%;\">\\s*<a[^>]+>[^<]+</a></span></TD>\\s*" +
                    "<TD class=\"tbl_stations_content\"\\s*NOWRAP><b>[^:]+:([^<]+)<sup>o</sup>C\\s*(</b><BR>[^:]+:([^<]+)<sup>o</sup>C)*\\s*</TD>";
                String reTxtF = "<img src=\"../templates/default/iconsgif/([\\d]+)n*.gif\" width=\"50\" height=\"41\"></TD>\\s*<TD class=\"tbl_stations_content\">" +
                    "([^<]+)<br>\\s*<span\\s*style=\"text-align:right;width:100\\%;\">\\s*<a[^>]+>[^<]+</a></span></TD>\\s*" +
                    "<TD class=\"tbl_stations_content\"\\s*NOWRAP><b>[^:]+:\\s*([\\d]+)\\s*°F\\s*(</b><BR>[^:]+:\\s*([\\d]+)\\s*°F)*\\s*</TD>";

                //<img src="../templates/default/iconsgif/3N.gif" width="50" height="41"></TD>    <TD class="tbl_stations_content">Переменная облачность<br><span style="text-align:right;width:100%;"><a href="default.asp?pid=22&la=14&gid=2013348&nDate=0">Детали...</a></span></TD>    <TD class="tbl_stations_content" NOWRAP><b>Низ.: 2&nbsp; <sup>o</sup>C</TD>
                //<img src="../templates/default/iconsgif/30.gif" width="50" height="41"></TD>    <TD class="tbl_stations_content">Ранним утром наблюдается незначительная облачность  &nbsp;переменная облачность, возможен дождь в течение дня. Облачно поздним вечером.<br><span style="text-align:right;width:100%;"><a href="default.asp?pid=22&la=14&gid=2013348&nDate=1">Детали...</a></span></TD>    <TD class="tbl_stations_content" NOWRAP><b>Выс.: 17&nbsp; <sup>o</sup>C </b><BR> Низ.: 2&nbsp; <sup>o</sup>C</TD>
                //reTxt = "<img.*?templates/default/iconsgif/3N.gif.*?50.*?41.*?></TD>" +                    "";

                Regex reForecast = new Regex(tempScale == TemperatureScale.Celsius ? reTxt : reTxtF, RegexOptions.IgnoreCase);
                if (reForecast.IsMatch(sData))
                {
                    Int32 num = 0;
                    foreach (Match m in reForecast.Matches(sData))
                    {
                        Int32 LT = 0;
                        if (!Int32.TryParse(m.Groups[5].Value.Trim(), out LT))
                            LT = result.Temperature;
                        result.ForecastList.Add(new ForecastData()
                        {
                            SkyCode = GetWeatherPic(m.Groups[1].Value.Trim(), DateTime.MinValue, DateTime.MaxValue),
                            Text = m.Groups[2].Value.Trim().Replace("  ", " "),
                            HighTemperature = Convert.ToInt32(m.Groups[3].Value.Trim()),
                            LowTemperature = LT,
                            Url = String.Format(UrlDetail, rePrefix.Match(location.Code).Groups[1], language, num++)
                        });
                    }
                    if (result.ForecastList.Count > 0)
                    {
                        result.Curent.HighTemperature = result.ForecastList[0].HighTemperature;
                        result.Curent.LowTemperature = result.ForecastList[0].LowTemperature;
                        return result;
                    }
                }
                #endregion
            }
            #endregion
            return null;
        }
        #endregion
        #region GetWeatherPic
        private int GetWeatherPic(string icon, DateTime sunrise, DateTime sunset)
        {
            if (String.IsNullOrEmpty(icon)) return 1;
            icon = icon.ToLower().Replace("n", String.Empty);
            bool isDay = DateTime.Now > sunrise && DateTime.Now < sunset;
            #region Fog
            switch (icon)
            {
                case "1f": { return isDay ? 5 : 37; }
                case "3f":
                case "4f": { return isDay ? 11 : 11; }
                default: break;
            }
            icon = icon.ToLower().Replace("f", String.Empty);
            #endregion
            switch (Convert.ToInt32(icon))
            {
                case 1: { return isDay ? 1 : 33; }
                case 2: { return isDay ? 2 : 34; }
                case 3: { return isDay ? 6 : 38; }
                case 4: { return isDay ? 7 : 8; }
                case 5: { return isDay ? 14 : 39; }
                case 6:
                case 7:
                case 8: { return isDay ? 12 : 18; }
                case 9:
                case 10:
                case 11:
                case 16:
                case 17:
                case 22:
                case 23:
                case 28:
                case 29:
                case 39:
                case 44:
                case 49: { return isDay ? 15 : 15; }
                case 12:
                case 13:
                case 18:
                case 19:
                case 35:
                case 36:
                case 40:
                case 41: { return isDay ? 26 : 26; }
                case 14:
                case 15:
                case 20:
                case 21:
                case 37:
                case 38:
                case 42:
                case 43: { return isDay ? 29 : 29; }
                case 24:
                case 25: { return isDay ? 24 : 24; }
                case 26:
                case 27: { return isDay ? 22 : 22; }
                case 30:
                case 31:
                case 32:
                case 33: { return isDay ? 13 : 40; }
                case 34: { return isDay ? 16 : 42; }
                case 45:
                case 46: { return isDay ? 21 : 43; }
                case 47:
                case 48: { return isDay ? 23 : 44; }
                default: break;
            }
            return 1;
        }
        #endregion
        #region Language ID
        Int32 GetLaguageId(CultureInfo ci)
        {
            foreach (string l in Enum.GetNames(typeof(Languages)))
                if (ci.EnglishName.ToLower().IndexOf(l.ToLower()) == 0)
                    return Convert.ToInt32((Languages)Enum.Parse(typeof(Languages), l, true));
            return 1;
        }
        
        enum Languages
        {
            Arabic = 19,
            Bulgarian = 22,
            Catalan = 24,
            Chinese = 7,
            Croatian = 21,
            Czech = 12,
            Danish = 8,
            Dutch = 11,
            English = 1,
            Finnish = 10,
            French = 6,
            German = 3,
            Greek = 2,
            Hungarian = 16,
            Italian = 13,
            Norwegian = 9,
            Polish = 20,
            Portuguese = 18,
            Romanian = 15,
            Russian = 14,
            Serbian = 23,
            Spanish = 4,
            Swedish = 5,
            Turkish = 17
        }
        #endregion
        #region TeFelt
        public static double TeFelt(Double Temperature, Double Humidity, Double WindSpeed)
        {
            //Kelvin
            double var_1 = 7.5 * Temperature / (237.7 + Temperature);
            double var_2 = Math.Pow(10.0, var_1);
            double var_3 = 6.112 * var_2 * Humidity / 100.0;
            double Temperature2 = Temperature + 5.0 / 9.0 * (var_3 - 10);
            if (Temperature2 < Temperature) Temperature2 = Temperature;
            double Temperature1 = WindSpeed <= 2.0 ? Temperature2 : 0.045 * ((5.2735 * Math.Sqrt(WindSpeed * 3.0) + 10.45) - 0.2778 * WindSpeed * 3.0) * (Temperature2 - 33.0) + 33.0;
            return Math.Round(Temperature1);
        }
        #endregion
    }
}
