using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace HTCHome.Manager
{
    internal sealed class ManagerSettings
    {
        [DataMember(Order = 1)] public string Language { get; set; }
        [DataMember(Order = 2)] public bool AutoStartManager { get; set; }
        [DataMember(Order = 3)] public double Left { get; set; }
        [DataMember(Order = 4)] public double Top { get; set; }
        [DataMember(Order = 5)] public double Width { get; set; }
        [DataMember(Order = 6)] public double Height { get; set; }
        [DataMember(Order = 7)] public bool HasWindowPlacement { get; set; }
    }

    internal sealed class ProfileStore
    {
        private readonly string directory;
        private readonly string managerSettingsPath;

        public ProfileStore(string rootDirectory)
        {
            directory = Path.Combine(rootDirectory, "Profiles");
            Directory.CreateDirectory(directory);
            managerSettingsPath = Path.Combine(directory, "manager.json");
        }

        public List<ProfileRecord> LoadAll()
        {
            var result = new List<ProfileRecord>();
            foreach (string file in Directory.GetFiles(directory, "*.json"))
            {
                if (string.Equals(Path.GetFileName(file), "manager.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(ProfileRecord));
                        var profile = serializer.ReadObject(stream) as ProfileRecord;
                        if (profile != null && !string.IsNullOrWhiteSpace(profile.Id))
                            result.Add(profile);
                    }
                }
                catch
                {
                }
            }
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        public ProfileRecord Create(string name)
        {
            var profile = new ProfileRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name) ? ManagerText.NewInstanceDefault : name.Trim(),
                AutoStart = false
            };
            Save(profile);
            return profile;
        }

        public void Save(ProfileRecord profile)
        {
            string path = GetPath(profile.Id);
            using (var stream = File.Create(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(ProfileRecord));
                serializer.WriteObject(stream, profile);
            }
        }

        public void Delete(ProfileRecord profile)
        {
            string path = GetPath(profile.Id);
            if (File.Exists(path)) File.Delete(path);
        }

        public ManagerSettings LoadManagerSettings()
        {
            if (!File.Exists(managerSettingsPath)) return new ManagerSettings();
            try
            {
                using (var stream = File.OpenRead(managerSettingsPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(ManagerSettings));
                    return serializer.ReadObject(stream) as ManagerSettings ?? new ManagerSettings();
                }
            }
            catch
            {
                return new ManagerSettings();
            }
        }

        public void SaveManagerSettings(ManagerSettings settings)
        {
            using (var stream = File.Create(managerSettingsPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(ManagerSettings));
                serializer.WriteObject(stream, settings);
            }
        }

        private string GetPath(string id)
        {
            return Path.Combine(directory, id + ".json");
        }
    }
}
