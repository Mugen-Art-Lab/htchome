using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Home.Base
{
    /// <summary>
    /// Environment
    /// </summary>
    public static class E
    {
        public static readonly string Root;
        public static Version Version;
        public static string VersionString;
        public static string ExtPath;

        static E()
        {
            Root = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Version = Assembly.GetExecutingAssembly().GetName().Version;
            var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            VersionString = Assembly.GetExecutingAssembly().GetName().Version.ToString() + ".int." + fileInfo.LastWriteTimeUtc.ToString("yyMMdd-HHmm");
            ExtPath = Root + "\\Extras";

            foreach (var file in Directory.GetFiles(Root, "*.bak", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch {}
            }
        }
    }
}
