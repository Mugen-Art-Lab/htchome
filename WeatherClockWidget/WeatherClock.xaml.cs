using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

using WeatherClockWidget.Domain;

using E = HTCHome.Core.Environment;
using System.IO;
using HTCHome.Core;
using System.Threading;
using WeatherClockWidget.WeatherAnimation;
using System.Windows.Media.Animation;
using System.Xml.Linq;

namespace WeatherClockWidget
{
    /// <summary>
    /// Interaction logic for WeatherClock.xaml
    /// </summary>
    public partial class WeatherClock : UserControl
    {
        private DispatcherTimer timer;
        private DispatcherTimer weatherTimer;
        private bool firstFlip;
        private int lastMinute, lastHour;

        public List<WeatherProvider> providers;
        public WeatherProvider currentProvider;
        public WeatherReport weatherReport;

        private Options options;

        public static bool UseClockAnimation = true;

        public WeatherClock()
        {
            InitializeComponent();

            Initialize();
        }

        private void Initialize()
        {
            if (!Directory.Exists(E.Path + "\\WeatherClock\\Skins\\" + Widget.Sett.skin))
                Widget.Sett.skin = "Sense";
            Bg.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("bg.png")));
            ForecastBg.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("forecast_base_default.png")));

            FrostLeft.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("Weather\\frost_left.png")));
            FrostRight.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("Weather\\frost_right.png")));

            DateTime d = DateTime.Now.AddHours(-1).AddMinutes(-2);
            if (Widget.Sett.timeMode == 1)
            {
                int h = Convert.ToInt32(d.ToString("hh"));
                Hours.Initialize(h);
            }
            else
                Hours.Initialize(d.Hour);

            Minutes.Initialize(d.Minute);

            Skin.Source = new Uri(E.Path + "\\WeatherClock\\Skins\\" + Widget.Sett.skin + "\\Layout.xaml");
        }

        public void ReloadSkin()
        {
            Skin.Source = new Uri(E.Path + "\\WeatherClock\\Skins\\" + Widget.Sett.skin + "\\Layout.xaml");
            Widget.ResourceManager = new ResourceManager(E.Path + "\\WeatherClock", Widget.Sett.skin);

            Bg.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("base_default.png")));
            ForecastBg.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("forecast_base_default.png")));
            FrostLeft.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("Weather\\frost_left.png")));
            FrostRight.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("Weather\\frost_right.png")));

            if (Widget.Sett.timeMode == 1)
            {
                int h = Convert.ToInt32(DateTime.Now.ToString("hh"));
                Hours.Initialize(h);
            }
            else
                Hours.Initialize(DateTime.Now.Hour);
            Minutes.Initialize(DateTime.Now.Minute);

            Widget.Instance.UpdateAero(this);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            weatherReport = WeatherReport.Read(E.Path + "\\WeatherClock\\Weather.data");

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += new EventHandler(timer_Tick);

            weatherTimer = new DispatcherTimer();
            weatherTimer.Interval = TimeSpan.FromMinutes(Widget.Sett.interval);
            weatherTimer.Tick += weatherTimer_Tick;

            Minutes.HalfFlip += Minutes_HalfFlip;
            Hours.HalfFlip += Hours_HalfFlip;

            Date.Text = DateTime.Now.ToString((string)Skin["MainDateFormat"]);

            City.Text = weatherReport.Location;
            Weather.Text = weatherReport.NowSky;
            Temperature.Text = weatherReport.NowTemp.ToString() + "°";

            if (Widget.Sett.showIconOnTaskbar)
                Widget.Parent.ShowInTaskbar = true;

            /*Image icon = new Image();
            icon.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\weather_{0}.png", weatherReport.NowSkyCode))));
            icon.MouseLeftButtonDown += icon_MouseLeftButtonDown;
            WeatherIconGrid.Children.Add(icon);*/
            WeatherIcon.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\weather_{0}.png", weatherReport.NowSkyCode))));
            if (Widget.Sett.showIconOnTaskbar)
            {
                if (!string.IsNullOrEmpty(weatherReport.NowSky))
                    Widget.Parent.Title = weatherReport.NowSky;
                Widget.Parent.Icon = WeatherIcon.Source;
            }

            for (int i = 0; i < ForecastPanel.Children.Count; i++)
            {
                ForecastItem item = (ForecastItem)ForecastPanel.Children[i];
                item.Initialize();
                item.Day.Text = DateTime.Today.AddDays(i).ToString((string)Skin["ForecastDateFormat"]);
                if (weatherReport.Forecast != null && weatherReport.Forecast.Count == 5)
                {
                    item.Icon.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\weather_{0}.png", weatherReport.Forecast[i].SkyCode))));
                    item.TemperatureH.Text = weatherReport.Forecast[i].HighTemperature.ToString() + "°";
                    item.TemperatureL.Text = "/" + weatherReport.Forecast[i].LowTemperature.ToString() + "°";
                    item.Url = weatherReport.Forecast[i].Url;
                }
            }

            FItem1.Day.Text = Widget.LocaleManager.GetString("Today");
            FItem2.Day.Text = Widget.LocaleManager.GetString("Tomorrow");

            

            if (Widget.Sett.showIconOnTaskbar && Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.IsPlatformSupported)
            {
                System.Drawing.Icon oicon = DrawIcon(weatherReport.NowTemp); //System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.Instance.SetOverlayIcon(Widget.Parent, oicon, "test");
                //Widget.Parent.Icon = MakeIcon(15, 2);
            }

            ForecastGrid.Visibility = Widget.Sett.showForecast ? Visibility.Visible : Visibility.Collapsed;

            GetWeatherProviders();

            MenuItem optionsItem = new MenuItem();
            optionsItem.Header = Widget.LocaleManager.GetString("Options");
            optionsItem.Click += optionsItem_Click;

            MenuItem refreshItem = new MenuItem();
            refreshItem.Header = Widget.LocaleManager.GetString("Refresh");
            refreshItem.Click += new RoutedEventHandler(refreshItem_Click);


            Widget.Parent.ContextMenu.Items.Insert(0, new Separator());
            Widget.Parent.ContextMenu.Items.Insert(0, optionsItem);
            Widget.Parent.ContextMenu.Items.Insert(0, refreshItem);

            if (Widget.Sett.debug)
            {
                MenuItem DemoItem = new MenuItem();
                DemoItem.Header = "Demo";

                MenuItem rainDemo = new MenuItem();
                rainDemo.Header = "Rain";
                rainDemo.Click += new RoutedEventHandler(rainDemo_Click);

                MenuItem snowDemo = new MenuItem();
                snowDemo.Header = "Snow";
                snowDemo.Click += new RoutedEventHandler(snowDemo_Click);

                MenuItem cloudsDemo = new MenuItem();
                cloudsDemo.Header = "Clouds";
                cloudsDemo.Click += new RoutedEventHandler(cloudsDemo_Click);

                MenuItem lightningDemo = new MenuItem();
                lightningDemo.Header = "Lightning";
                lightningDemo.Click += new RoutedEventHandler(lightningDemo_Click);

                MenuItem frostDemo = new MenuItem();
                frostDemo.Header = "Cold";
                frostDemo.Click += new RoutedEventHandler(frostDemo_Click);

                DemoItem.Items.Add(rainDemo);
                DemoItem.Items.Add(snowDemo);
                DemoItem.Items.Add(cloudsDemo);
                DemoItem.Items.Add(lightningDemo);
                DemoItem.Items.Add(frostDemo);

                Widget.Parent.ContextMenu.Items.Insert(0, DemoItem);
            }

            if (Widget.Sett.timeMode == 1)
                Hours.ShowAmPm = true;
            else
                Hours.ShowAmPm = false;

            Scale.ScaleX = Widget.Sett.scaleFactor;

            options = new Options(this);

            XDocument doc = XDocument.Load(E.Path + "\\WeatherClock\\Skins\\" + Widget.Sett.skin + "\\Skin.xml");
            WeatherClock.UseClockAnimation = Convert.ToBoolean(doc.Root.Element("UseClockAnimation").Value);

            if (UseClockAnimation)
            {
                FirstFlip();
            }
            else
            {
                lastMinute = 0;
                Minutes.Flip(DateTime.Now.Minute, Widget.Sett.timeMode, UseClockAnimation);
                timer.Start();
                weatherTimer.Start();
                weatherTimer_Tick(null, EventArgs.Empty);
            }
        }

        private System.Drawing.Icon MakeIcon(int degrees, int skycode)
        {

            System.Drawing.Icon oIcon = null;
            try
            {
                System.Drawing.Bitmap bm = new System.Drawing.Bitmap(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\weather_{0}.png", skycode)));
                System.Drawing.Graphics g = System.Drawing.Graphics.FromImage((System.Drawing.Image)bm);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                System.Drawing.Font oFont = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
                if (degrees < 10 && degrees > -10)
                {
                    g.DrawString(degrees.ToString(), oFont, new System.Drawing.SolidBrush(System.Drawing.Color.Black), 3, 2);
                }
                else
                {
                    g.DrawString(degrees.ToString(), oFont, new System.Drawing.SolidBrush(System.Drawing.Color.Black), 1, 2);
                }
                oIcon = System.Drawing.Icon.FromHandle(bm.GetHicon());
                oFont.Dispose();
                g.Dispose();
                bm.Dispose();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.InnerException.ToString());
            }

            return oIcon;

        }

        private System.Drawing.Icon DrawIcon(int degrees)
        {

            System.Drawing.Icon oIcon = null;
            try
            {
                System.Drawing.Bitmap bm = new System.Drawing.Bitmap(Widget.ResourceManager.GetResourcePath("Weather\\overlay_icon.png"));
                System.Drawing.Graphics g = System.Drawing.Graphics.FromImage((System.Drawing.Image)bm);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                System.Drawing.Font oFont = new System.Drawing.Font("Arial", 18, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
                switch (degrees.ToString().Length)
                {
                    case (1):
                        g.DrawString(degrees.ToString(), oFont, new System.Drawing.SolidBrush(System.Drawing.Color.Black), 8, 0);
                        break;
                    case (2):
                        g.DrawString(degrees.ToString(), oFont, new System.Drawing.SolidBrush(System.Drawing.Color.Black), 3, 0);
                        break;
                    case (3):
                        g.DrawString(degrees.ToString(), oFont, new System.Drawing.SolidBrush(System.Drawing.Color.Black), -2, 0);
                        break;
                }
                /*if (degrees < 10 && degrees > -10)
                {
                    g.DrawString(degrees.ToString(), oFont, new System.Drawing.SolidBrush(System.Drawing.Color.Black), 0, 0);
                }
                else
                {
                    g.DrawString(degrees.ToString(), oFont, new System.Drawing.SolidBrush(System.Drawing.Color.Black), 3, 0);
                }*/
                oIcon = System.Drawing.Icon.FromHandle(bm.GetHicon());
                oFont.Dispose();
                g.Dispose();
                bm.Dispose();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }

            return oIcon;

        }

        void frostDemo_Click(object sender, RoutedEventArgs e)
        {
            WeatherAnimationCanvas.Children.Clear();
            StartFrostAnimation();
        }

        void lightningDemo_Click(object sender, RoutedEventArgs e)
        {
            WeatherAnimationCanvas.Children.Clear();
            StartLightningAnimation();
            StartRainAnimation();
        }

        void cloudsDemo_Click(object sender, RoutedEventArgs e)
        {
            WeatherAnimationCanvas.Children.Clear();
            StartCloudAnimation();
        }

        void snowDemo_Click(object sender, RoutedEventArgs e)
        {
            WeatherAnimationCanvas.Children.Clear();
            StartSnowAnimation();
        }

        void rainDemo_Click(object sender, RoutedEventArgs e)
        {
            WeatherAnimationCanvas.Children.Clear();
            StartRainAnimation();
        }

        void refreshItem_Click(object sender, RoutedEventArgs e)
        {
            weatherTimer_Tick(null, EventArgs.Empty);
        }

        private void GetWeatherProviders()
        {
            if (Directory.Exists(E.Path + "\\WeatherClock\\WeatherProviders"))
            {
                providers = new List<WeatherProvider>();
                var files = from x in Directory.GetFiles(E.Path + "\\WeatherClock\\WeatherProviders")
                            where x.EndsWith(".dll")
                            select x;
                foreach (var f in files)
                {
                    var p = new WeatherProvider(f);
                    providers.Add(p);
                    if (Widget.Sett.weatherProvider == p.Name)
                    {
                        currentProvider = p;
                        p.Load();
                    }
                }
            }
        }

        public void UpdateSettings()
        {
            Scale.ScaleX = Widget.Sett.scaleFactor;
            Scale.ScaleY = Scale.ScaleX;

            if (Convert.ToBoolean(Widget.Sett.timeMode) != Hours.ShowAmPm)
                FirstFlip();
            ForecastGrid.Visibility = Widget.Sett.showForecast ? Visibility.Visible : Visibility.Collapsed;
            if (E.Locale != Widget.LocaleManager.LocaleCode)
                Widget.LocaleManager.LoadLocale(E.Locale);

            if (Skin.Source.AbsolutePath != Widget.Sett.skin)
            {
                ReloadSkin();
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (!firstFlip)
            {
                if (DateTime.Now.Minute != lastMinute)
                {
                    Minutes.Flip(DateTime.Now.Minute, UseClockAnimation);
                }

                lastMinute = DateTime.Now.Minute;
                Date.Text = DateTime.Now.ToString((string)Skin["MainDateFormat"]);
            }

            if (DateTime.Now.Hour != lastHour)
            {
                for (int i = 0; i < ForecastPanel.Children.Count; i++)
                {
                    ForecastItem item = (ForecastItem)ForecastPanel.Children[i];
                    item.Initialize();
                    item.Day.Text = DateTime.Today.AddDays(i).ToString((string)Skin["ForecastDateFormat"]);
                    if (weatherReport.Forecast != null && weatherReport.Forecast.Count == 5)
                    {
                        item.Icon.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\weather_{0}.png", weatherReport.Forecast[i].SkyCode))));
                        item.TemperatureH.Text = weatherReport.Forecast[i].HighTemperature.ToString() + "°";
                        item.TemperatureL.Text = "/" + weatherReport.Forecast[i].LowTemperature.ToString() + "°";
                        item.Url = weatherReport.Forecast[i].Url;
                    }
                }

                FItem1.Day.Text = Widget.LocaleManager.GetString("Today");
                FItem2.Day.Text = Widget.LocaleManager.GetString("Tomorrow");
            }
        }

        private void FirstFlip()
        {
            firstFlip = true;
            ((Storyboard)Minutes.Resources["FlipAnim"]).BeginTime = TimeSpan.FromMilliseconds(1000);
            Minutes.Flip(DateTime.Now.AddMinutes(-1).Minute, UseClockAnimation);
            ((Storyboard)Minutes.Resources["FlipAnim"]).BeginTime = TimeSpan.Zero;
        }

        private void Minutes_HalfFlip()
        {
            if (DateTime.Now.Hour != lastHour || firstFlip)
            {
                if (Widget.Sett.timeMode == 1)
                {
                    int m;
                    if (DateTime.Now.Hour >= 12 && DateTime.Now.Hour <= 23)
                        m = 1;
                    else
                        m = 0;
                    int h = Convert.ToInt32(DateTime.Now.ToString("hh"));
                    if (h == 12 && m == 0)
                        h = 12;
                    Hours.Flip(h, m, UseClockAnimation);
                }
                else
                    Hours.Flip(DateTime.Now.Hour, UseClockAnimation);
            }
        }

        private void Hours_HalfFlip()
        {
            if (firstFlip)
            {
                Minutes.Flip(DateTime.Now.Minute, UseClockAnimation);
                firstFlip = false;

                timer.Start();
                weatherTimer.Start();
                weatherTimer_Tick(null, EventArgs.Empty);
            }

            lastHour = DateTime.Now.Hour;
            lastMinute = DateTime.Now.Minute;

            if (Convert.ToBoolean(Widget.Sett.timeMode) != Hours.ShowAmPm)
                Hours.ShowAmPm = Convert.ToBoolean(Widget.Sett.timeMode);
        }

        public void weatherTimer_Tick(object sender, EventArgs e)
        {
            ThreadStart threadStarter = RefreshWeather;
            var thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public void RefreshWeather()
        {
            try
            {
                GetWeatherReport();

                if (weatherReport != null)
                    WeatherPanel.Dispatcher.Invoke((Action)UpdateWeatherData, null);
                ForecastPanel.Dispatcher.Invoke((Action)UpdateForecastData, null);
            }
            catch (Exception ex)
            {
                WeatherPanel.Dispatcher.Invoke((Action)RefreshWeatherFail, null);
                HTCHome.Core.Logger.Log(ex.ToString());
            }
        }

        private void GetWeatherReport()
        {
            WeatherReport temp = new WeatherReport();
            if (Widget.Sett.locationcode != string.Empty)
            {
                temp = currentProvider.GetWeatherReport(E.Locale, Widget.Sett.locationcode, Widget.Sett.degreeType);
            }
            else
            {
                temp = currentProvider.GetWeatherReport(E.Locale, string.Empty, Widget.Sett.degreeType);
            }

            if (temp != null)
                weatherReport = temp;

        }

        private void UpdateWeatherData()
        {
            if (!string.IsNullOrEmpty(weatherReport.Location))
            {
                if (weatherReport.Location.Contains(","))
                    weatherReport.Location = weatherReport.Location.Substring(0, weatherReport.Location.IndexOf(","));

                City.Text = weatherReport.Location;

                FlipWeatherIcon();

                if (Widget.Sett.showIconOnTaskbar && Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.IsPlatformSupported)
                {
                    System.Drawing.Icon oicon = DrawIcon(weatherReport.NowTemp);
                    Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.Instance.SetOverlayIcon(Widget.Parent, oicon, "");
                }

                SetWeatherState(weatherReport.NowSkyCode);
            }

            Temperature.Text = weatherReport.NowTemp + "°";

            if (!string.IsNullOrEmpty(weatherReport.NowSky))
                Weather.Text = weatherReport.NowSky;
        }

        private void UpdateForecastData()
        {
            for (int i = 0; i < ForecastPanel.Children.Count; i++)
            {
                ForecastItem item = (ForecastItem)ForecastPanel.Children[i];
                if (weatherReport.Forecast != null && weatherReport.Forecast.Count == 5)
                {
                    item.FlipWeather(weatherReport.Forecast[i].SkyCode);
                    item.TemperatureH.Text = weatherReport.Forecast[i].HighTemperature.ToString() + "°";
                    item.TemperatureL.Text = "/" + weatherReport.Forecast[i].LowTemperature.ToString() + "°";
                    item.Url = weatherReport.Forecast[i].Url;
                    item.ToolTip = weatherReport.Forecast[i].Text;
                }
            }
        }

        private void RefreshWeatherFail()
        {

        }

        private void FlipWeatherIcon()
        {
            WeatherIconBg.Source = WeatherIcon.Source;
            WeatherIconBg.Opacity = 1;
            WeatherIcon.Opacity = 0;
            WeatherIcon.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\weather_{0}.png", weatherReport.NowSkyCode))));
            ((Storyboard)WeatherIconGrid.Resources["Flip"]).Begin();
            if (Widget.Sett.showIconOnTaskbar)
            {
                if (!string.IsNullOrEmpty(weatherReport.NowSky))
                    Widget.Parent.Title = weatherReport.NowSky;
                Widget.Parent.Icon = WeatherIcon.Source;
            }
        }

        public void icon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1 && !string.IsNullOrEmpty(weatherReport.Url))
                WinAPI.ShellExecute(IntPtr.Zero, "open", weatherReport.Url, "", "", 0);
        }

        void optionsItem_Click(object sender, RoutedEventArgs e)
        {
            if (options.IsVisible)
            {
                options.Activate();
                return;
            }
            options = new Options(this);

            if (E.Locale == "he-IL" || E.Locale == "ar-SA")
            {
                options.FlowDirection = FlowDirection.RightToLeft;
            }
            else
            {
                options.FlowDirection = FlowDirection.LeftToRight;
            }

            options.ShowDialog();
        }

        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift))
            {
                /*IntPtr handle = WinAPI.GetDesktopWindow();
                WinAPI.SystemParametersInfo(WinAPI.SPI_SETDESKWALLPAPER, 0, string.Empty, WinAPI.SPIF_UPDATEINIFILE);*/
                WeatherAnimationCanvas.Children.Clear();
                //StartLightningAnimation();
                //StartRainAnimation();

                //StartCloudAnimation();
                //StartFrostAnimation();
                //StartSnowAnimation();
            }


            /*if (WeatherIconGrid.Children.Count > 0)
            {
                ((WeatherIcons.IWeatherIcon)WeatherIconGrid.Children[0]).Unload();
            }

            UserControl a = (UserControl)Application.LoadComponent(new Uri("/WeatherClock;component/WeatherIcons/Weather02.xaml", UriKind.Relative));
            WeatherIconGrid.Children.Add(a);*/
        }

        private void SetWeatherState(int weather)
        {
            WeatherAnimationCanvas.Children.Clear();

            switch (weather)
            {
                case 38:
                    if (Widget.Sett.enableWeather && Widget.Sett.enableWeatherAnimation)
                    {
                        StartCloudAnimation();
                    }
                    break;
                case 6:
                case 8:
                case 3:
                case 7:
                    if (Widget.Sett.enableWeather && Widget.Sett.enableWeatherAnimation)
                    {
                        StartCloudAnimation();
                    }
                    break;
                case 11:
                    //StartFogAnimation();
                    break;
                case 12:
                case 13:
                case 14:
                    if (Widget.Sett.enableWeather && Widget.Sett.enableWeatherAnimation)
                    {
                        StartRainAnimation();
                    }
                    break;

                case 19:
                case 20:
                case 21:
                case 22:
                case 23:
                case 24:
                case 25:
                    if (Widget.Sett.enableWeather && Widget.Sett.enableWeatherAnimation)
                    {
                        StartSnowAnimation();
                    }
                    break;

                case 32:
                    if (Widget.Sett.enableWeather && Widget.Sett.enableWeatherAnimation)
                    {
                        //StartWindAnimation();
                    }

                    break;
                case 18:
                    if (Widget.Sett.enableWeather && Widget.Sett.enableWeatherAnimation)
                    {
                        StartRainAnimation();
                    }
                    break;

                case 15:
                case 16:
                case 17:
                    if (Widget.Sett.enableWeather && Widget.Sett.enableWeatherAnimation)
                    {
                        StartLightningAnimation();
                        StartRainAnimation();
                    }
                    break;
            }

            if (weatherReport.NowTemp < -10)
                StartFrostAnimation();
        }

        private void StartCloudAnimation()
        {
            for (int i = 0; i < 5; i++)
            {
                Cloud c = new Cloud();
                c.Initialize(i);
                WeatherAnimationCanvas.Children.Add(c);
            }
        }

        private void StartRainAnimation()
        {
            for (int i = 0; i < 30; i++)
            {
                Raindrop r = new Raindrop();
                r.Initialize(i, 2500 + i * 100);
                WeatherAnimationCanvas.Children.Add(r);
            }

            for (int i = 0; i < 4; i++)
            {
                Raindrop2 r2 = new Raindrop2();
                r2.Initialize(i);
                WeatherAnimationCanvas.Children.Add(r2);
            }

            /*if (Widget.Sett.useFullscreenAnimation)
            {
                for (int i = 0; i < 10; i++)
                {
                    FullscreenAnimation.Raindrop r = new FullscreenAnimation.Raindrop();
                    r.Initialize(i);
                    r.Show();
                }
            }*/

            if (Widget.Sett.top < 10)
            {
                RainWiper wiper = new RainWiper();
                wiper.Initialize();
                WeatherAnimationCanvas.Children.Add(wiper);
            }

            RainCloud c1 = new RainCloud();
            c1.Initialize(0);
            RainCloud c2 = new RainCloud();
            c2.Initialize(1);
            WeatherAnimationCanvas.Children.Add(c1);
            WeatherAnimationCanvas.Children.Add(c2);

            if (Widget.Sett.enableSounds)
                c1.PlayRainSound();
        }

        private void StartLightningAnimation()
        {
            Lightning l1 = new Lightning();
            l1.Initialize(1, 0, ref LightningBg1);
            Lightning l2 = new Lightning();
            l2.Initialize(2, 1, ref LightningBg2);
            WeatherAnimationCanvas.Children.Add(l1);
            WeatherAnimationCanvas.Children.Add(l2);

            if (Widget.Sett.enableSounds)
            {
                l1.PlayLightningSound();
                l2.PlayLightningSound();
            }
        }

        private void StartSnowAnimation()
        {

            for (int i = 0; i < 30; i++)
            {
                Snowflake s = new Snowflake();
                s.Initialize(i, 2500 + i * 100);
                WeatherAnimationCanvas.Children.Add(s);
            }

            RainCloud c1 = new RainCloud();
            c1.Initialize(0);
            RainCloud c2 = new RainCloud();
            c2.Initialize(1);
            WeatherAnimationCanvas.Children.Add(c1);
            WeatherAnimationCanvas.Children.Add(c2);

            if (Widget.Sett.enableSounds)
                c1.PlaySnowSound();
        }

        private void StartFrostAnimation()
        {
            Icicle i1 = new Icicle();
            i1.Initialize(3);

            i1.Style = (Style)Skin["Icicle1Style"];

            Icicle i2 = new Icicle();
            i2.Initialize(2);
            i2.Style = (Style)Skin["Icicle2Style"];

            Icicle i3 = new Icicle();
            i3.Initialize(1);
            i3.Style = (Style)Skin["Icicle3Style"];

            WeatherAnimationCanvas.Children.Add(i1);
            WeatherAnimationCanvas.Children.Add(i2);
            WeatherAnimationCanvas.Children.Add(i3);

            Storyboard s = (Storyboard)FrostBg.Resources["FadeIn"];
            s.Begin();

            if (Widget.Sett.enableSounds)
            {
                MediaPlayer player = new MediaPlayer();
                player.Open(new Uri(Widget.ResourceManager.GetResourcePath("Sounds\\Cold.wav")));
                player.Play();
            }
        }

        public void Unload()
        {
            if (weatherReport != null)
            {
                weatherReport.Write(E.Path + "\\WeatherClock\\Weather.data");
            }
        }

        private void FrostFadeInAnimation_Completed(object sender, EventArgs e)
        {
            Storyboard s = (Storyboard)FrostBg.Resources["FadeOut"];
            s.Begin();
        }
    }
}
