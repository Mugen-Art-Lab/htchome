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

namespace Foreca3
{
    public class WeatherProvider : IWeatherProvider
    {
        //Version ver = new Version();
        //public WeatherProvider()
        //{
        //    ver = new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version;
        //}

        const String prefix = "foreca";
        Regex rePrefix = new Regex(prefix + "\\|(.+)$");
        NumberFormatInfo nfi = new NumberFormatInfo() { CurrencyDecimalSeparator = "." };
        #region GetLocations
        public List<LocationData> GetLocations(string query, CultureInfo culture, TemperatureScale tempScale)
        {
            return GetLocations(query, culture);
        }
        public List<LocationData> GetLocations(string query, CultureInfo culture)
        {
            //new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.GetFiles("providers_set.xml");
            if (!String.IsNullOrEmpty(query) && !String.IsNullOrEmpty(query.Trim()))
            {
                String Query = query.Trim();
                try
                {
                    HttpWebRequest request = null;
                    HttpWebResponse response = null;
                    byte[] bytes = new byte[0];
                    Stream os = null;

                    Uri url = new Uri("http://www.foreca.com");
                    bool isEng = true;
                    string result = String.Empty;
                    string resultEn = String.Empty;
                    isEng = culture.TwoLetterISOLanguageName.ToLower() == "en";
                    url = new Uri(GetLanguageUrl(culture));
                    #region language cookie
                    if (!isEng)
                        try
                        {
                            request = (HttpWebRequest)WebRequest.Create(url);
                            request.SendChunked = false;
                            request.Proxy = Helper.GetProxy();
                            request.Timeout = 15000;
                            request.UserAgent = "Mozilla/4.0 (Compatible; Windows NT 5.1; MSIE 8.0) (compatible; MSIE 8.0; Windows NT 5.1;)";
                            request.CookieContainer = new CookieContainer();
                            request.AllowAutoRedirect = true;
                            response = (HttpWebResponse)request.GetResponse();
                        }
                        catch { isEng = true; }
                    #endregion
                    Uri hostUrl = new Uri(url.Scheme + "://" + url.Host + ":" + url.Port.ToString() + "/complete.php");
                    #region Search locations
                    #region languge result
                    if (!isEng)
                    {
                        HttpWebRequest request1 = (HttpWebRequest)WebRequest.Create(hostUrl);
                        request1.SendChunked = false;
                        request1.Proxy = Helper.GetProxy();
                        request1.Timeout = 15000;
                        request1.UserAgent = "Mozilla/4.0 (Compatible; Windows NT 5.1; MSIE 8.0) (compatible; MSIE 8.0; Windows NT 5.1;)";
                        request1.Method = "POST";
                        request1.ContentType = "application/x-www-form-urlencoded";
                        request1.CookieContainer = new CookieContainer();
                        if (response != null)
                            foreach (Cookie c in response.Cookies)
                                request1.CookieContainer.Add(c);
                        bytes = Encoding.ASCII.GetBytes("q=" + Helper.UrlEncode(Query));
                        try
                        {
                            request1.ContentLength = bytes.Length;
                            os = request1.GetRequestStream();
                            os.Write(bytes, 0, bytes.Length);
                            os.Close();
                            HttpWebResponse response1 = (HttpWebResponse)request1.GetResponse();
                            result = new StreamReader(response1.GetResponseStream(), Encoding.UTF8).ReadToEnd();
                        }
                        catch { isEng = true; }
                        finally { }
                    }
                    #endregion
                    if (response != null)
                        response.Close();
                    response = null;
                    request = null;
                    #region english result
                    url = new Uri(GetLanguageUrl(new CultureInfo("en-US")));
                    hostUrl = new Uri(url.Scheme + "://" + url.Host + ":" + url.Port.ToString() + "/complete.php");
                    request = (HttpWebRequest)WebRequest.Create(hostUrl);
                    request.SendChunked = false;
                    request.Proxy = Helper.GetProxy();
                    request.Timeout = 15000;
                    request.UserAgent = "Mozilla/4.0 (Compatible; Windows NT 5.1; MSIE 8.0) (compatible; MSIE 8.0; Windows NT 5.1;)";
                    request.Method = "POST";
                    request.ContentType = "application/x-www-form-urlencoded";
                    bytes = Encoding.ASCII.GetBytes("q=" + Helper.UrlEncode(Query));
                    try
                    {
                        request.ContentLength = bytes.Length;
                        os = request.GetRequestStream();
                        os.Write(bytes, 0, bytes.Length);
                        os.Close();
                        response = (HttpWebResponse)request.GetResponse();
                        resultEn = new StreamReader(response.GetResponseStream(), Encoding.UTF8).ReadToEnd();
                    }
                    catch { isEng = true; }
                    finally { }
                    #endregion
                    #region Clear streams
                    response.Close(); response = null;
                    request = null;
                    if (os != null) { os.Close(); os.Dispose(); os = null; }
                    #endregion
                    if (isEng || String.IsNullOrEmpty(result)) result = resultEn;
                    if (resultEn.IndexOf("<li style") > 0)
                        resultEn = resultEn.Substring(0, resultEn.IndexOf("<li style"));
                    Regex liRe = new Regex("<li id=\"(\\d+)\">(.+?)</li>", RegexOptions.IgnoreCase);
                    if (liRe.IsMatch(resultEn))
                    {
                        List<LocationData> CityLocationLsit = new List<LocationData>();
                        #region Get list data
                        for (int mi = 0; mi < liRe.Matches(resultEn).Count; mi++)
                        {
                            Match mv = liRe.Matches(result)[mi];
                            String locName = new Regex("</?[^>]+>").Replace(mv.Groups[2].Value, String.Empty);
                            String locCountry = String.Empty;
                            if (locName.Split(',').Length > 1)
                            {
                                locCountry = locName.Substring(locName.IndexOf(',') + 1).Trim();
                                locName = locName.Split(',')[0].Trim();
                            }
                            if (locCountry.IndexOf(",") > 0)
                                locCountry = locCountry.Split(',')[1].Trim() + ", " + locCountry.Split(',')[0].Trim();
                            CityLocationLsit.Add(new LocationData()
                            {
                                Code = prefix + "|" + mv.Groups[1].Value,
                                City = locName,
                                Country = locCountry,
                                Lat = Double.MinValue,
                                Lon = Double.MinValue
                            });
                        }
                        #endregion
                        return CityLocationLsit;
                    }
                    #endregion
                }
                catch { }
            }
            return null;
        }
        #endregion
        #region GetWeatherReport
        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale, WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            bool isMetric = tempScale == TemperatureScale.Celsius;
            Uri url = new Uri(GetLanguageUrl(culture));
            //if (!rePrefix.IsMatch(location.Code)) { }
            Uri hostUrl = new Uri(url.ToString() + (!String.IsNullOrEmpty(url.Query) ? "&" : "?") + "id=" + rePrefix.Match(location.Code).Groups[1].Value + "&quick_units=" + (isMetric ? "metric" : "us"));
            String CurentDataUrl = hostUrl.ToString();
            String CurentData = Helper.HtmlDecode(Helper.GetRequest(new Uri(CurentDataUrl), Encoding.UTF8, 30000));
            if (string.IsNullOrEmpty(CurentData)) return null;
            WeatherData result = new WeatherData();
            #region Location name
            Regex CityNameRe = new Regex("<h1 class=\"entry-title\">([^<]+)</h1>", RegexOptions.IgnoreCase);
            if (CityNameRe.IsMatch(CurentData))
                location.City = CityNameRe.Match(CurentData).Groups[1].Value.Trim();
            else
            {
                /*арабский*/
                CityNameRe = new Regex("<div class=\"to-left\">\\s*<h1[^>]*>([^<]+)</h1>\\s*</div>", RegexOptions.IgnoreCase);
                if (CityNameRe.IsMatch(CurentData))
                    location.City = CityNameRe.Match(CurentData).Groups[1].Value.Trim();
                else
                {
                    CityNameRe = new Regex("<div class=\"to-left\">\\s*<h1[^>]*>([^<]+)</h1>\\s*</div>", RegexOptions.IgnoreCase);
                    if (CityNameRe.IsMatch(CurentData))
                        location.City = CityNameRe.Match(CurentData).Groups[1].Value.Trim();
                }
            }
            if (location.City.IndexOf(",") > 0)
                location.City = location.City.Split(',')[0].Trim();
            result.Location = location;
            #endregion
            #region Curent data
            String CurentReStr = "<div class=\"left\">\\s*" +
                    "<div class=\"symbol_70x70\\w{1}\\s*symbol_([^_]+)[^>]*></div>\\s*" +
                    "<span[^>]*>\\s*<strong[^>]*>" +
                    "(.*?\\d+)</strong>.*?</span>\\s*<br[^>]*>\\s*" +
                    "(<img[^>]*>\\s*<strong[^>]*>\\s*(\\d+).*?</strong>\\s*<br[^>]*>)*\\s*" +
                    "</div>\\s*" +
                    "<div[^>]*>\\s*([^<]*)<br[^>]*>[^<]*<strong[^>]*>\\s*(.*?\\d+).*?</strong>\\s*<br[^>]*>\\s*" +
                    "[^<]*<strong[^>]*>[^<]*</strong>\\s*<br[^>]*>\\s*[^<]*<strong[^>]*>[^<]*</strong>\\s*<br[^>]*>\\s*" +
                    "[^<]*<strong[^>]*>(.*?)%\\s*</strong>";

