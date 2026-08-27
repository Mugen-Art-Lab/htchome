using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace HTCHome.Core
{
    public static class Logger
    {
        private static string GetLogPath()
        {
            var fileName = string.IsNullOrEmpty(Environment.ProfileId)
                ? "log.txt"
                : "log-" + Environment.ProfileId + ".txt";
            return System.IO.Path.Combine(Environment.LogsPath, fileName);
        }

        public static void Log(string s)
        {
            var logPath = GetLogPath();
            if (!File.Exists(logPath))
                File.WriteAllText(logPath, "");
            try
            {
                File.AppendAllText(logPath, DateTime.Now + " -------------- " + (char)(13) + (char)(10) + "OS: " + System.Environment.OSVersion.VersionString + (char)(13) + (char)(10) + "Profile: " + (string.IsNullOrEmpty(Environment.ProfileId) ? "legacy" : Environment.ProfileId) + (char)(13) + (char)(10) + s + (char)(13) + (char)(10));
            }
            catch { }
        }
    }
}
