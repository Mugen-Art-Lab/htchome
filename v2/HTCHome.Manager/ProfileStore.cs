using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace HTCHome.Manager
{
    internal sealed class ProfileStore
    {
        private readonly string directory;

        public ProfileStore(string rootDirectory)
        {
            directory = Path.Combine(rootDirectory, "Profiles");
            Directory.CreateDirectory(directory);
        }

        public List<ProfileRecord> LoadAll()
        {
            var result = new List<ProfileRecord>();
            foreach (string file in Directory.GetFiles(directory, "*.json"))
            {
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
                    // Ignore malformed profile files for now; manager must stay usable.
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
                Name = string.IsNullOrWhiteSpace(name) ? "Новый экземпляр" : name.Trim()
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

        private string GetPath(string id)
        {
            return Path.Combine(directory, id + ".json");
        }
    }
}
