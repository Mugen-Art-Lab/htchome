using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Home.Base;
using Home.Packaging;

namespace Home.Updates
{
    public class Updater
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public const string BaseUrl = "http://store.htchome.org/updates/apis/";

        public static Dictionary<string, string> GetInstalledUpdatesList()
        {
            if (!File.Exists(E.Root + "\\updates.xml"))
                CreateDefaultUpdatesXml();
            var xml = XElement.Load(E.Root + "\\updates.xml");

            var result = new Dictionary<string, string>();
            foreach (var u in xml.Descendants("Update"))
            {
                var name = u.Attribute("Name");
                if (name != null)
                    result.Add(u.Attribute("Id").Value, name.Value);
                else
                    result.Add(u.Attribute("Id").Value, "");
            }
            return result;
        }

        //this for options list
        public static List<UpdateInfo> GetInstalledUpdatesInfoList()
        {
            if (!File.Exists(E.Root + "\\updates.xml"))
                CreateDefaultUpdatesXml();
            var xml = XElement.Load(E.Root + "\\updates.xml");

            var result = new List<UpdateInfo>();
            foreach (var u in xml.Descendants("Update"))
            {
                var info = new UpdateInfo();
                info.Id = u.Attribute("Id").Value;
                if (info.Id == "HU3000" || info.Id == "HU3100")
                    continue;
                var name = u.Attribute("Name");
                if (name != null)
                    info.Title = name.Value;
                result.Add(info);
            }
            return result;
        }


        private static void CreateDefaultUpdatesXml()
        {
            var x = new XElement("InstalledUpdates", new XElement("Update", new XAttribute("Id", "HU3000")), new XElement("Update", new XAttribute("Id", "HU3100")));
            x.Save(E.Root + "\\updates.xml");
        }

