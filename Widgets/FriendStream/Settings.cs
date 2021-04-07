using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Home.Base;

namespace FriendStream
{
    public class Settings : XmlSerializable
    {
        public Settings()
        {
            UseAero = Environment.OSVersion.Version.Major >= 6;
            Autostart = false;
            Language = CultureInfo.CurrentUICulture.Name;
            TopMost = false;
            Left = -100.0f;
            Top = -100.0f;
            Scale = 1.0;
            UseTrayIcon = (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 1); //if not win7 use tray icon
            UseSoftwareRendering = false;
            Pin = false;
            DisableUnminimizer = true;
            CheckForUpdates = true;
            SilentUpdate = false;
            Opacity = 1;
            UpdateInterval = 60;
            CheckForUpdates = true;
            Debug = false;
            OptionsWidth = 520;
            OptionsHeight = 480;
            ActiveProviders = new List<string>();
            MaxEntries = 50;
            RefreshInterval = 10;
            Style = Styles.Classic;
        }

        public bool UseAero { get; set; }
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
        public double Opacity { get; set; }
        public int UpdateInterval { get; set; }
        public bool Debug { get; set; }
        public double OptionsWidth { get; set; }
        public double OptionsHeight { get; set; }
        public List<string> ActiveProviders { get; set; }
        public int MaxEntries { get; set; }   
        public int RefreshInterval { get; set; }
        public Styles Style { get; set; }
    }
}
