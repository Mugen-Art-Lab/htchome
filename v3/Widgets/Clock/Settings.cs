using System;
using System.ComponentModel;
using System.Globalization;
using Home.Base;
using Weather.Base;

namespace Clock
{
    public class Settings : XmlSerializable
    {
        public Settings()
        {
            Autostart = false;
            Language = CultureInfo.CurrentUICulture.Name;
            Style = Styles.FlipClock;
            UseAero = Environment.OSVersion.Version.Major >= 6;
            TopMost = false;
            Left = -100;
            Top = -100;
            ShowWeather = true;
            ShowWeatherAnimation = true;
            ShowForecast = false;
            ShowHighLow = true;
            ShowFeelsLike = false;
            RefreshInterval = 20;
            TempScale = TemperatureScale.Celsius;
            WindSpeedScale = WindSpeedScale.Kmh;
            Provider = "MSN";
            DateFormat = "ddd, d MMMM";
            UseCustomTime = false;
            FirstTimeZoneId = TimeZoneInfo.Local.Id;
            SecondTimeZoneId = "UTC";
            FirstClockName = Properties.Resources.ClockDefaultName;
            SecondClockName = "UTC";
            TimeFormat = 1;
            LastHour = 0;
            LastMinute = 0;
            LastSecond = 0;
            ShowClockName = true;
            ClockName = Properties.Resources.ClockDefaultName;
            Scale = 1.0;
            UseTrayIcon = (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 1); //if not win7 use tray icon
            UseSoftwareRendering = false;
            Pin = false;
            DisableUnminimizer = true;
            CheckForUpdates = true;
            UseProxy = false;
            DisableShadows = false;
            SilentUpdate = false;
            Opacity = 1;
            UpdateInterval = 60;
            EnableSounds = true;
            OptionsWidth = 520;
            OptionsHeight = 480;
        }

        public bool Autostart { get; set; }
        public string Language { get; set; }
        public Styles Style { get; set; }
        public bool UseAero { get; set; }
        public bool TopMost { get; set; }
        public string DateFormat { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public string LocationCode { get; set; }
        public bool ShowWeather { get; set; }
        public bool ShowWeatherAnimation { get; set; }
        public bool ShowForecast { get; set; }
        public bool ShowHighLow { get; set; }
        public bool ShowFeelsLike { get; set; }
        public bool ShowWindSpeed { get; set; }
        public bool ShowHumidity { get; set; }
        public bool UseCustomTime { get; set; }
        public string FirstTimeZoneId { get; set; }
        public string SecondTimeZoneId { get; set; }
        public string FirstClockName { get; set; }
        public string SecondClockName { get; set; }
        public int TimeFormat { get; set; } // 0 - 12 hour time, 1 - 24 hour time
        public double RefreshInterval { get; set; }
        public TemperatureScale TempScale { get; set; }
        public WindSpeedScale WindSpeedScale { get; set; }
        public string Provider { get; set; }
        public bool ShowClockName { get; set; }
        public string ClockName { get; set; }
        public int LastHour { get; set; }
        public int LastMinute { get; set; }
        public int LastSecond { get; set; }
        public double Scale { get; set; }
        public bool UseTrayIcon { get; set; }
        public bool UseSoftwareRendering { get; set; }
        public bool Pin { get; set; }
        public bool DisableUnminimizer { get; set; }
        public bool DisableShadows { get; set; }
        public bool CheckForUpdates { get; set; }
        public bool SilentUpdate { get; set; }
        public double Opacity { get; set; }
        public double UpdateInterval { get; set; }
        public bool EnableSounds { get; set; }
        public double OptionsWidth { get; set; }
        public double OptionsHeight { get; set; }
        public bool UseProxy { get; set; }
        public string ProxyAddress { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUsername { get; set; }
        public string ProxyPassword { get; set; }
    }
}
