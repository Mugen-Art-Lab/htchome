using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Social.Base
{
    public class SocialProvider
    {
        private readonly string path;
        private Assembly assembly;
        private ISocialProvider provider;
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public bool IsLoaded { get; private set; }
        public string Name { get; private set; }
        public bool IsActive { get; set; }
        public bool HasErrors { get; set; }
        public bool Signed { get; set; }

        public event EventHandler SignedIn;

        public SocialProvider(string file)
        {
            path = file;
            //Name = path.Substring(path.LastIndexOf(@"\") + 1, path.Length - path.LastIndexOf(@"\") - 5);
        }

        public void Load()
        {
            assembly = Assembly.LoadFrom(path);
            var providerType =
                assembly.GetTypes().FirstOrDefault(type => typeof(ISocialProvider).IsAssignableFrom(type));
            if (providerType == null)
            {
                IsLoaded = false;
                HasErrors = true;
                //throw new TypeLoadException(String.Format("Failed to find ISocialProvider in {0}", path));
                return;
            }

            provider = Activator.CreateInstance(providerType) as ISocialProvider;
            if (provider == null)
                return;
            IsLoaded = true;
            Name = provider.GetName();

            provider.SignedIn += ProviderSignedIn;
        }

        void ProviderSignedIn(object sender, EventArgs e)
        {
            if (SignedIn != null)
                SignedIn(sender, e);
        }

        public void SignIn()
        {
            try
            {
                provider.SignIn();
                Signed = true;
            }
            catch (Exception ex)
            {
                logger.Error("An error occured during signing" + ".\n" + ex);
            }
        }

        public List<FriendStreamEntry> GetFriendStream()
        {
            //try
            //{
                return provider.GetFriendStream();
            //}
            //catch (Exception ex)
            /*{
                logger.Error("An error occured during news feed receiving." + ".\n" + ex);
                return null;
            }*/
        }

        public void Post(string status)
        {
            try
            {
                provider.Post(status);
            }
            catch (Exception ex)
            {
                logger.Error("An error occured during posting." + ".\n" + ex);
            }
        }
    }
}
