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

namespace ClockWidget
{
    public class Widget : IWidget
    {
        public static Window Parent;
        public static Settings Sett;
        private WidgetControl _widgetControl;
        public static LocaleManager LocaleManager;
        public static ResourceManager ResourceManager;
        public static Widget Instance;

        public Widget()
        {
            Instance = this;
        }

        public string GetWidgetName()
        {
            return "Clock widget";
        }

        public UserControl Load()
        {
            Sett = Settings.Read(E.ConfigDirectory + "\\ClockWidget.conf");

            LocaleManager = new HTCHome.Core.LocaleManager(E.Path + "\\Clock\\Localization");
            LocaleManager.LoadLocale(E.Locale);
            ResourceManager = new HTCHome.Core.ResourceManager(E.Path + "\\Clock", Sett.Skin);

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
            Sett.Write(E.ConfigDirectory + "\\ClockWidget.conf");
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
            string uri = Widget.ResourceManager.GetResourcePath("Skin.xml");
            if (string.IsNullOrEmpty(uri))
                return IntPtr.Zero;
            XDocument doc = XDocument.Load(Widget.ResourceManager.GetResourcePath("Skin.xml"));
            int left = Convert.ToInt32(doc.Descendants("Left").First().Value);
            int top = Convert.ToInt32(doc.Descendants("Top").First().Value);
            int right = Convert.ToInt32(doc.Descendants("Right").First().Value);
            int bottom = Convert.ToInt32(doc.Descendants("Bottom").First().Value);

            if (left == -1 || top == -1 || right == -1 || bottom == -1)
                return IntPtr.Zero;
            else
                return WinAPI.CreateEllipticRgn((int)(left * Sett.ScaleFactor), (int)(top * Sett.ScaleFactor), (int)(right * Sett.ScaleFactor), (int)(bottom * Sett.ScaleFactor));
        }

        public void SetWindowPosition(double left, double top)
        {
            Sett.Left = left;
            Sett.Top = top;
        }

        public string GetIcon()
        {
            return E.Path + "\\Clock\\Resources\\icon.png";
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

        public void UpdateSettings()
        {
            _widgetControl.UpdateSettings();
        }

        public void UpdateAero(object sender)
        {
            UpdateAeroEvent(sender, EventArgs.Empty);
        }

        public event EventHandler UpdateAeroEvent;
    }
}
