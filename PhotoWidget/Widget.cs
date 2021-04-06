using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using HTCHome.Core;
using E = HTCHome.Core.Environment;

namespace PhotoWidget
{
    public class Widget : IWidget
    {
        public Window Parent;
        public Settings Sett;
        private WidgetControl _widgetControl;
        public LocaleManager LocaleManager;
        public ResourceManager ResourceManager;
        public static Widget Instance;

        public Widget()
        {
            Instance = this;
        }

        public string GetWidgetName()
        {
            return "Photo widget";
        }

        public UserControl Load()
        {
            Sett = Settings.Read(E.ConfigDirectory + "\\PhotoWidget.conf");

            LocaleManager = new HTCHome.Core.LocaleManager(E.Path + "\\Photo\\Localization");
            LocaleManager.LoadLocale(E.Locale);

            _widgetControl = new WidgetControl();
            _widgetControl.Load();

            return _widgetControl;
        }

        public UserControl GetWidgetControl()
        {
            return _widgetControl;
        }

        public void Unload()
        {
            //_widgetControl.Unload();
            Sett.Write(E.ConfigDirectory + "\\PhotoWidget.conf");
        }

        public void SetParent(Window window)
        {
            Parent = window;
        }

        public Point GetWindowPosition()
        {
            return new Point(Sett.Left, Sett.Top);
        }

        public IntPtr GetRegion()
        {
            return IntPtr.Zero;
        }

        public void SetWindowPosition(double left, double top)
        {
            Sett.Left = left;
            Sett.Top = top;
        }

        public string GetIcon()
        {
            return E.Path + "\\Photo\\Resources\\icon.png";
        }

        public double GetScalefactor()
        {
            return Sett.ScaleFactor;
        }

        public void SetScalefactor(double scale)
        {
            Sett.ScaleFactor = scale;
            _widgetControl.Scale.ScaleX = scale;
            _widgetControl.Scale.ScaleY = scale;
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

        public void UpdateSettings(bool rescan)
        {
            _widgetControl.UpdateSettings(rescan);
        }

        public void UpdateAero(object sender)
        {
            UpdateAeroEvent(sender, EventArgs.Empty);
        }

        public event EventHandler UpdateAeroEvent;
    }
}
