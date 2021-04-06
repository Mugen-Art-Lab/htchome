using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.IO;

namespace HTCHome.Core
{
    public class LocaleManager
    {
        private ResourceDictionary locale;

        public string LocaleCode
        {
            get;
            set;
        }

        public string LocaleBasePath
        {
            get;
            set;
        }

        public LocaleManager(string localeBasePath)
        {
            locale = new ResourceDictionary();
            LocaleBasePath = localeBasePath;
        }

        public void LoadLocale(string localeCode)
        {
            LocaleCode = localeCode;
            locale.Source = new Uri(LocaleBasePath + "\\" + LocaleCode + ".xaml");
        }

        public static string GetLocaleName(string path)
        {
            ResourceDictionary l = new ResourceDictionary();
            l.Source = new Uri(path);
            if (l["LocaleName"] != null)
                return l["LocaleName"].ToString();
            return String.Empty;
        }

        public static string GetLocaleCode(string path)
        {
            ResourceDictionary l = new ResourceDictionary();
            l.Source = new Uri(path);
            if (l["LocaleCode"] != null)
                return l["LocaleCode"].ToString();
            return String.Empty;
        }

        public string GetString(string s)
        {
            if (locale[s] != null)
                return locale[s].ToString();
            return String.Empty;
        }
    }
}
