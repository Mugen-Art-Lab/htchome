using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTCHome.Core;
using System.Windows.Controls;
using System.Windows;
using CalendarWidget.Domain;
using E = HTCHome.Core.Environment;
using L = HTCHome.Core.Logger;

namespace CalendarWidget
{
    public class Widget : IWidget
    {
        public static Window Parent;
        public static Settings Sett;
        public static LocaleManager LocaleManager;
        private UserControl _widgetControl;

        public string GetWidgetName()
        {
            return "Calendar widget";
        }

        public System.Windows.Controls.UserControl Load()
        {
            L.Log("Calendar: Loading settings");
            Sett = Settings.Read(E.ConfigDirectory + "\\CalendarWidget.conf");
            L.Log("Calendar: Settings loaded");
            LocaleManager = new HTCHome.Core.LocaleManager(E.Path + "\\Calendar\\Localization");
            LocaleManager.LoadLocale(E.Locale);
            L.Log("Calendar: Loading saved calendar data");
            DayConverter.Read(E.Path + "\\Calendar\\Calendar.data");
            L.Log("Calendar: Calendar data loaded");
            _widgetControl = new Calendar();
            return _widgetControl;
        }

        public System.Windows.Controls.UserControl GetWidgetControl()
        {
            return _widgetControl;
        }

        public void Unload()
        {
            L.Log("Calendar: Saving settings");
            Sett.Write(E.ConfigDirectory + "\\CalendarWidget.conf");
            L.Log("Calendar: Saving calendar data");
            DayConverter.Write(E.Path + "\\Calendar\\Calendar.data");
        }

        public void SetParent(System.Windows.Window window)
        {
            Parent = window;
        }

        public System.Windows.Point GetWindowPosition()
        {
            return new Point(Sett.left, Sett.top);
        }

        public IntPtr GetRegion()
        {
           return WinAPI.CreateRoundRectRgn(0, 20, (int)(301 * Sett.scaleFactor), (int)(430 * Sett.scaleFactor), 5, 5);
        }

        public void SetWindowPosition(double left, double top)
        {
            Sett.left = left;
            Sett.top = top;
        }

        public string GetIcon()
        {
            return E.Path + "\\Calendar\\Resources\\icon.png";
        }


        public void SetScalefactor(double scale)
        {
            Sett.scaleFactor = scale;
            ((Calendar)_widgetControl).Scale.ScaleX = scale;
        }


        public double GetScalefactor()
        {
            return Sett.scaleFactor;
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
            return  Sett.pinned;
        }

        public void SetPin(bool value)
        {
            Sett.pinned = value;
        }

        public event EventHandler UpdateAeroEvent;
    }
}
