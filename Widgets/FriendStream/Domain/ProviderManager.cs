using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Social.Base;

namespace FriendStream.Domain
{
    public class ProviderManager
    {
        public List<SocialProvider> Providers;

        public void FindProviders()
        {
            if (Directory.Exists(Home.Base.E.ExtPath + "\\Social"))
            {
                Providers = new List<SocialProvider>();
                var files = from x in Directory.GetFiles(Home.Base.E.ExtPath + "\\Social")
                            where x.EndsWith(".dll")
                            select x;
                foreach (var f in files)
                {
                    var p = new SocialProvider(f);
                    p.Load();
                    if (p.HasErrors)
                        continue;
                    Providers.Add(p);
                    if (App.Settings.ActiveProviders.Contains(p.Name))
                    {
                        p.IsActive = true;
                    }
                }
            }
        }

        public void ActivateProvider(string name)
        {
            foreach (var socialProvider in Providers)
            {
                if (socialProvider.Name == name)
                {
                    if (socialProvider.HasErrors)
                        return;
                    if (!socialProvider.IsLoaded)
                        socialProvider.Load();
                    socialProvider.IsActive = true;
                    break;
                }
            }
        }

        public void DeactivateProvider(string name)
        {
            foreach (var socialProvider in Providers)
            {
                if (socialProvider.Name == name)
                {
                    if (socialProvider.HasErrors || !socialProvider.IsLoaded)
                        return;
                    socialProvider.IsActive = false;
                    break;
                }
            }
        }
    }
}
