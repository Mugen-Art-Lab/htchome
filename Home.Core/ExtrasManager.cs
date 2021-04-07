using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;

namespace Home.Base
{
    public class ExtrasManager
    {
        public static List<ExtrasInfo> GetInstalledExtrasInfo()
        {
            var extFiles = from x in Directory.GetFiles(E.ExtPath, "*.ext.xml", SearchOption.AllDirectories)
                           select x;
            if (extFiles.Count() > 0)
            {
                var result = new List<ExtrasInfo>();
                foreach (var e in extFiles)
                {
                    var doc = XDocument.Load(e);
                    var extrasInfo = new ExtrasInfo();
                    try
                    {
                        var cid = doc.Root.Element("Cid");
                        if (cid != null)
                            extrasInfo.Cid = cid.Value;
                        extrasInfo.Title = doc.Root.Element("Title").Value;
                        extrasInfo.Developer = doc.Root.Element("Developer").Value;
                        extrasInfo.Version = doc.Root.Element("Version").Value;
                        extrasInfo.Removable = Convert.ToBoolean(doc.Root.Element("Removable").Value);
                    }
                    catch (Exception)
                    {
                        continue;  
                    }
                    var files = doc.Root.Element("Files").Descendants("File");
                    if (files != null)
                    {
                        extrasInfo.Files = new List<string>();
                        foreach (var f in files)
                            extrasInfo.Files.Add(f.Value);
                    }
                    result.Add(extrasInfo);
                }
                return result;
            }
            return null;
        }

        public static bool IsExtrasInstalled(string cid)
        {
            var list = GetInstalledExtrasInfo();
            return list.Any(x => x.Cid == cid);
        }

        public static void DeleteExtra(ExtrasInfo extra)
        {
            foreach (var f in extra.Files)
            {
                if (File.Exists(f))
                    File.Delete(f);
            }
        }
    }
}
