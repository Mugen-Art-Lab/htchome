using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

using HTCHome.Core;

using WeatherClockWidget.Domain;

using E = HTCHome.Core.Environment;
using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace WeatherClockWidget
{
    public class Widget : IWidget
    {
        public static Window Parent;
        public static Settings Sett;
        private UserControl _widgetControl;
        public static LocaleManager LocaleManager;
        public static ResourceManager ResourceManager;
        public static Widget Instance;

        public Widget()
        {
            Instance = this;
        }

        public UserControl GetWidgetControl()
        {
            return _widgetControl;
        }

        public string GetWidgetName()
        {
            return "Weather/Clock widget";
        }

        public UserControl Load()
        {
            Sett = Settings.Read(E.ConfigDirectory + "\\WeatherClockWidget.conf");

            LocaleManager = new HTCHome.Core.LocaleManager(E.Path + "\\WeatherClock\\Localization");
            LocaleManager.LoadLocale(E.Locale);
            ResourceManager = new HTCHome.Core.ResourceManager(E.Path + "\\WeatherClock", Sett.Skin);

            if (!File.Exists(E.ConfigDirectory + "\\WeatherClockWidget.conf"))
            {
                Wizard.WizardWindow wizard = new Wizard.WizardWindow();
                wizard.ShowDialog();
            }

            _widgetControl = new WeatherClock();
            ((WeatherClock)_widgetControl).Load();
            return _widgetControl;
        }

        public void SetParent(Window window)
        {
            Parent = window;
        }

        public Point GetWindowPosition()
        {
            return new Point(Sett.Left, Sett.Top);
        }

        public void SetWindowPosition(double left, double top)
        {
            Sett.Left = left;
            Sett.Top = top;
        }

        public void Unload()
        {
            ((WeatherClock)_widgetControl).Unload();
            Sett.Write(E.ConfigDirectory + "\\WeatherClockWidget.conf");
        }


        public IntPtr GetRegion()
        {

            string uri = Widget.ResourceManager.GetResourcePath("Skin.xml");
            if (string.IsNullOrEmpty(uri))
                return IntPtr.Zero;
            XDocument doc = XDocument.Load(Widget.ResourceManager.GetResourcePath("Skin.xml"));
            int left = Convert.ToInt32(doc.Descendants("Left").First().Value);
            int top = Convert.ToInt32(doc.Descendants("Top").First().Value);
            int right = Convert.ToInt32(doc.Descendants("Right").First().Value);
            int bottom = Convert.ToInt32(doc.Descendants("Bottom").First().Value);
            int radiusX = Convert.ToInt32(doc.Descendants("RadiusX").First().Value);
            int radiusY = Convert.ToInt32(doc.Descendants("RadiusY").First().Value);

            if (left == -1 || top == -1 || right == -1 || bottom == -1)
                return IntPtr.Zero;
            else
                return WinAPI.CreateRoundRectRgn((int)(left * Sett.ScaleFactor), (int)(top * Sett.ScaleFactor), (int)(right * Sett.ScaleFactor), (int)(bottom * Sett.ScaleFactor), radiusX, radiusY);
        }


        public double GetScaleFactor()
        {
            return Sett.ScaleFactor;
        }


        public string GetIcon()
        {
            return E.Path + "\\WeatherClock\\Resources\\icon.png";
        }


        public void SetScalefactor(double scale)
        {
            Sett.ScaleFactor = scale;
            ((WeatherClock)_widgetControl).Scale.ScaleX = scale;
            ((WeatherClock)_widgetControl).Scale.ScaleY = scale;
        }


        public double GetScalefactor()
        {
            return Sett.ScaleFactor;
        }


        public bool GetTopMost()
        {
            return Sett.Topmost;
        }


        public void SetTopMost(bool value)
        {
            Sett.Topmost = value;
        }


        public bool GetPin()
        {
            return Sett.Pinned;
        }

        public void SetPin(bool value)
        {
            Sett.Pinned = value;
        }


        public event EventHandler UpdateAeroEvent;

        public void UpdateAero(object sender)
        {
            UpdateAeroEvent(sender, EventArgs.Empty);
        }

    }
}