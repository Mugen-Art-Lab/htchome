using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace NewsWidget
{
    public class LastFeedsData
    {
        public List<Source> sources;

        public static LastFeedsData Read(string path)
        {
            var result = new LastFeedsData();
            if (File.Exists(path))
            {
                var f = new FileInfo(path);
                if (f.Length > 162)
                {
                    using (TextReader textReader = new StreamReader(path))
                    {
                        var deserializer = new XmlSerializer(typeof(LastFeedsData));
                        result = (LastFeedsData)deserializer.Deserialize(textReader);
                    }
                }
                else
                {
                    //App.Log("Settings file is corrupted.");
                }
            }
            return result;
        }

        public void Write(string path)
        {
            using (TextWriter textWriter = new StreamWriter(path))
            {
                var serializer = new XmlSerializer(typeof(LastFeedsData));
                serializer.Serialize(textWriter, this);
            }
        }
    }
}
