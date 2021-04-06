using System.Windows;
using System.Windows.Controls;
using HTCHome.Core;
using NewsWidget.Domain;
using E = HTCHome.Core.Environment;
using System;

namespace NewsWidget
{
    public class Widget : IWidget
    {
        public static Window Parent;
        public static Settings Sett;
        private UserControl _widgetControl;

        public static LocaleManager LocaleManager;

        public UserControl GetWidgetControl()
        {
            return _widgetControl;
        }

        public string GetWidgetName()
        {
            return "News widget";
        }

        public UserControl Load()
        {
            Sett = Settings.Read(E.ConfigDirectory + "\\NewsWidget.conf");
            LocaleManager = new HTCHome.Core.LocaleManager(E.Path + "\\News\\Localization");
            LocaleManager.LoadLocale(E.Locale);
            _widgetControl = new News();
            return _widgetControl;
        }

        public void SetParent(Window window)
        {
            Parent = window;
        }

        public Point GetWindowPosition()
        {
            return new Point(Sett.left, Sett.top);
        }

        public void SetWindowPosition(double left, double top)
        {
            Sett.left = left;
            Sett.top = top;
        }

        public void Unload()
        {
            ((News)_widgetControl).Unload();
            Sett.Write(E.ConfigDirectory + "\\NewsWidget.conf");
        }


        public IntPtr GetRegion()
        {
            return WinAPI.CreateRoundRectRgn(0, 20, (int)(320 * Sett.scaleFactor), (int)(410 * Sett.scaleFactor), 5, 5);
        }

        public double GetScalefactor()
        {
            return Sett.scaleFactor;
        }


        public string GetIcon()
        {
            return E.Path + "\\News\\Resources\\newspaper.png";
        }


        public void SetScalefactor(double scale)
        {
            Sett.scaleFactor = scale;
            ((News)_widgetControl).Scale.ScaleX = scale;
        }

        public bool GetTopMost()
        {
            return Sett.topMost;
        }

        public void SetTopMost(bool value)
        {
            Sett.topMost = value;
        }


        public bool GetPin()
        {
            return Sett.pinned;
        }

        public void SetPin(bool value)
        {
            Sett.pinned = value;
        }


        public event EventHandler UpdateAero;
    }
}