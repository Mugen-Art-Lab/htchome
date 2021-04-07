using System;
using System.ComponentModel;
using System.Globalization;
using Home.Base;

namespace Update
{
    public class Settings : XmlSerializable
    {
        public Settings()
        {
            UseProxy = false;
        }

        public bool UseProxy { get; set; }
        public string ProxyAddress { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUsername { get; set; }
        public string ProxyPassword { get; set; }
    }
}
