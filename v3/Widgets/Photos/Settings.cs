using System;
using System.Globalization;
using Home.Base;

namespace Photos
{
    public class Settings : XmlSerializable
    {
        public Settings()
        {
            Autostart = false;
            Language = CultureInfo.CurrentUICulture.Name;
            TopMost = false;
            Left = -100.0f;
            Top = -100.0f;
            Scale = 0.6;
            UseTrayIcon = (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 1); //if not win7 use tray icon
            UseSoftwareRendering = false;
            Pin = false;
            DisableUnminimizer = true;
            CheckForUpdates = true;
            SilentUpdate = false;
            PicsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\Web\\Wallpaper";
            SwitchRandom = true;
            Opacity = 1;
            UpdateInterval = 60;
            SwitchAutomatically = true;
            SwitchInterval = 5;
            CheckForUpdates = true;
            Debug = false;
            MaxSize = 500;
            Angle = 0;
            OptionsWidth = 520;
            OptionsHeight = 480;
        }

        public bool Autostart { get; set; }
        public string Language { get; set; }
        public bool TopMost { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public bool UseTrayIcon { get; set; }
        public bool UseSoftwareRendering { get; set; }
        public bool Pin { get; set; }
        public bool DisableUnminimizer { get; set; }
        public bool CheckForUpdates { get; set; }
        public bool SilentUpdate { get; set; }
        public double Scale { get; set; }
        public double MaxSize { get; set; }
        public string PicsFolder { get; set; }
        public bool SwitchRandom { get; set; }
        public double Opacity { get; set; }
        public int UpdateInterval { get; set; }
        public bool SwitchAutomatically { get; set; }
        public double SwitchInterval { get; set; }
        public bool Debug { get; set; }
        public double Angle { get; set; }
        public double OptionsWidth { get; set; }
        public double OptionsHeight { get; set; }
    }
}