        public static List<UpdateInfo> GetAvailableUpdatesList(string locale, bool force = false, bool dev = false)
        {
            if (locale != "en-US" && locale != "ru-RU")
                locale = "en-US"; //it support only russian or english description at this time

            var webClient = new WebClient();

            var availableUpdates = new List<UpdateInfo>();

            var updatesXmlContent = "";

            try
            {
                if (!dev)
                    updatesXmlContent = GeneralHelper.GetWebPageContent(BaseUrl + "updates.xml");
                else
                    updatesXmlContent = GeneralHelper.GetWebPageContent(BaseUrl + "updates-dev.xml?" + Environment.TickCount);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            if (string.IsNullOrEmpty(updatesXmlContent))
            {
                logger.Warn("updates.xml is empty");
                webClient.Dispose();
                return null;
            }

            //File.AppendAllText(E.Root + "\\log\\update-content.log", "--------------------------------------" + (char)13 + (char)10);
            //File.AppendAllText(E.Root + "\\log\\update-content.log", updatesXmlContent + (char)13 + (char)10);
            //File.AppendAllText(E.Root + "\\log\\update-content.log", "--------------------------------------" + (char)13 + (char)10);

            XElement updatesXml;
            try
            {
                updatesXml = XElement.Parse(updatesXmlContent);
            }
            catch (Exception ex)
            {
                logger.Fatal("An error occured while downloading updates list. " + (char)10 + (char)13 + ex);
                return null;
            }

            foreach (var el in updatesXml.Descendants("Update"))
            {
                var u = new UpdateInfo();
                var cid = el.Attribute("Cid");
                if (cid != null)
                    u.Cid = cid.Value;
                u.Id = el.Attribute("Id").Value;
                //parse dependencies
                var dep = el.Attribute("Dep");
                if (dep != null)
                {
                    var deps = dep.Value.Split(',');
                    u.Dependencies.AddRange(deps);
                }

                //parse target cultures
                var cul = el.Attribute("Cul");
                if (cul != null)
                {
                    var culs = cul.Value.Split(',');
                    u.CultureList.AddRange(culs);
                }

                u.InfoUrl = BaseUrl + locale + "/" + el.Attribute("Info").Value;

                availableUpdates.Add(u);
            }
            webClient.Dispose();

            if (!force)
            {
                //remove updates that doesn't meet some requirements
                var installedUpdates = GetInstalledUpdatesList();
                var installedExtras = ExtrasManager.GetInstalledExtrasInfo();
                var result = new List<UpdateInfo>();
                foreach (var u in availableUpdates)
                {
                    if (installedUpdates.Keys.Contains(u.Id)) //skip already installed updates
                        continue;
                    if (!RequiredUpdatesInstalled(u, installedUpdates.Keys))
                        continue;
                    if (!u.CultureList.Contains(locale))
                        continue;
                    if (!RequiredComponentInstalled(u.Cid, installedExtras))
                        continue;

                    //get update description
                    var infoXmlContent = GeneralHelper.GetWebPageContent(u.InfoUrl); //webClient.DownloadString(u.InfoUrl);)
                    if (string.IsNullOrEmpty(infoXmlContent))
                    {
                        logger.Warn("Update description is empty");
                        webClient.Dispose();
                        return null;
                    }

                    var infoXml = XElement.Parse(infoXmlContent);
                    u.Title = infoXml.Descendants("Title").FirstOrDefault().Value;
                    u.Description = infoXml.Descendants("Description").FirstOrDefault().Value;
                    u.Package = BaseUrl + infoXml.Descendants("Package").FirstOrDefault().Value;
                    var s = infoXml.Descendants("Size").FirstOrDefault();
                    u.Size = s != null ? s.Value : "0 MB";

                    result.Add(u);
                }
                return result;
            }

            return availableUpdates;
        }

        private static bool RequiredComponentInstalled(string cid, IEnumerable<ExtrasInfo> installedExtras)
        {
            return installedExtras.Any(c => cid == c.Cid);
        }

        private static bool RequiredUpdatesInstalled(UpdateInfo update, ICollection<string> installedUpdates)
        {
            return update.Dependencies.All(installedUpdates.Contains);
        }

        public static void InstallUpdates(List<UpdateInfo> updates)
        {
            var installedUpdates = GetInstalledUpdatesList();

            var webClient = new WebClient();

            foreach (var u in updates)
            {
                if (!string.IsNullOrEmpty(u.Package))
                {
                    //download package and unpack
                    try
                    {
                        var tempFileName = Path.GetTempFileName();
                        if (GeneralHelper.Proxy != null)
                            webClient.Proxy = GeneralHelper.Proxy;
                        webClient.DownloadFile(u.Package, tempFileName);
                        InstallUpdate(tempFileName);
                        if (File.Exists(tempFileName))
                            File.Delete(tempFileName);
                        installedUpdates.Add(u.Id, u.Title);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("An error occured while installing updates.\n" + ex);
                    }
                }
            }

            webClient.Dispose();

            var x = new XElement("UpdatesList", from u in installedUpdates
                                                select new XElement("Update", new XAttribute("Id", u.Key), new XAttribute("Name", u.Value)));
            x.Save(E.Root + "\\updates.xml");

        }

        private static void InstallUpdate(string file)
        {
            var pm = new PackageManager();
            var dir = Path.GetTempPath() + Path.GetFileNameWithoutExtension(file);
            pm.Unpack(file, dir);

            if (!File.Exists(dir + "\\manifest.xml"))
            {
                logger.Error("Update manifest not found in " + dir);
                return;
            }

            var manifest = XElement.Load(dir + "\\manifest.xml");
            foreach (var el in manifest.Descendants("File"))
            {
                var dest = E.Root + "\\" + el.Attribute("Dest").Value;
                var src = dir + "\\" + el.Attribute("Src").Value;
                if (File.Exists(dest))
                    File.Move(dest, dest + ".bak");
                File.Copy(src, dest, true);
            }
        }
    }
}
