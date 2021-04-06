using System.IO;
using System.Xml.Serialization;

namespace WeatherClockWidget.Domain
{
    //note i'm sure this class is useless
    //maybe, but i will better spend time to add something new than to remove this class ;)
    public class Settings
    {
        public int cloudsAnimCount = 3;

        public int cloudsCount = 6;

        public bool debug;

        public int degreeType;

        public bool enableSounds = true;

        public bool enableWallpaperChanging = false;

        public bool enableWeather = true;

        public bool enableWeatherAnimation = true;

        //public int forecastBeginTime;

        public double interval = 90;

        //public int leavesAnimCount = 5;

        //public int leavesCount = 10;

        public string locationcode = "";

        public double opacity = 1;

        public bool pinned = false;

       // public int raindropsAnimCount = 30;

        //public int raindropsCount = 15;

        public double refreshWeatherAnimInterval;

        public double scaleFactor = 1;

        public bool showIconOnTaskbar = true;

        public bool showForecast = true;

        public string skin = "Sense";

        //public int snowflakesAnimCount = 10;

        //public int snowflakesCount = 35;

        public int sunrise = 4;

        public int sunset = 22;

        public int timeMode;

        public double top = -1;

        public double left = -1;

        public bool topmost;

        public string weatherProvider = "MSN";

        public string wallpapersFolder;

        //public bool useFullscreenAnimation = true;

        public static Settings Read(string path)
        {
            var result = new Settings();
            if (File.Exists(path))
            {
                var f = new FileInfo(path);
                if (f.Length > 162)
                {
                    using (TextReader textReader = new StreamReader(path))
                    {
                        var deserializer = new XmlSerializer(typeof(Settings));
                        result = (Settings)deserializer.Deserialize(textReader);
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
                var serializer = new XmlSerializer(typeof(Settings));
                serializer.Serialize(textWriter, this);
            }
        }
    }
}