            Regex CurentRe = new Regex(CurentReStr, RegexOptions.IgnoreCase);
            if (CurentRe.IsMatch(CurentData))
            {
                result.Curent.SkyCode = GetWeatherPic(CurentRe.Match(CurentData).Groups[1].Value.Trim());
                result.Curent.Text = CurentRe.Match(CurentData).Groups[5].Value.Trim();
                result.Curent.Url = CurentDataUrl;
                Int32.TryParse(CurentRe.Match(CurentData).Groups[2].Value.Trim(), out result.Temperature);
                Int32.TryParse(CurentRe.Match(CurentData).Groups[4].Value.Trim(), out result.WindSpeed);
                Int32.TryParse(CurentRe.Match(CurentData).Groups[6].Value.Trim(), out result.FeelsLike);
                Double hTmp = 0.0;
                Double.TryParse(CurentRe.Match(CurentData).Groups[7].Value.Trim(), NumberStyles.Any, nfi, out hTmp);
                result.Humidity = Convert.ToInt32(Math.Round(hTmp));
            }
            else
            {
                #region Arabic and old version
                CurentReStr = "<div class=\"left\">\\s*" +
                    "<img class=\"symb\" src=\"/img/symb-70x70/([^\\.]+).png\"[^>]*>\\s*" +
                    "<span[^>]*>\\s*<strong[^>]*>" +
                    "(.*?\\d+)</strong>.*?</span>\\s*<br[^>]*>\\s*" +
                    "(<img[^>]*>\\s*<strong[^>]*>\\s*(\\d+).*?</strong>\\s*<br[^>]*>)*\\s*" +
                    "</div>\\s*" +
                    "<div[^>]*>\\s*"+
                    "([^<]*)<br[^>]*>"+
                    "[^<]*<strong[^>]*>\\s*(<span[^>]*>)*\\s*(.*?\\d+).*?(</span>)*\\s*</strong>\\s*<br[^>]*>\\s*" +
                    "[^<]*<strong[^>]*>[^<]*</strong>\\s*<br[^>]*>\\s*"+
                    "[^<]*<strong[^>]*>\\s*(<span[^>]*>)*[^<]*(</span>)*\\s*</strong>\\s*<br[^>]*>\\s*" +
                    "[^<]*<strong[^>]*>(.*?)%\\s*</strong>";

                CurentRe = new Regex(CurentReStr, RegexOptions.IgnoreCase);
                if (CurentRe.IsMatch(CurentData))
                {
                    result.Curent.SkyCode = GetWeatherPic(CurentRe.Match(CurentData).Groups[1].Value.Trim());
                    result.Curent.Text = CurentRe.Match(CurentData).Groups[5].Value.Trim();
                    result.Curent.Url = CurentDataUrl;
                    Int32.TryParse(CurentRe.Match(CurentData).Groups[2].Value.Trim(), out result.Temperature);
                    Int32.TryParse(CurentRe.Match(CurentData).Groups[4].Value.Trim(), out result.WindSpeed);
                    Int32.TryParse(CurentRe.Match(CurentData).Groups[7].Value.Trim(), out result.FeelsLike);
                    Double hTmp = 0.0;
                    Double.TryParse(CurentRe.Match(CurentData).Groups[11].Value.Trim(), NumberStyles.Any, nfi, out hTmp);
                    result.Humidity = Convert.ToInt32(Math.Round(hTmp));
                }
                else return null;
                #endregion
            }
            #region Wind Speed
            WindSpeedScale wScale = isMetric ? WindSpeedScale.Ms : WindSpeedScale.Mph;
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
            #endregion
            #region Forecast
            String ForecaDataUrl = hostUrl.ToString() + "&tenday";
            String ForecaData = Helper.HtmlDecode(Helper.GetRequest(new Uri(ForecaDataUrl), Encoding.UTF8, 30000));
            Regex ForecastRe = new Regex("<div class=\"symbol_50x50d symbol_([^_]+)_50x50\"\\s*alt=\"([^\\\"]*)\"[^>]*></div>\\s*<br[^>]*>\\s*[^:]+:\\s*<strong>([^°]+)°</strong><br[^>]*>\\s*[^:]+:\\s*<strong>([^°]+)°</strong><br[^>]*>", RegexOptions.IgnoreCase);
            if (ForecastRe.IsMatch(ForecaData))
            {
                DateTime date = DateTime.Today;
                Int32 num = 0;
                foreach (Match m in ForecastRe.Matches(ForecaData))
                    result.ForecastList.Add(new ForecastData()
                    {
                        SkyCode = GetWeatherPic(m.Groups[1].Value.Trim()),
                        Text = m.Groups[2].Value.Trim(),
                        HighTemperature = Convert.ToInt32(m.Groups[3].Value.Trim()),
                        LowTemperature = Convert.ToInt32(m.Groups[4].Value.Trim()),
                        Url = hostUrl.ToString() + "&details=" + DateTime.Today.AddDays(num++).ToString("yyyyMMdd")
                    });
                result.Curent.HighTemperature = result.ForecastList[0].HighTemperature;
                result.Curent.LowTemperature = result.ForecastList[0].LowTemperature;
                return result;
            }
            else
            {
                ForecastRe = new Regex("<img src=\"/img/symb-50x50/([^\\.]+).png\"\\s*alt=\"([^\\\"]*)\"[^>]*><br />\\s*"+
                    "[^:]+:\\s*<strong>([^°]+)°</strong>\\s*(</abbr>)*\\s*<br />\\s*[^:]+:\\s*<strong>([^°]+)°</strong>\\s*(</abbr>)*\\s*<br />\\s*", RegexOptions.IgnoreCase);
                if (ForecastRe.IsMatch(ForecaData))
                {
                    DateTime date = DateTime.Today;
                    Int32 num = 0;
                    foreach (Match m in ForecastRe.Matches(ForecaData))
                        result.ForecastList.Add(new ForecastData()
                        {
                            SkyCode = GetWeatherPic(m.Groups[1].Value.Trim()),
                            Text = m.Groups[2].Value.Trim(),
                            HighTemperature = Convert.ToInt32(m.Groups[3].Value.Trim()),
                            LowTemperature = Convert.ToInt32(m.Groups[5].Value.Trim()),
                            Url = hostUrl.ToString() + "&details=" + DateTime.Today.AddDays(num++).ToString("yyyyMMdd")
                        });
                    if (result.ForecastList.Count > 0)
                    {
                        result.Curent.HighTemperature = result.ForecastList[0].HighTemperature;
                        result.Curent.LowTemperature = result.ForecastList[0].LowTemperature;
                        return result;
                    }
                }
            }
            #endregion
            return null;
        }
        #endregion
        #region GetWeatherPic
        private int GetWeatherPic(string icon)
        {
            try
            {
                //Облачность 0-Ясно, 1-Небольшая облачность, 2-Переменная облачность, 3-Облачно с прояснениями, 4-Облачно
                //Сила осадков 0-нет, 1-слабый, 2-тип, 3-сильный, 4-гроза
                //Тип осадков 0-дождь,1-осадки 2-снег
                bool isDay = icon.ToLower()[0] == 'd';
                switch (Convert.ToInt32(icon.Substring(1)))
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
            catch { return 1; }
        }
        #endregion
        #region GetLanguageUrl
        String GetLanguageUrl(CultureInfo LCode)
        {
            try
            {
                if (LCode == new CultureInfo("pt-pt"))
                    return "http://www.foreca.com/?lang=pt_pt";             //Português europeu
                if (LCode == new CultureInfo("pt-br"))
                    return "http://www.foreca.com/?lang=pt_br";             //Português brasileiro
                switch (LCode.TwoLetterISOLanguageName.ToLower())
                {
                    //case "ru": return "http://foreca.com/?lang=ru";       //Русский
                    case "ru": return "http://foreca.ru/";                  //Русский
                    case "ar": return "http://foreca.com/?lang=ar";         //الْعَرَبيّة
                    case "bn": return "http://foreca.in/?lang=bn";          //বাংলা
                    case "bg": return "http://foreca.in/?lang=bg";          //български
                    case "cs": return "http://foreca.cz/?lang=cs";          //Čeština
                    case "da": return "http://foreca.dk/?lang=da";          //Dansk
                    case "de": return "http://foreca.de/?lang=de";          //Deutsch
                    case "et": return "http://foreca.com/?lang=et";         //Eesti
                    case "el": return "http://foreca.gr/?lang=el";          //Ελληνικά
                    case "es": return "http://foreca.es/?lang=es";          //Español
                    case "fr": return "http://foreca.com/?lang=fr";         //Français
                    case "gu": return "http://foreca.in/?lang=gu";          //ગુજરાતી
                    case "hi": return "http://foreca.in/?lang=hi";          //हिन्दी
                    case "hr": return "http://foreca.com/?lang=hr";         //Hrvatski
                    case "kn": return "http://foreca.in/?lang=kn";          //ಕನ್ನಡ
                    case "lt": return "http://foreca.com/?lang=lt";         //Lietuvių
                    case "hu": return "http://foreca.hu/?lang=hu";          //Magyar
                    case "ml": return "http://foreca.in/?lang=ml";          //മലയാളം
                    case "is": return "http://foreca.com/?lang=is";         //Íslenska
                    case "it": return "http://foreca.it/?lang=it";          //Italiano
                    case "nl": return "http://foreca.nl/?lang=nl";          //Nederlands
                    case "ja": return "http://foreca.com/?lang=ja";         //日本語
                    case "pl": return "http://foreca.pl/?lang=pl";          //Polski
                    case "pa": return "http://foreca.in/?lang=pa";          //ਪੰਜਾਬੀ
                    case "ro": return "http://foreca.ro/?lang=ro";          //Româneşte
                    case "sr": return "http://foreca.com/?lang=sr";         //Srpski
                    case "fi": return "http://foreca.fi";                   //Suomi
                    case "sv": return "http://foreca.se/?lang=sv";          //Svenska
                    case "ta": return "http://foreca.in/?lang=ta";          //தமிழ்
                    case "te": return "http://foreca.in/?lang=te";          //తెలుగు
                    case "tr": return "http://foreca.com/?lang=tr";         //Türkçe
                    case "th": return "http://foreca.com/?lang=th";         //ไทย
                    case "uk": return "http://foreca.com/?lang=uk";         //Українська
                    case "sq": return "http://foreca.com/?lang=sq";         //Shqip
                    case "lv": return "http://foreca.lv";                   //Latviešu
                    default: return "http://foreca.com/?lang=en";           //English
                }
            }
            catch { return "http://foreca.com/?lang=en"; }
        }
        #endregion
    }
}
