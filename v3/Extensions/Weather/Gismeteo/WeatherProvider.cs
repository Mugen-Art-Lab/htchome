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

namespace Gismeteo3
{
    public class WeatherProvider : IWeatherProvider
    {
        const String prefix = "gismeteo";
        Regex rePrefix = new Regex(prefix + "\\|(.+)$");
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
                try
                {
                    String JQData = Regex.Unescape(Helper.GetRequest(new Uri("http://www.gismeteo.ru/ajax/city_search/?searchQuery=х" + Query), Encoding.UTF8, 30000));
                    Regex jqList = new Regex("\"'(\\d+)'\":\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (jqList.IsMatch(JQData))
                    {
                        List<LocationData> cList = new List<LocationData>();
                        foreach (Match m in jqList.Matches(new Regex("</?[^>]+>").Replace(JQData, String.Empty)))
                        {
                            String tln = Regex.Unescape(m.Groups[2].Value).Trim();
                            String tcn = String.Empty;
                            if (tln.IndexOf('-') > 0)
                            {
                                tcn = tln.Substring(tln.IndexOf('-') + 1).Trim();
                                tln = tln.Substring(0, tln.IndexOf('-')).Trim();
                            }
                            cList.Add(new LocationData()
                            {
                                Code = prefix + "|" + m.Groups[1].Value.Trim(),
                                City = tln,
                                Country = tcn,
                                Lat = Double.MinValue,
                                Lon = Double.MinValue
                            });
                        }
                        if (cList.Count > 0) return cList;
                    }
                }
                catch
                {

                }
            }
            return null;
        }
        #endregion
        #region GetWeatherReport
        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale, WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            Uri baseUrl = new Uri("http://www.gismeteo.ru");
            if (!rePrefix.IsMatch(location.Code))
            {
                String RData = Helper.HtmlDecode(Helper.GetRequest(baseUrl, Encoding.UTF8, 15000));
                Regex reCode = new Regex("<div class=\"wrap f_link\">\\s*<span.+?>.+?</span>\\s*<a.+?href=\"/city/daily/(\\d+)/\">.+?</a>\\s*</div>", RegexOptions.IgnoreCase);
                Regex reCode1 = new Regex("<div class=\"wrap f_link\">\\s*<a.+?href=\"/city/daily/(\\d+)/\">.+?</a>\\s*</div>", RegexOptions.IgnoreCase);
                if (reCode.IsMatch(RData)) location.Code = prefix + "|" + reCode.Match(RData).Groups[1].Value;
                else if (reCode1.IsMatch(RData)) location.Code = prefix + "|" + reCode1.Match(RData).Groups[1].Value.Trim();
            }
            if (!String.IsNullOrEmpty(location.Code))
            {
                String frmUrl = baseUrl.ToString() + "city/hourly/" + rePrefix.Match(location.Code).Groups[1].Value + "/";
                String rDataFill = Helper.HtmlDecode(Helper.GetRequest(new Uri(frmUrl), Encoding.UTF8, 15000));
                String rData1Fill = Helper.HtmlDecode(Helper.GetRequest(new Uri(frmUrl + "2/"), Encoding.UTF8, 15000));
                Regex locName = new Regex("<h3 class=\"type\\w{1}\">([^>]+)</h3>", RegexOptions.IgnoreCase);
                if (locName.IsMatch(rDataFill))
                {
                    location.City = locName.Match(rDataFill).Groups[1].Value.Trim();
                    Regex reCountry = new Regex("<div class=\"scity\">(.+?)</div>");
                    if (reCountry.IsMatch(rDataFill))
                        location.Country = new Regex("</?[^>]+>").Replace(reCountry.Match(rDataFill).Groups[1].Value.Trim(), String.Empty).Trim().Replace("/", "-");
                    WeatherData result = new WeatherData();
                    result.Location = location;
                    Regex curent = new Regex("<dl class=\"cloudness\">\\s*<dt.+?new/(.+?).png\\)\"><br></dt>\\s*<dd>(.+?)</dd>\\s*</dl>\\s*<div class=\"temp\">([^°]+)°C</div>\\s*<div.+?barp.+?>.+?</div>\\s*<div.+?wind.+?>.+?<dd>(\\d+).+?</div>\\s*<div.+?hum.+?>.*?(\\d+).+?</div>", RegexOptions.IgnoreCase);
                    if (!curent.IsMatch(rDataFill)) return null;
                    result.Curent.SkyCode = GetWeatherPic(curent.Match(rDataFill).Groups[1].Value);
                    result.Curent.Text = !String.IsNullOrEmpty(GetWeatherDesc(curent.Match(rDataFill).Groups[1].Value, culture)) ?
                        GetWeatherDesc(curent.Match(rDataFill).Groups[1].Value, culture) : curent.Match(rDataFill).Groups[2].Value;
                    result.Curent.Url = frmUrl;
                    result.Temperature = Convert.ToInt32(curent.Match(rDataFill).Groups[3].Value.Trim());
                    result.WindSpeed = Convert.ToInt32(curent.Match(rDataFill).Groups[4].Value.Trim());
                    result.Humidity = Convert.ToInt32(curent.Match(rDataFill).Groups[5].Value.Trim());
                    result.FeelsLike = Convert.ToInt32(TeFelt(result.Temperature, result.Humidity, result.WindSpeed));
                    if (tempScale == TemperatureScale.Fahrenheit)
                    {
                        result.Temperature = Convert.ToInt32(TempF(result.Temperature));
                        result.FeelsLike = Convert.ToInt32(TempF(result.FeelsLike));
                    }
                    #region Wind Speed
                    WindSpeedScale wScale = WindSpeedScale.Ms;
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

                    List<tmpForecast> fList = new List<tmpForecast>();
                    Regex list = new Regex("<tr class=\"wrow[^\"]*\" id=\"wrow-([^\"]+)\">\\s*<th[^>]*>[^<]*</th>\\s*<td class=\"clicon\">\\s*<img.+?new/(.+?).png\"[^>]*>\\s*</td>\\s*<td class=\"cltext\">([^<]+)</td>\\s*<td class=\"temp\">([^°]+)°</td>", RegexOptions.IgnoreCase);
                    foreach (Match m in list.Matches(rDataFill))
                        fList.Add(new tmpForecast() { dt = Convert.ToDateTime(m.Groups[1].Value.Substring(0, m.Groups[1].Value.LastIndexOf('-')) + " " + m.Groups[1].Value.Substring(m.Groups[1].Value.LastIndexOf('-') + 1) + ":00", new CultureInfo("ru-RU")), img = m.Groups[2].Value.Trim(), desc = m.Groups[3].Value.Trim(), temp = Convert.ToInt32(m.Groups[4].Value.Trim()) });
                    foreach (Match m in list.Matches(rData1Fill))
                        fList.Add(new tmpForecast() { dt = Convert.ToDateTime(m.Groups[1].Value.Substring(0, m.Groups[1].Value.LastIndexOf('-')) + " " + m.Groups[1].Value.Substring(m.Groups[1].Value.LastIndexOf('-') + 1) + ":00", new CultureInfo("ru-RU")), img = m.Groups[2].Value.Trim(), desc = m.Groups[3].Value.Trim(), temp = Convert.ToInt32(m.Groups[4].Value.Trim()) });
                    fList.Sort();
                    List<tmpForecast> fListMain = new List<tmpForecast>();
                    Regex listMain = new Regex("<div.+?wtab.+?>\\s*<img.+?new/(.+?).png\"[^>]*>\\s*<dl.+?date.+?>(.+?)</dl>", RegexOptions.IgnoreCase);
                    
                    for (int mi = 0; mi < listMain.Matches(rDataFill).Count; mi++)
                    {
                        Match m = listMain.Matches(rDataFill)[mi];
                        fListMain.Add(new tmpForecast() { dt = DateTime.Today.AddDays(mi), img=m.Groups[1].Value });
                    }
                    for (int mi = 0; mi < listMain.Matches(rData1Fill).Count; mi++)
                    {
                        Match m = listMain.Matches(rData1Fill)[mi];
                        fListMain.Add(new tmpForecast() { dt = DateTime.Today.AddDays(2 + mi), img = m.Groups[1].Value });
                    }
                    fListMain.Sort();

                    for (DateTime dc = DateTime.Today; dc < DateTime.Today.AddDays(5); dc = dc.AddDays(1))
                    {
                        ForecastData fd = new ForecastData() { HighTemperature = Int32.MinValue, LowTemperature = Int32.MaxValue };
                        foreach (tmpForecast tfm in fListMain)
                            if (tfm.dt == dc)
                                fd.SkyCode = GetWeatherPic(tfm.img);
                        foreach (tmpForecast tf in fList)
                            if (tf.dt >= dc && tf.dt < dc.AddDays(1))
                            {
                                if (fd.HighTemperature < tf.temp) fd.HighTemperature = tf.temp;
                                if (fd.LowTemperature > tf.temp) fd.LowTemperature = tf.temp;
                                fd.Url = "http://www.gismeteo.ru/city/hourly/" + rePrefix.Match(location.Code).Groups[1].Value + "/" +
                                    (dc > DateTime.Today.AddDays(1) ? "2/#wdaily" + ((dc - DateTime.Today).Days - 1).ToString() : "#wdaily" + ((dc - DateTime.Today).Days + 1).ToString());
                                //http://www.gismeteo.ru/city/hourly/12980/2/#wdaily2

                                foreach (tmpForecast tfm in fListMain)
                                {
                                    if (tf.dt >= tfm.dt && tf.dt < tfm.dt.AddDays(1) && tfm.CompareImg(tf.img))
                                    {
                                        fd.SkyCode = GetWeatherPic(tfm.img);
                                        fd.Text = GetWeatherDesc(tfm.img, culture) != String.Empty ? GetWeatherDesc(tfm.img, culture) : tf.desc;
                                    }
                                }
                                if (String.IsNullOrEmpty(fd.Text)) fd.Text = GetWeatherDescLng(tf.img, new CultureInfo("ru-RU"));
                                //if (dc.AddHours(13) <= tf.dt && dc.AddHours(16) > tf.dt)
                                //{
                                //    fd.SkyCode = GetWeatherPic(tf.img);
                                //    fd.Text = GetWeatherDesc(tf.img, culture) != String.Empty ? GetWeatherDesc(tf.img, culture) : tf.desc;
                                //}
                            }
                        if (fd.HighTemperature != Int32.MinValue && fd.LowTemperature != Int32.MaxValue)
                            result.ForecastList.Add(fd);
                    }
                    if (tempScale == TemperatureScale.Fahrenheit)
                        foreach (ForecastData fd in result.ForecastList)
                        {
                            fd.HighTemperature = Convert.ToInt32(TempF(fd.HighTemperature));
                            fd.LowTemperature = Convert.ToInt32(TempF(fd.LowTemperature));
                        }
                    if (result.ForecastList.Count > 0)
                    {
                        result.Curent.HighTemperature = result.ForecastList[0].HighTemperature;
                        result.Curent.LowTemperature = result.ForecastList[0].LowTemperature;
                        return result;
                    }
                }
            }
            return null;
        }
        #endregion
        #region GetWeatherPic
        private int GetWeatherPic(string icon)
        {
            try
            {
                icon += ".";
                Regex iconRe = new Regex("(([d|n])\\.)((sun|moon)\\.)(c(\\d{1})\\.)*((s|r)(\\d{1})\\.)*((st)\\.)*", RegexOptions.IgnoreCase);
                Boolean isDay = false;
                Int32 cloud = 0;
                Int32 precipType = 0; //0 -дождь, 2 - снег
                Int32 precipitation = 0;
                Boolean ts = false;
                if (iconRe.IsMatch(icon))
                {
                    isDay = iconRe.Match(icon).Groups[2].Value.ToLower() == "d";
                    if (!String.IsNullOrEmpty(iconRe.Match(icon).Groups[6].Value))
                        cloud = Convert.ToInt32(iconRe.Match(icon).Groups[6].Value.ToLower());
                    if (!String.IsNullOrEmpty(iconRe.Match(icon).Groups[9].Value))
                        precipitation = Convert.ToInt32(iconRe.Match(icon).Groups[9].Value.ToLower());
                    precipitation = iconRe.Match(icon).Groups[11].Value.ToLower() != String.Empty ? 4 : precipitation;
                    precipType = iconRe.Match(icon).Groups[8].Value.ToLower() == "s" ? 2 : precipType;
                    Int32 code = cloud * 100 + precipitation * 10 + precipType;
                    //Облачность 0-Ясно, 1-Небольшая облачность, 2-Переменная облачность, 3-Облачно с прояснениями, 4-Облачно
                    //Сила осадков 0-нет, 1-слабый, 2-тип, 3-сильный, 4-гроза
                    //Тип осадков 0-дождь,1-осадки 2-снег
                    isDay = icon.ToLower()[0] == 'd';
                    switch (code)
                    {
                        case 0: return isDay ? 1 : 33; //Ясно
                        case 100: return isDay ? 2 : 34; //Небольшая облачность
                        case 200: return isDay ? 3 : 35; //Переменная облачность
                        case 210: return isDay ? 14 : 39; //Переменная облачность, слабый дождь
                        case 211: return isDay ? 26 : 26; //Переменная облачность, слабые осадки
                        case 212: return isDay ? 21 : 44; //Переменная облачность, слабый снег
                        case 220: return isDay ? 14 : 39; //Переменная облачность, дождь
                        case 221: return isDay ? 26 : 26; //Переменная облачность, осадки
                        case 222: return isDay ? 20 : 43; //Переменная облачность, снег
                        case 240: return isDay ? 17 : 41; //Переменная облачность, гроза
                        case 300: return isDay ? 6 : 38; //Облачно с прояснениями
                        case 310: return isDay ? 13 : 40; //Облачно с прояснениями, слабый дождь
                        case 311: return isDay ? 26 : 26; //Облачно с прояснениями, слабый осадки
                        case 312: return isDay ? 21 : 44; //Облачно с прояснениями, слабый снег
                        case 320: return isDay ? 13 : 40; //Облачно с прояснениями, дождь
                        case 321: return isDay ? 26 : 26; //Облачно с прояснениями, осадки
                        case 322: return isDay ? 20 : 43; //Облачно с прояснениями, снег
                        case 340: return isDay ? 16 : 42; //Облачно с прояснениями, гроза
                        case 400: return isDay ? 7 : 8; //Облачно
                        case 410: return isDay ? 12 : 12; //Облачно, слабый дождь
                        case 411: return isDay ? 26 : 26; //Облачно, слабый осадки
                        case 412: return isDay ? 24 : 24; //Облачно, слабый снег
                        case 420: return isDay ? 12 : 12; //Облачно, дождь
                        case 421: return isDay ? 26 : 26; //Облачно, осадки
                        case 422: return isDay ? 19 : 19; //Облачно, снег
                        case 430: return isDay ? 18 : 18; //Облачно, сильный дождь
                        case 431: return isDay ? 26 : 26; //Облачно, сильные осадки
                        case 432: return isDay ? 22 : 22; //Облачно, сильный снег
                        case 440: return isDay ? 15 : 15; //Облачно, гроза
                        default: return 1;
                    }
                }
                return 1;
            }
            catch { return 1; }
        }
        #endregion
        #region GetWeatherDesc
        enum langs { ru, uk, en }
        private String GetWeatherDesc(string icon, CultureInfo ci) { return String.Empty; }
        private String GetWeatherDescLng(string icon, CultureInfo ci)
        {
            langs l = langs.en;
            if (ci.TwoLetterISOLanguageName == "ru") l = langs.ru;
            else if (ci.TwoLetterISOLanguageName == "uk") l = langs.uk;
            String desc = "Clear";
            if (l != langs.en) desc = "Ясно";
            try
            {
                icon += ".";
                Regex iconRe = new Regex("(([d|n])\\.)((sun|moon)\\.)(c(\\d{1})\\.)*((s|r)(\\d{1})\\.)*((st)\\.)*", RegexOptions.IgnoreCase);
                Boolean isDay = false;
                Int32 cloud = 0;
                Int32 precipType = 0; //0 -дождь, 2 - снег
                Int32 precipitation = 0;
                Boolean ts = false;
                if (iconRe.IsMatch(icon))
                {
                    isDay = iconRe.Match(icon).Groups[2].Value.ToLower() == "d";
                    if (!String.IsNullOrEmpty(iconRe.Match(icon).Groups[6].Value))
                        cloud = Convert.ToInt32(iconRe.Match(icon).Groups[6].Value.ToLower());
                    if (!String.IsNullOrEmpty(iconRe.Match(icon).Groups[9].Value))
                        precipitation = Convert.ToInt32(iconRe.Match(icon).Groups[9].Value.ToLower());
                    precipitation = iconRe.Match(icon).Groups[11].Value.ToLower() != String.Empty ? 4 : precipitation;
                    precipType = iconRe.Match(icon).Groups[8].Value.ToLower() == "s" ? 2 : precipType;
                    Int32 code = cloud * 100 + precipitation * 10 + precipType;
                    //Облачность 0-Ясно, 1-Небольшая облачность, 2-Переменная облачность, 3-Облачно с прояснениями, 4-Облачно
                    //Сила осадков 0-нет, 1-слабый, 2-тип, 3-сильный, 4-гроза
                    //Тип осадков 0-дождь,1-осадки 2-снег
                    isDay = icon.ToLower()[0] == 'd';
                    switch (code)
                    {
                        //case 000: return l == langs.ru ? "" : (l == langs.uk ? "" : ""); //Ясно
                        case 100: return l == langs.ru ? "Небольшая облачность" : (l == langs.uk ? "Слабка хмарність" : "Mostly sunny"); //Небольшая облачность
                        case 200: return l == langs.ru ? "Переменная облачность" : (l == langs.uk ? "Мінлива хмарність" : "Partly cloudy"); //Переменная облачность
                        case 210: return l == langs.ru ? "Переменная облачность, слабый дождь" : (l == langs.uk ? "Мінлива хмарність, невеликий дощ" : "Partly cloudy, light rain"); //Переменная облачность, слабый дождь
                        case 211: return l == langs.ru ? "Переменная облачность, слабые осадки" : (l == langs.uk ? "Мінлива хмарність, слабкі опади" : "Partly cloudy, weak rainfall"); //Переменная облачность, слабые осадки
                        case 212: return l == langs.ru ? "Переменная облачность, слабый снег" : (l == langs.uk ? "Мінлива хмарність, слабкий сніг" : "Partly cloudy, light snow"); //Переменная облачность, слабый снег
                        case 220: return l == langs.ru ? "Переменная облачность, дождь" : (l == langs.uk ? "Мінлива хмарність, дощ" : "Partly cloudy, rain"); //Переменная облачность, дождь
                        case 221: return l == langs.ru ? "Переменная облачность, осадки" : (l == langs.uk ? "Мінлива хмарність, опади" : "Partly cloudy, rainfall"); //Переменная облачность, осадки
                        case 222: return l == langs.ru ? "Переменная облачность, снег" : (l == langs.uk ? "Мінлива хмарність, сніг" : "Partly cloudy, snow"); //Переменная облачность, снег
                        case 240: return l == langs.ru ? "Переменная облачность, гроза" : (l == langs.uk ? "Мінлива хмарність, гроза" : "Partly cloudy, thunderstorm"); //Переменная облачность, гроза
                        case 300: return l == langs.ru ? "Облачно с прояснениями" : (l == langs.uk ? "Хмарно з проясненнями" : "Partly cloudy"); //Облачно с прояснениями
                        case 310: return l == langs.ru ? "Облачно с прояснениями, слабый дождь" : (l == langs.uk ? "Хмарно з проясненнями, невеликий дощ" : "Partly cloudy, light rain"); //Облачно с прояснениями, слабый дождь
                        case 311: return l == langs.ru ? "Облачно с прояснениями, слабый осадки" : (l == langs.uk ? "Хмарно з проясненнями, слабкі опади" : "Partly cloudy, weak rainfall"); //Облачно с прояснениями, слабый осадки
                        case 312: return l == langs.ru ? "Облачно с прояснениями, слабый снег" : (l == langs.uk ? "Хмарно з проясненнями, слабкий сніг" : "Partly cloudy, light snow"); //Облачно с прояснениями, слабый снег
                        case 320: return l == langs.ru ? "Облачно с прояснениями, дождь" : (l == langs.uk ? "Хмарно з проясненнями, дощ" : "Partly cloudy, rain"); //Облачно с прояснениями, дождь
                        case 321: return l == langs.ru ? "Облачно с прояснениями, осадки" : (l == langs.uk ? "Хмарно з проясненнями, опади" : "Partly cloudy, rainfall"); //Облачно с прояснениями, осадки
                        case 322: return l == langs.ru ? "Облачно с прояснениями, снег" : (l == langs.uk ? "Хмарно з проясненнями, сніг" : "Partly cloudy, snow"); //Облачно с прояснениями, снег
                        case 340: return l == langs.ru ? "Облачно с прояснениями, гроза" : (l == langs.uk ? "Хмарно з проясненнями, гроза" : "Partly cloudy, thunderstorm"); //Облачно с прояснениями, гроза
                        case 400: return l == langs.ru ? "Облачно" : (l == langs.uk ? "Похмуро" : "Cloudy"); //Облачно
                        case 410: return l == langs.ru ? "Облачно, слабый дождь" : (l == langs.uk ? "Похмуро, невеликий дощ" : "Cloudy, light rain"); //Облачно, слабый дождь
                        case 411: return l == langs.ru ? "Облачно, слабый осадки" : (l == langs.uk ? "Похмуро, слабкі опади" : "Cloudy, weak rainfall"); //Облачно, слабый осадки
                        case 412: return l == langs.ru ? "Облачно, слабый снег" : (l == langs.uk ? "Похмуро, слабкий сніг" : "Cloudy, light snow"); //Облачно, слабый снег
                        case 420: return l == langs.ru ? "Облачно, дождь" : (l == langs.uk ? "Похмуро, дощ" : "Cloudy, rain"); //Облачно, дождь
                        case 421: return l == langs.ru ? "Облачно, осадки" : (l == langs.uk ? "Похмуро, опади" : "Cloudy, rainfall"); //Облачно, осадки
                        case 422: return l == langs.ru ? "Облачно, снег" : (l == langs.uk ? "Похмуро, сніг" : "Cloudy, snow"); //Облачно, снег
                        case 430: return l == langs.ru ? "Облачно, сильный дождь" : (l == langs.uk ? "Похмуро, злива" : "Cloudy, downpour"); //Облачно, сильный дождь
                        case 431: return l == langs.ru ? "Облачно, сильные осадки" : (l == langs.uk ? "Похмуро" : "Cloudy, heavy rainfall"); //Облачно, сильные осадки
                        case 432: return l == langs.ru ? "Облачно, сильный снег" : (l == langs.uk ? "Похмуро, снігопад" : "Cloudy, snowfall"); //Облачно, сильный снег
                        case 440: return l == langs.ru ? "Облачно, гроза" : (l == langs.uk ? "Похмуро, гроза" : "Cloudy, thunderstorm"); //Облачно, гроза
                        default: return desc;
                    }
                }
            }
            catch { }
            return String.Empty;
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
        #region TempF
        public static double TempF(double TempC)
        {
            return Math.Round(1.8 * TempC + 32.0);
        }
        #endregion
    }

    #region tmpForecast
    public class tmpForecast : IComparable
    {
        public DateTime dt;
        public String img = String.Empty;
        public String desc = String.Empty;
        public Int32 temp;

        public override string ToString()
        {
            return dt.ToString("dd.MM HH") + " temp:" + temp + ", " + desc + " (" + img + ")";
        }

        public int CompareTo(object obj)
        {
            tmpForecast othert_tmpForecast = obj as tmpForecast;
            if (othert_tmpForecast != null)
                return this.dt.CompareTo(othert_tmpForecast.dt);
            else
                throw new ArgumentException("Object is not a tmpForecast");
        }

        public bool CompareImg(String IMG)
        {
            String IMG0 = IMG + ".";
            String img0 = img + ".";
            Regex iconRe = new Regex("(([d|n])\\.)((sun|moon)\\.)(c(\\d{1})\\.)*((s|r)(\\d{1})\\.)*((st)\\.)*$", RegexOptions.IgnoreCase);
            for (int i = 5; i < iconRe.Match(IMG0).Groups.Count; i++)
                if (iconRe.Match(IMG0).Groups[i].Value != iconRe.Match(img0).Groups[i].Value)
                    return false;
            return true;
        }
    }
    #endregion
}
