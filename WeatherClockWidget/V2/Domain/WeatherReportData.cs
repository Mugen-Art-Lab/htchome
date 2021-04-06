using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace WeatherClockWidget.V2.Domain
{
    public class WeatherReportData
    {
        public WeatherReportData() { }
        public WeatherReportData(LocationData Location) { this.Location = Location; }
        public LocationData Location = new LocationData() { City = "Moscow", Code = "ASI|RU|RS052|MOSCOW", Height = 192, Lat = 55.75, Lon = 37.61 }; //name of the current location
        public Int32 Temperature = 0; //curent temperature
        public ForecastData Curent = new ForecastData(); //curent forecast
        public List<ForecastData> ForecastList = new List<ForecastData>();

        public static WeatherReportData Read(string path)
        {
            var result = new WeatherReportData();
            if (File.Exists(path))
            {
                var f = new FileInfo(path);
                //note what does this magic number mean?
                //this magic mean that if WeatherClockWidget.weather contains <179 bytes, file is corrupted (contains wrong data)
                if (f.Length > 179) //??? Check!!!
                {
                    using (TextReader textReader = new StreamReader(path))
                    {
                        var deserializer = new XmlSerializer(typeof(WeatherReportData));
                        result = (WeatherReportData)deserializer.Deserialize(textReader);
                    }
                }
            }
            return result;
        }


        public void Write(string path)
        {
            using (TextWriter textWriter = new StreamWriter(path))
            {
                var serializer = new XmlSerializer(typeof(WeatherReportData));
                serializer.Serialize(textWriter, this);
            }
        }
    }
}
