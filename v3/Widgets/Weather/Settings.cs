using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Home.Base;
using Weather.Base;

namespace Weather
{
    public class Settings : XmlSerializable
    {
        public Settings()
        {
            Autostart = false;
            Language = CultureInfo.CurrentUICulture.Name;
            Style = Styles.Large;
            UseAero = false;
            Left = -100;
            Top = -100;
            ShowFeelsLike = false;
            ShowHW = false;
            RefreshInterval = 20;
            TempScale = TemperatureScale.Celsius;
            WindSpeedScale = WindSpeedScale.Kmh;
            Provider = "MSN";
            Scale = 1.0;
            UseTrayIcon = (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 1); //if not win7 use tray icon
            TopMost = false;
            UseSoftwareRendering = false;
            Pin = false;
            DisableUnminimizer = true;
            CheckForUpdates = false;
            UseProxy = true;
            SilentUpdate = false;
            Opacity = 1;
            UpdateInterval = 60;
            EnableSounds = true;
            OptionsWidth = 520;
            OptionsHeight = 480;
            EnableOverlayIcon = Environment.OSVersion.Version.Major >= 6 && Environment.OSVersion.Version.Minor >= 1;
        }

        public bool Autostart { get; set; }
        public string Language { get; set; }
        public Styles Style { get; set; }
        public bool UseAero { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public string LocationCode { get; set; }
        public bool ShowFeelsLike { get; set; }
        public bool ShowHW { get; set; }
        public double RefreshInterval { get; set; }
        public TemperatureScale TempScale { get; set; }
        public WindSpeedScale WindSpeedScale { get; set; }
        public string Provider { get; set; }
        public double Scale { get; set; }
        public bool UseTrayIcon { get; set; }
        public bool TopMost { get; set; }
        public bool UseSoftwareRendering { get; set; }
        public bool Pin { get; set; }
        public bool DisableUnminimizer { get; set; }
        public bool CheckForUpdates { get; set; }
        public bool SilentUpdate { get; set; }
        public double Opacity { get; set; }
        public int UpdateInterval { get; set; } 
        public bool EnableSounds { get; set; }
        public double OptionsWidth { get; set; }
        public double OptionsHeight { get; set; }
        public bool UseProxy { get; set; }
        public string ProxyAddress { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUsername { get; set; }
        public string ProxyPassword { get; set; }
        public bool EnableOverlayIcon { get; set; }
    }
}
