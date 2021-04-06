using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebInstaller
{
    public static class E
    {
        public static string InstallPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\HTC Home";
        public static bool DesktopShortcut = false;
        public static bool StartMenuShortcut = true;
        public static bool ShowShield = false;
        public static bool SkipStartPage = false;
    }
}
