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
        public Window Parent;
        public Settings Sett;
        private News _widgetControl;
        public static Widget Instance;
        public LocaleManager LocaleManager;

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
            return "News widget";
        }

        public UserControl Load()
        {
            Sett = Settings.Read(E.ConfigDirectory + "\\NewsWidget.conf");
            LocaleManager = new HTCHome.Core.LocaleManager(E.Path + "\\News\\Localization");
            LocaleManager.LoadLocale(E.Locale);
            _widgetControl = new News();
            _widgetControl.Load();
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
            //((News)_widgetControl).Unload();
            Sett.Write(E.ConfigDirectory + "\\NewsWidget.conf");
        }


        public IntPtr GetRegion()
        {
            return WinAPI.CreateRoundRectRgn(0, 20, (int)(320 * Sett.ScaleFactor), (int)(410 * Sett.ScaleFactor), 5, 5);
        }

        public double GetScalefactor()
        {
            return Sett.ScaleFactor;
        }


        public string GetIcon()
        {
            return E.Path + "\\News\\Resources\\newspaper.png";
        }


        public void SetScalefactor(double scale)
        {
            Sett.ScaleFactor = scale;
            ((News)_widgetControl).Scale.ScaleX = scale;
        }

        public bool GetTopMost()
        {
            return Sett.TopMost;
        }

        public void SetTopMost(bool value)
        {
            Sett.TopMost = value;
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


        public event EventHandler UpdateAero;
    }
}