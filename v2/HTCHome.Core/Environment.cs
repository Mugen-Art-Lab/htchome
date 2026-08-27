using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HTCHome.Core
{
    //This class provides some info for widgets (e.g. path to widgets folder, locale, etc.)
    public static class Environment
    {
        public static string Root;
        public static string Path;
        public static string ConfigDirectory;
        public static string Locale;
        public static string ExtensionsPath;
        public static string LogsPath;

        // Mugen fork: non-empty when this host was launched as a named instance.
        // Widgets can use this value to isolate per-user settings for multiple
        // instances running from the same installation directory.
        public static string ProfileId;
        public static string ProfileDirectory;
    }
}
