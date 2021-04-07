using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Vkontakte.VkApi
{
    public class VkClient
    {
        //Используется внутри большинства методов для отправки запросов серверу
        private static HttpWebResponse SendRequest(string query)
        {
            //Отправляем запрос
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(query));
            request.Method = "POST";
            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; .NET4.0C; .NET4.0E)";
            //request.Referer = "http://vkontakte.ru";
            request.Accept = "image/jpeg, application/x-ms-application, image/gif, application/xaml+xml, image/pjpeg, application/x-ms-xbap, application/msword, */*";
            request.Headers.Add(HttpRequestHeader.AcceptLanguage, "ru-RU");
            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            request.ContentType = "application/x-www-form-urlencoded";
            request.KeepAlive = true;
            request.AllowAutoRedirect = false;
            request.AutomaticDecompression = DecompressionMethods.GZip;

            //возвращаем ответ
            return (HttpWebResponse)request.GetResponse();
        }

        /// <summary>
        /// Вызывает указанный метод
        /// </summary>
        /// <param name="method">Название метода</param>
        /// <param name="param">Параметры</param>
        /// <returns>Результат метода</returns>
        public static string InvokeMethod(string method, List<string> param, string app_id)
        {
            //http://api.vkontakte.ru/api.php
            var response = SendRequest(string.Format("https://api.vkontakte.ru/method/{0}?{1}&access_token={2}", method, string.Join("&", param.ToArray()), app_id));
            var reader = new StreamReader(response.GetResponseStream());
            return reader.ReadToEnd();
        }
    }
}
