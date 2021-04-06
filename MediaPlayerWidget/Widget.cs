using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using E = HTCHome.Core.Environment;
using HTCHome.Core;
using System.Windows;
using MediaPlayerWidget.Domain;
using System.Windows.Controls;
using System.Xml.Linq;

namespace MediaPlayerWidget
{
    public class Widget : IWidget
    {
        public static Window Parent;
        public static Settings Sett;
        private UserControl _widgetControl;
        public static ResourceManager ResourceManager;
        public static LocaleManager LocaleManager;

        public static Widget Instance;

        public Widget()
        {
            Instance = this;
        }

        public string GetWidgetName()
        {
            return "Music widget";
        }

        public System.Windows.Controls.UserControl Load()
        {
            Sett = Settings.Read(E.ConfigDirectory + "\\MusicWidget.conf");
            //Sett.skin = "Zune-Light";

            LocaleManager = new HTCHome.Core.LocaleManager(E.Path + "\\Music\\Localization");
            LocaleManager.LoadLocale(E.Locale);
            ResourceManager = new HTCHome.Core.ResourceManager(E.Path + "\\Music", Sett.skin);

            _widgetControl = new MediaPlayer();
            return _widgetControl;
        }

        public System.Windows.Controls.UserControl GetWidgetControl()
        {
            return _widgetControl;
        }

        public void Unload()
        {
            Sett.Write(E.ConfigDirectory + "\\MusicWidget.conf");
            //((MediaPlayer)_widgetControl).mediaPlayer.ShutdownPlayerApplication();
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
                return WinAPI.CreateRoundRectRgn((int)(left * Sett.scaleFactor), (int)(top * Sett.scaleFactor), (int)(right * Sett.scaleFactor), (int)(bottom * Sett.scaleFactor), radiusX, radiusY);

            //return WinAPI.CreateRoundRectRgn(0, 30, (int)(295 * Sett.scaleFactor), (int)(400 * Sett.scaleFactor), 15, 15);
        }

        public void SetWindowPosition(double left, double top)
        {
            Sett.left = left;
            Sett.top = top;
        }

        public string GetIcon()
        {
            return E.Path + "\\Music\\Resources\\icon.png";
        }


        public void SetScalefactor(double scale)
        {
            Sett.scaleFactor = scale;
            ((MediaPlayer)_widgetControl).Scale.ScaleX = scale;
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


        public event EventHandler UpdateAeroEvent;

        public void UpdateAero(object sender)
        {
            UpdateAeroEvent(sender, EventArgs.Empty);
        }
    }
}
