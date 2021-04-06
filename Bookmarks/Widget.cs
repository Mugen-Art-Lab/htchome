using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTCHome.Core;
using System.Windows;
using BookmarksWidget.Domain;
using System.Windows.Controls;
using E = HTCHome.Core.Environment;

namespace BookmarksWidget
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
            return "Bookmarks widget";
        }

        public UserControl Load()
        {
            Sett = Settings.Read(E.ConfigDirectory + "\\BookmarksWidget.conf");
            LocaleManager = new HTCHome.Core.LocaleManager(E.Path + "\\Bookmarks\\Localization");
            LocaleManager.LoadLocale(E.Locale);
            _widgetControl = new Bookmarks();
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
            ((Bookmarks)_widgetControl).Unload();
            Sett.Write(E.ConfigDirectory + "\\BookmarksWidget.conf");
        }


        public IntPtr GetRegion()
        {
            /*if (Sett.scaleFactor == 1.0f)
                return WinAPI.CreateRoundRectRgn(105, 26, 400, 195, 10, 10);
            else*/
                return IntPtr.Zero;
        }


        public string GetIcon()
        {
            return E.Path + "\\Bookmarks\\Resources\\icon.png";
        }


        public void SetScalefactor(double scale)
        {
            Sett.scaleFactor = scale;
            ((Bookmarks)_widgetControl).Scale.ScaleX = scale;
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
            return Sett.pinned;
        }

        public void SetPin(bool value)
        {
            Sett.pinned = value;
        }
    }
}
