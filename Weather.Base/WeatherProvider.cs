using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Weather.Base
{
    public class WeatherProvider
    {
        private readonly string path;
        private Assembly assembly;
        private IWeatherProvider provider;
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public bool IsLoaded { get; private set; }
        public string Name { get; private set; }
        public bool HasErrors { get; private set; }

        public WeatherProvider(string file)
        {
            path = file;
            Name = path.Substring(path.LastIndexOf(@"\") + 1, path.Length - path.LastIndexOf(@"\") - 5);
        }

        public void Load()
        {
            assembly = Assembly.LoadFrom(path);
            Type providerType = null;
            try
            {
                providerType = assembly.GetTypes().FirstOrDefault(type => typeof(IWeatherProvider).IsAssignableFrom(type));
            }
            catch (ReflectionTypeLoadException ex)
            {
                logger.Error("Failed to load provider from " + path + ".\n" + ex);
                HasErrors = true;
                return;
            }

            if (providerType == null)
            {
                logger.Error("Failed to find IWeatherProvider in " + path);
                HasErrors = true;
                return;
            }

            provider = Activator.CreateInstance(providerType) as IWeatherProvider;
            IsLoaded = true;
        }

        //public List<LocationData> GetLocations(string query, CultureInfo culture)
        //{
        //    try
        //    {
        //        return provider.GetLocations(query, culture);
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("Can't find location '" + query +  "'.\n" + ex);
        //        return null;
        //    }
        //}

        public List<LocationData> GetLocations(string query, CultureInfo culture, TemperatureScale tempScale)
        {
            Shared.TemperatureScale = tempScale;
            if (provider == null || HasErrors)
            {
                var result = new List<LocationData>();
                var loc = new LocationData();
                loc.Country = "Error";
                loc.City = "Unsupported provider";
                result.Add(loc);
                return result;
            }

            try
            {
                return provider.GetLocations(query, culture/*, tempScale*/);
            }
            catch (Exception ex)
            {
                logger.Error("Can't find location '" + query + "'.\n" + ex);
                return null;
            }
        }

        public WeatherData GetWeatherReport(CultureInfo culture, LocationData location, TemperatureScale tempScale, WindSpeedScale windSpeedScale, TimeSpan baseUtcOffset)
        {
            if (provider == null || HasErrors)
            {
                var result = new WeatherData();
                result.Curent.Text = "Unsupported provider";
                return result;
            }
            return provider.GetWeatherReport(culture, location, tempScale, windSpeedScale, baseUtcOffset);
        }
    }
}
