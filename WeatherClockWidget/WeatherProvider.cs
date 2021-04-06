using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using WeatherClockWidget.Domain;

namespace WeatherClockWidget
{
    public class WeatherProvider
    {
        private readonly string _path;

        private Assembly _assembly;

        private IWeatherProvider _provider;

        public WeatherProvider(string path)
        {
            _path = path;
            Name = path.Substring(path.LastIndexOf(@"\") + 1, path.Length - path.LastIndexOf(@"\") - 5);
        }

        public string Name { get; private set; }
        public bool IsLoaded { get; set; }

        public static int ToCelsius(int degrees)
        {
            return (int) Math.Round((decimal) ((degrees - 32) * 5 / 9), 1);
        }

        public static int ToFahrenheit(int degrees)
        {
            return (int) Math.Round((decimal) (degrees * 9 / 5 + 32), 1);
        }

        public Coordinates GetCoordinates(string locationCode)
        {
            return _provider.GetCoordinates(locationCode);
        }

        public List<CityLocation> GetLocation(string s)
        {
            return _provider.GetLocation(s);
        }

        public WeatherReport GetWeatherReport(string locale, string locationcode, int degreeType)
        {
            WeatherReport weatherReport = null;
            try
            {
                 weatherReport= _provider.GetWeatherReport(locale, locationcode, degreeType);
            }
            catch (Exception ex)
            {
                HTCHome.Core.Logger.Log(ex.ToString());
            }
            return weatherReport;
        }

        /// <exception cref = "System.TypeLoadException"><c>TypeLoadException</c>.</exception>
        public void Load()
        {
            _assembly = Assembly.LoadFrom(_path);
            Type providerType =
                _assembly.GetTypes().FirstOrDefault(type => typeof (IWeatherProvider).IsAssignableFrom(type));
            if (providerType == null)
            {
                IsLoaded = false;
                throw new TypeLoadException(String.Format("Failed to find IWeatherProvider in {0}", _path));
            }

            _provider = Activator.CreateInstance(providerType) as IWeatherProvider;
            IsLoaded = true;
        }
    }
}