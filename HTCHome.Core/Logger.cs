using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace HTCHome.Core
{
    public static class Logger
    {
        public static void Log(string s)
        {
            if (!File.Exists(Environment.ConfigDirectory + "\\log.txt"))
                File.WriteAllText(Environment.ConfigDirectory + "\\log.txt", "");
            try
            {
                File.AppendAllText(Environment.ConfigDirectory + "\\log.txt", DateTime.Now.ToString() + " -------------- " + s + (char)(13) + (char)(10));
            }
            catch { }
        }
    }
}
