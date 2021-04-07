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
using System.Xml.Linq;
using System.Collections.Specialized;

namespace AccuWeather3
{
    public class WeatherProvider : IWeatherProvider
    {
        const String prefix = "accuweather";
        Regex rePrefix = new Regex(prefix + "\\|(.+)$");
        NumberFormatInfo nfi = new NumberFormatInfo() { CurrencyDecimalSeparator = "." };
        #region Language
        IntLangZ Language(CultureInfo ci)
        {
            String nameSmall = ci.TwoLetterISOLanguageName.ToLower();
            String nameFull = ci.IetfLanguageTag.ToLower();
            if (nameSmall == "ca")
                return new IntLangZ()
                {
                    ID = 22,
                    Name = Helper.UrlDecode("Catal%c3%a0"),
                    ISO = new CultureInfo("ca"),
                    CI = new CultureInfo("ca-ES")
                };
            if (nameSmall == "cs")
                return new IntLangZ()
                {
                    ID = 19,
                    Name = Helper.UrlDecode("%c4%8ce%c5%a1tina"),
                    ISO = new CultureInfo("cs"),
                    CI = new CultureInfo("cs-CZ")
                };
            if (nameSmall == "da")
                return new IntLangZ()
                {
                    ID = 4,
                    Name = Helper.UrlDecode("Dansk"),
                    ISO = new CultureInfo("da"),
                    CI = new CultureInfo("da-DK")
                };
            if (nameSmall == "de")
                return new IntLangZ()
                {
                    ID = 9,
                    Name = Helper.UrlDecode("Deutsch"),
                    ISO = new CultureInfo("de"),
                    CI = new CultureInfo("de-DE")
                };
            if (nameFull == "en-gb")
                return new IntLangZ()
                {
                    ID = 28,
                    Name = Helper.UrlDecode("English+(UK)"),
                    ISO = new CultureInfo("en-gb"),
                    CI = new CultureInfo("en-US")
                };
            if (nameFull == "es-ar")
                return new IntLangZ()
                {
                    ID = 15,
                    Name = Helper.UrlDecode("Espa%c3%b1ol"),
                    ISO = new CultureInfo("es-ar"),
                    CI = new CultureInfo("es-AR")
                };
            if (nameFull == "es-mx")
                return new IntLangZ()
                {
                    ID = 16,
                    Name = Helper.UrlDecode("Espa%c3%b1ol+(Latin)"),
                    ISO = new CultureInfo("es-mx"),
                    CI = new CultureInfo("es-MX")
                };
            if (nameSmall == "es")
                return new IntLangZ()
                {
                    ID = 2,
                    Name = Helper.UrlDecode("Castellano"),
                    ISO = new CultureInfo("es"),
                    CI = new CultureInfo("es-ES")
                };
            if (nameFull == "fr-ca")
                return new IntLangZ()
                {
                    ID = 32,
                    Name = Helper.UrlDecode("Fran%c3%a7ais+(Canada)"),
                    ISO = new CultureInfo("fr-ca"),
                    CI = new CultureInfo("fr-FR")
                };
            if (nameSmall == "fr")
                return new IntLangZ()
                {
                    ID = 3,
                    Name = Helper.UrlDecode("Fran%c3%a7ais"),
                    ISO = new CultureInfo("fr"),
                    CI = new CultureInfo("fr-FR")
                };
            if (nameSmall == "it")
                return new IntLangZ()
                {
                    ID = 8,
                    Name = Helper.UrlDecode("Italiano"),
                    ISO = new CultureInfo("it"),
                    CI = new CultureInfo("it-IT")
                };
            if (nameSmall == "hu")
                return new IntLangZ()
                {
                    ID = 20,
                    Name = Helper.UrlDecode("Magyar"),
                    ISO = new CultureInfo("hu"),
                    CI = new CultureInfo("hu-HU")
                };
            if (nameSmall == "nl")
                return new IntLangZ()
                {
                    ID = 6,
                    Name = Helper.UrlDecode("Nederlands"),
                    ISO = new CultureInfo("nl"),
                    CI = new CultureInfo("nl-NL")
                };
            if (nameSmall == "nb")
                return new IntLangZ()
                {
                    ID = 7,
                    Name = Helper.UrlDecode("Norsk"),
                    ISO = new CultureInfo("no"),
                    CI = new CultureInfo("nb-NO")
                };
            if (nameSmall == "pl")
                return new IntLangZ()
                {
                    ID = 21,
                    Name = Helper.UrlDecode("Polski"),
                    ISO = new CultureInfo("pl"),
                    CI = new CultureInfo("pl-PL")
                };
            if (nameFull == "pt-br")
                return new IntLangZ()
                {
                    ID = 23,
                    Name = Helper.UrlDecode("Portugu%c3%aas+(Brazil)"),
                    ISO = new CultureInfo("pt-br"),
                    CI = new CultureInfo("pt-BR")
                };
            if (nameSmall == "pt")
                return new IntLangZ()
                {
                    ID = 5,
                    Name = Helper.UrlDecode("Portugu%c3%aas+(Europe)"),
                    ISO = new CultureInfo("pt"),
                    CI = new CultureInfo("pt-PT")
                };
            if (nameSmall == "ro")
                return new IntLangZ()
                {
                    ID = 18,
                    Name = Helper.UrlDecode("Romana"),
                    ISO = new CultureInfo("ro"),
                    CI = new CultureInfo("ro-RO")
                };
            if (nameSmall == "ru")
                return new IntLangZ()
                {
                    ID = 25,
                    Name = Helper.UrlDecode("%d1%80%d1%83%d1%81%d1%81%d0%ba%d0%b8%d0%b9"),
                    ISO = new CultureInfo("ru"),
                    CI = new CultureInfo("ru-RU")
                };
            if (nameSmall == "sv")
                return new IntLangZ()
                {
                    ID = 10,
                    Name = Helper.UrlDecode("Svenska"),
                    ISO = new CultureInfo("sv"),
                    CI = new CultureInfo("sv-SE")
                };
            if (nameSmall == "fi")
                return new IntLangZ()
                {
                    ID = 11,
                    Name = Helper.UrlDecode("Suomi"),
                    ISO = new CultureInfo("fi"),
                    CI = new CultureInfo("fi-FI")
                };
            if (nameSmall == "sk")
                return new IntLangZ()
                {
                    ID = 17,
                    Name = Helper.UrlDecode("Sloven%c4%8dinu"),
                    ISO = new CultureInfo("sk"),
                    CI = new CultureInfo("sk-sk")
                };
            if (nameSmall == "ar")
                return new IntLangZ()
                {
                    ID = 26,
                    Name = Helper.UrlDecode("%d8%b9%d8%b1%d8%a8%d9%8a+(Arabic)"),
                    ISO = new CultureInfo("ar"),
                    CI = new CultureInfo("ar-SA")
                };
            if (nameFull == "zh-cn")
                return new IntLangZ()
                {
                    ID = 13,
                    Name = Helper.UrlDecode("%e4%b8%ad%e6%96%87+(SIM)"),
                    ISO = new CultureInfo("zh-cn"),
                    CI = new CultureInfo("zh-cn")
                };
            if (nameFull == "zh-tw")
                return new IntLangZ()
                {
                    ID = 14,
                    Name = Helper.UrlDecode("%e4%b8%ad%e6%96%87+(Taiwan)"),
                    ISO = new CultureInfo("zh-tw"),
                    CI = new CultureInfo("zh-tw")
                };
            if (nameFull == "zh-hk")
                return new IntLangZ()
                {
                    ID = 12,
                    Name = Helper.UrlDecode("%e4%b8%ad%e6%96%87+(HK)"),
                    ISO = new CultureInfo("zh-hk"),
                    CI = new CultureInfo("en-US")
                };
            //if (nameSmall == "tr")
            //    return new IntLangZ()
            //    {
            //        ID = 31,
            //        Name = Helper.UrlDecode("T%c3%9cRK%c3%87E+(Turkish)"),
            //        ISO = new CultureInfo("tr"),
            //        CI = new CultureInfo("tr-TR")
            //    };
            if (nameSmall == "el")
                return new IntLangZ()
                {
                    ID = 27,
                    Name = Helper.UrlDecode("%ce%95%ce%bb%ce%bb%ce%b7%ce%bd%ce%b9%ce%ba%ce%ac+(Greek)"),
                    ISO = new CultureInfo("el"),
                    CI = new CultureInfo("el-GR")
                };
            if (nameSmall == "ja")
                return new IntLangZ()
                {
                    ID = 29,
                    Name = Helper.UrlDecode("%e6%97%a5%e6%9c%ac%e8%aa%9e+(Japanese)"),
                    ISO = new CultureInfo("ja"),
                    CI = new CultureInfo("ja-JP")
                };
            if (nameSmall == "ko")
                return new IntLangZ()
                {
                    ID = 30,
                    Name = Helper.UrlDecode("%ed%95%9c%ea%b5%ad%ec%96%b4+(Korean)"),
                    ISO = new CultureInfo("ko"),
                    CI = new CultureInfo("ko-KR")
                };
            if (nameSmall == "hi")
                return new IntLangZ()
                {
                    ID = 24,
                    Name = Helper.UrlDecode("%e0%a4%b9%e0%a4%bf%e0%a4%a8%e0%a5%8d%e0%a4%a6%e0%a5%80+(Hindi)"),
                    ISO = new CultureInfo("hi"),
                    CI = new CultureInfo("hi-IN")
                };
            if (nameSmall == "he")
                return new IntLangZ()
                {
                    ID = 33,
                    Name = Helper.UrlDecode("%d7%a2%d7%91%d7%a8%d7%99%d7%aa+(Hebrew)"),
                    ISO = new CultureInfo("he"),
                    CI = new CultureInfo("he-IL")
                };
            return new IntLangZ()
            {
                ID = 1,
                Name = Helper.UrlDecode("English+(US)"),
                ISO = new CultureInfo("en-US"),
                CI = new CultureInfo("en-US")
            };
        }
        #endregion
        #region GetCoordinates
        LocationData GetCoordinates(LocationData location)
        {
            try
            {
                String sData = Helper.GetRequest(new Uri("http://vwidget.accuweather.com/widget/vista1/weather_data_v2.asp?location=" + rePrefix.Match(location.Code).Groups[1].Value), Encoding.UTF8, 15000);
                XDocument doc = XDocument.Parse(sData, LoadOptions.PreserveWhitespace);
                XElement el = doc.Root.Element(XName.Get("local", doc.Root.Name.Namespace.NamespaceName));
                location.Lat = Double.Parse(el.Attribute("lat").Value, nfi);
                location.Lon = Double.Parse(el.Attribute("lon").Value, nfi);
            }
            catch { }
            return location;
        }
        #endregion
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
                String sData = Helper.GetRequest(new Uri("http://vwidget.accuweather.com/widget/vista1/locate_city.asp?location=" + Query), Encoding.UTF8, 30000);
                XDocument doc = XDocument.Parse(sData, LoadOptions.PreserveWhitespace);
                List<LocationData> CityLocationLsit = new List<LocationData>();
                foreach (XElement el in doc.Root.Element(XName.Get("citylist", doc.Root.Name.Namespace.NamespaceName)).Elements())
                {
                    LocationData ld = new LocationData();
                    ld.Code = el.Attribute("location").Value.Trim();
                    ld.Code = prefix + "|" + (ld.Code[ld.Code.Length - 1] == '|' ? ld.Code.Substring(0, ld.Code.Length - 1) : ld.Code);
                    ld.City = el.Attribute("city").Value;
                    ld.Country = el.Attribute("state").Value;
                    CityLocationLsit.Add(ld);
                }
                if (CityLocationLsit.Count > 0) return CityLocationLsit;
            }
            return null;
        }
        #endregion
        #region GetWeatherReport
        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale, WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            bool isMetric = tempScale == TemperatureScale.Celsius;
            IntLangZ ilz = Language(culture);
            Uri url = new Uri("http://www.accuweather.com");
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            string RequestData = String.Empty;
            try
            {
                #region Текущие данные
                if (!rePrefix.IsMatch(location.Code))
                    location.Code = prefix + "|10001";
                url = new Uri("http://www.accuweather.com/quick-look.aspx?loc=" + rePrefix.Match(location.Code).Groups[1].Value);
                request = (HttpWebRequest)WebRequest.Create(url);
                request.SendChunked = false;
                request.Proxy = Helper.GetProxy();
                request.Timeout = 15000;
                request.UserAgent = "Mozilla/4.0 (Compatible; Windows NT 5.1; MSIE 8.0) (compatible; MSIE 8.0; Windows NT 5.1;)";
                request.CookieContainer = new CookieContainer();
                request.CookieContainer.Add(new Cookie("IntLangZ", ilz.ToString(), "/", "www.accuweather.com")); //Язык
                request.CookieContainer.Add(new Cookie("IntPreferencesZ", String.Format("Units={0}", isMetric ? 1 : 0), "/", "www.accuweather.com")); //тип измерения температуры
                response = (HttpWebResponse)request.GetResponse();
                #region Полученные куки
                NameValueCollection nvc = new NameValueCollection();
                if (response.Cookies["IntLocZ"] != null)
                {
                    String coocVal = Helper.UrlDecode(response.Cookies["IntLocZ"].Value as String); //"IntLocZ"Version=4&Name=New+York&OfficialName=New+York&Lat=40.749&Lon=-73.994&S=CT_&U=MANH&CountryName=United+States&CountryID=US&AdMinID=NY&AdminName=New+York&AdminOfficialName=New+York&CountryCode=US&TzId=21&StandardGmtO=-5&CurrentGmtO=-5&LookupType=postal&PC=10001&Climo=NYC&CityID=&VideoCode=LGA
                    if (!String.IsNullOrEmpty(coocVal) && coocVal.IndexOf("=") > -1)
                        foreach (String c in coocVal.Split('&')) nvc.Add(c.Split('=')[0], c.Split('=')[1]);
                }
                #endregion
                RequestData = new StreamReader(response.GetResponseStream(), Encoding.UTF8).ReadToEnd();
                RequestData = Helper.HtmlDecode(RequestData);
                #region Чистка
                if (response != null) { response.Close(); response = null; }
                if (request != null) request = null;
                #endregion
                Regex locRe = new Regex("<a.+?lnkLocation.+?href=\"([^\"]+)\"[^>]*>([^,]+)([^<]*)</a>", RegexOptions.IgnoreCase);
                if (locRe.IsMatch(RequestData))
                {
                    #region Location
                    location = GetCoordinates(location);
                    location.City = locRe.Match(RequestData).Groups[2].Value.Trim();
                    if (!String.IsNullOrEmpty(locRe.Match(RequestData).Groups[3].Value))
                        location.Country = locRe.Match(RequestData).Groups[3].Value.Split(',')[1].Trim();
                    WeatherData result = new WeatherData();
                    result.Location = location;
                    #endregion
                    #region CurentData
                    result.Curent.Url = locRe.Match(RequestData).Groups[1].Value.Trim();
                    Regex imgRe = new Regex("<img.+?imgCurConCondition.+?([\\d]+)_int", RegexOptions.IgnoreCase);
                    if (imgRe.IsMatch(RequestData))
                        result.Curent.SkyCode = Int32.Parse(imgRe.Match(RequestData).Groups[1].Value.Trim());
                    Regex tempRe = new Regex("<span.+?lblCurrentTemp.+?>([^°]+)°", RegexOptions.IgnoreCase);
                    if (tempRe.IsMatch(RequestData))
                        result.Temperature = Int32.Parse(tempRe.Match(RequestData).Groups[1].Value.Trim());
                    Regex descRe = new Regex("<span.+?lblCurrentText.+?>([^<]+)</span>", RegexOptions.IgnoreCase);
                    if (descRe.IsMatch(RequestData))
                        result.Curent.Text = descRe.Match(RequestData).Groups[1].Value.Trim();
                    Regex feelRe = new Regex("<span.+?lblRealFeelValue.+?>([^°]+)°", RegexOptions.IgnoreCase);
                    if (feelRe.IsMatch(RequestData))
                        result.FeelsLike = Int32.Parse(feelRe.Match(RequestData).Groups[1].Value.Trim());
                    Regex humidityRe = new Regex("<span.+?lblHumidityValue.+?>([^%]+)%", RegexOptions.IgnoreCase);
                    if (humidityRe.IsMatch(RequestData))
                        result.Humidity = Int32.Parse(humidityRe.Match(RequestData).Groups[1].Value.Trim());
                    Regex windRe = new Regex("<span.+?lblWindsValue.+?>.*?(\\d+).*?</span>", RegexOptions.IgnoreCase);
                    if (windRe.IsMatch(RequestData))
                        result.WindSpeed = Int32.Parse(windRe.Match(RequestData).Groups[1].Value.Trim());
                    #region Wind Speed
                    WindSpeedScale wScale = isMetric ? WindSpeedScale.Kmh : WindSpeedScale.Mph;
                    if (wScale != windSpeedScale && result.WindSpeed != 0)
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
                    #endregion
                    #region Forecast
                    url = new Uri("http://www.accuweather.com/forecast.aspx?loc=" + rePrefix.Match(location.Code).Groups[1].Value);
                    request = (HttpWebRequest)WebRequest.Create(url);
                    request.SendChunked = false;
                    request.Proxy = Helper.GetProxy();
                    request.Timeout = 15000;
                    request.UserAgent = "Mozilla/4.0 (Compatible; Windows NT 5.1; MSIE 8.0) (compatible; MSIE 8.0; Windows NT 5.1;)";
                    request.CookieContainer = new CookieContainer();
                    request.CookieContainer.Add(new Cookie("IntLangZ", ilz.ToString(), "/", "www.accuweather.com")); //Язык
                    request.CookieContainer.Add(new Cookie("IntPreferencesZ", String.Format("Units={0}", isMetric ? 1 : 0), "/", "www.accuweather.com")); //тип измерения температуры
                    RequestData = new StreamReader(((HttpWebResponse)request.GetResponse()).GetResponseStream(), Encoding.UTF8).ReadToEnd();
                    RequestData = Helper.HtmlDecode(RequestData);
                    if (response != null) { response.Close(); response = null; }
                    if (request != null) request = null;
                    DateTime LocToday = DateTime.Now;
                    Regex curDateRe = new Regex("<span.+?lblDate.+?>([^<]+)</span>", RegexOptions.IgnoreCase);
                    if (curDateRe.IsMatch(RequestData))
                        LocToday = DateTime.Parse(curDateRe.Match(RequestData).Groups[1].Value, ilz.CI);
                    String rStr = "<div.+?ForecastIcon.+?>\\s*<img.+?imgIcon.+?([\\d]+)_int.+?>\\s*</div>\\s*" +
                    "<div.+?ForecastDescription.+?>\\s*" +
                    "<div.+?>\\s*<span.+?lblDate.+?>([^<]+)</span>\\s*</div>\\s*" +
                    "<div.+?>\\s*<span.+?lblDesc.+?>([^<]+)</span>\\s*</div>\\s*" +
                    "<div.+?>.+?<span.+?lblHigh.+?>([^°]+)[^<]*</span>\\s*</div>\\s*" +
                    "<div.+?>.+?</div>\\s*" +
                    ".+?<div.+?><a.+?lnkDetails.+?href=\"([^\"]*)\">";
                    Regex forecastRe = new Regex(rStr, RegexOptions.IgnoreCase);
                    List<ForecastInfo> fiList = new List<ForecastInfo>();
                    if (forecastRe.IsMatch(RequestData))
                        foreach (Match m in forecastRe.Matches(RequestData))
                        {
                            Regex dReg = new Regex("[\\d:.\\/\\-]+", RegexOptions.IgnoreCase);
                            ForecastInfo fi = new ForecastInfo();
                            MatchCollection mc = dReg.Matches(m.Groups[2].Value.Trim().Replace(". ", "."));
                            try { fi.lblDate = DateTime.Parse(mc[mc.Count - 1].Value.Trim(), ilz.CI); }
                            catch
                            {
                                try { fi.lblDate = DateTime.Parse(mc[mc.Count - 1].Value.Trim(), new CultureInfo("en-US")); }
                                catch { }
                            }
                            fi.lblHigh = Int32.Parse(m.Groups[4].Value);
                            fi.lblDesc = m.Groups[3].Value.Trim();
                            fi.lblDesc = fi.lblDesc[0].ToString().ToUpper() + fi.lblDesc.Substring(1);
                            fi.lnkDetails = m.Groups[5].Value;
                            fi.ForecastIcon = Int32.Parse(m.Groups[1].Value);
                            fiList.Add(fi);
                        }

                    //int counter = 0;
                    for (int i = 0; i < fiList.Count / 2; i++)
                    {
                        if (i == 0 && LocToday != fiList[i].lblDate) continue;
                        result.ForecastList.Add(new ForecastData()
                        {
                            SkyCode = fiList[i].ForecastIcon,
                            Text = fiList[i].lblDesc /* + (fiList[i + fiList.Count / 2].lblDesc != fiList[i].lblDesc ? "\r\n" + fiList[i + fiList.Count / 2].lblDesc : String.Empty)*/,
                            Url = fiList[i].lnkDetails,
                            HighTemperature = fiList[i].lblHigh,
                            LowTemperature = fiList[i + fiList.Count / 2].lblHigh
                        });
                    }
                    if (result.ForecastList.Count > 0)
                    {
                        result.Curent.HighTemperature = result.ForecastList[0].HighTemperature;
                        result.Curent.LowTemperature = result.ForecastList[0].LowTemperature;
                        return result;
                    }
                    #endregion
                }
                #endregion
            }
            catch { }
            return null;
        }
        #endregion
    }

    #region IntLangZ
    public class IntLangZ
    {
        public Int32 ID;
        public String Name;
        public CultureInfo ISO;
        public CultureInfo CI;
        public override string ToString()
        {
            String ret = String.Empty;
            ret += ID > 0 ? "ID=" + ID.ToString() : String.Empty;
            ret += (ret != String.Empty ? "&" : String.Empty) + (!String.IsNullOrEmpty(Name) ? "Name=" + Helper.UrlEncode(Name) : String.Empty);
            ret += (ret != String.Empty ? "&" : String.Empty) + (ISO != null ? "ISO=" + ISO.IetfLanguageTag.ToLower() : String.Empty);
            return ret;
        }
    }
    #endregion
    #region ForecastInfo
    public class ForecastInfo
    {
        public DateTime lblDate;
        public Int32 ForecastIcon;
        public String lblDesc;
        public Int32 lblHigh;
        public String lnkDetails;
    }
    #endregion
}
