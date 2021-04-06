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
using HTCHome.Core;
using System.Windows.Interop;
using System.Threading;
using WeatherClockWidget.Domain;
using E = HTCHome.Core.Environment;
using System.IO;
using System.Xml.Linq;

namespace WeatherClockWidget
{
    /// <summary>
    /// Interaction logic for options.xaml
    /// </summary>
    public partial class Options : Window
    {
        private IntPtr handle;
        private WeatherClock widget;

        private List<CityLocation> results = new List<CityLocation>();

        private List<string> skins = new List<string>();

        int _selectedIndex = -1;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value == _selectedIndex)
                    return;

                if (value > -1)
                {
                    ((LocationItem)SearchResults.Children[value]).Selected = true;
                    Widget.Sett.Locationcode = results[value].Code;
                    widget.weatherTimer_Tick(null, EventArgs.Empty);

                }
                else
                {
                    //
                }

                if (_selectedIndex > -1 && _selectedIndex < SearchResults.Children.Count)
                    ((LocationItem)SearchResults.Children[_selectedIndex]).Selected = false;
                _selectedIndex = value;
            }
        }


        public Options(WeatherClock w)
        {
            InitializeComponent();

            widget = w;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            handle = new WindowInteropHelper(this).Handle;

            WinAPI.MARGINS margins = new WinAPI.MARGINS();
            margins.cyTopHeight = 24;

            HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

            WinAPI.ExtendGlassFrame(handle, ref margins);

            GeneralTab.Header = Widget.LocaleManager.GetString("General");
            LocationTab.Header = Widget.LocaleManager.GetString("Location");
            SkinTab.Header = Widget.LocaleManager.GetString("Skin");
            AboutTab.Header = Widget.LocaleManager.GetString("AboutWidget");

            TaskbarIconCheckBox.Content = Widget.LocaleManager.GetString("ShowTaskbarIcon");
            WeatherCheckBox.Content = Widget.LocaleManager.GetString("ShowWeather");
            WeatherAnimationCheckBox.Content = Widget.LocaleManager.GetString("EnableWeatherAnimation");
            ShowForecastCheckBox.Content = Widget.LocaleManager.GetString("ShowForecast");
            EnableSoundsCheckBox.Content = Widget.LocaleManager.GetString("EnableSounds");
            ProviderTextBlock.Text = Widget.LocaleManager.GetString("WeatherProvider");
            IntervalTextBlock.Text = Widget.LocaleManager.GetString("Interval");
            ShowTempInTextBlock.Text = Widget.LocaleManager.GetString("ShowTempIn");
            Fahrenheit.Content = Widget.LocaleManager.GetString("Fahrenheit");
            Celsius.Content = Widget.LocaleManager.GetString("Celsius");
            TimeModeTextBlock.Text = Widget.LocaleManager.GetString("TimeMode");
            TimeMode1.Content = Widget.LocaleManager.GetString("TimeMode1");
            TimeMode2.Content = Widget.LocaleManager.GetString("TimeMode2");
            WallpaperChangingCheckBox.Content = Widget.LocaleManager.GetString("EnableWallpaperChanging");
            WallpapersFolderTextBlock.Text = Widget.LocaleManager.GetString("WallpapersFolder");
            RestartTextBlock.Text = Widget.LocaleManager.GetString("RestartText");

            OkButton.Content = Widget.LocaleManager.GetString("OK");
            CancelButton.Content = Widget.LocaleManager.GetString("Cancel");
            ApplyButton.Content = Widget.LocaleManager.GetString("Apply");

            CurrentLocationTextBlock.Text = Widget.LocaleManager.GetString("CurrentLocation");
            EnterLocationTextBlock.Text = Widget.LocaleManager.GetString("TypeLocationText");
            DetectLocationCheckBox.Content = Widget.LocaleManager.GetString("DetectLocation");

            TaskbarIconCheckBox.IsChecked = Widget.Sett.ShowIconOnTaskbar;
            WeatherCheckBox.IsChecked = Widget.Sett.EnableWeather;
            WeatherAnimationCheckBox.IsChecked = Widget.Sett.EnableWeatherAnimation;
            ShowForecastCheckBox.IsChecked = Widget.Sett.ShowForecast;
            //IntervalTextBox.Text = Widget.Sett.Interval.ToString();
            IntervalComboBox.Text = Widget.Sett.Interval.ToString();
            EnableSoundsCheckBox.IsChecked = Widget.Sett.EnableSounds;
            WallpaperChangingCheckBox.IsChecked = Widget.Sett.EnableWallpaperChanging;

            LocationText.Text = widget.City.Text;

            Image1.Source = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath("icon.png")));

            System.Reflection.Assembly _AsmObj = System.Reflection.Assembly.GetExecutingAssembly();
            System.Reflection.AssemblyName _CurrAsmName = _AsmObj.GetName();
            string _Major = _CurrAsmName.Version.Major.ToString();
            string _Minor = _CurrAsmName.Version.Minor.ToString();
            string _Build = _CurrAsmName.Version.Build.ToString();

            VersionString.Text = string.Format("Version {0}.{1} Build {2}", _Major, _Minor, _Build);

            //SkinPreview.Source = new BitmapImage(new Uri(E.Path + "\\WeatherClock\\Skins\\" + Widget.Sett.skin + "\\Preview.png"));

            var dirs = from x in Directory.GetDirectories(E.Path + "\\WeatherClock\\Skins")
                       where File.Exists(x + "\\Skin.xml")
                       select x;

            foreach (var d in dirs)
            {
                XDocument doc = XDocument.Load(d + "\\Skin.xml");

                ComboBoxItem item = new ComboBoxItem();
                item.Content = doc.Root.Element("Name").Value;
                SkinsComboBox.Items.Add(item);
                skins.Add(new DirectoryInfo(d).Name);
            }

            XDocument skin = XDocument.Load(E.Path + "\\WeatherClock\\Skins\\" + Widget.Sett.Skin + "\\Skin.xml");

            SkinsComboBox.Text = skin.Root.Element("Name").Value;

            if (Widget.Sett.DegreeType == 1)
            {
                Fahrenheit.IsChecked = true;
            }
            else
            {
                Celsius.IsChecked = true;
            }

            if (Widget.Sett.TimeMode == 0)
            {
                TimeMode1.IsChecked = true;
            }
            else
            {
                TimeMode2.IsChecked = true;
            }

            if (Widget.Sett.EnableWallpaperChanging)
                WallpapersFolderPanel.IsEnabled = true;
            else
                WallpapersFolderPanel.IsEnabled = false;

            if (widget.providers.Count > 0)
            {
                foreach (WeatherProvider p in widget.providers)
                {
                    ProviderComboBox.Items.Add(p.Name);
                }
            }

            ProviderComboBox.SelectedIndex = widget.providers.IndexOf(widget.currentProvider);

            WallpapersFolderTextBox.Text = Widget.Sett.WallpapersFolder;
            ApplyButton.IsEnabled = false;
        }

        private void LocationBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(LocationBox.Text))
            {
                SelectedIndex = -1;
                string s = LocationBox.Text;
                ThreadStart threadStarter = delegate
                {
                    GetLocation(s);
                };
                Thread thread = new Thread(threadStarter);
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        private void GetLocation(string l)
        {
            results.Clear();

            SearchProgress.Dispatcher.Invoke((Action)delegate
            {
                SearchProgress.Visibility = System.Windows.Visibility.Visible;
            }, null);

            SearchResults.Dispatcher.Invoke((Action)delegate
            {
                SearchResults.Children.Clear();
            }, null);

            List<CityLocation> locations = widget.currentProvider.GetLocation(l);
            if (locations != null && locations.Count > 0)
            {
                foreach (CityLocation location in locations)
                {
                    SearchResults.Dispatcher.Invoke((Action)delegate
                                                                 {
                                                                     LocationItem item = new LocationItem();
                                                                     item.Header = location.City;
                                                                     item.Order = SearchResults.Children.Count;
                                                                     item.MouseLeftButtonDown +=
                                                                         new MouseButtonEventHandler(
                                                                             item_MouseLeftButtonDown);
                                                                     SearchResults.Children.Add(item);
                                                                 }, null);
                    results.Add(location);
                }
            }
            else
            {
                SearchResults.Dispatcher.Invoke((Action)delegate
                                             {
                                                 LocationItem item = new LocationItem();
                                                 item.Header = Widget.LocaleManager.GetString("NoResults");
                                                 item.IsEnabled = false;
                                                 SearchResults.Children.Add(item);
                                             }, null);
            }

            SearchProgress.Dispatcher.Invoke((Action)delegate
            {
                SearchProgress.Visibility = System.Windows.Visibility.Hidden;
            }, null);
        }

        void item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.SelectedIndex = SearchResults.Children.IndexOf((LocationItem)sender);
            if (Widget.Sett.LastCities == null)
                Widget.Sett.LastCities = new List<CityLocation>();
            if (!Widget.Sett.LastCities.Contains(results[SelectedIndex]))
            {
                Widget.Sett.LastCities.Insert(0, results[SelectedIndex]);
                if (Widget.Sett.LastCities.Count > 10)
                    Widget.Sett.LastCities.RemoveAt(Widget.Sett.LastCities.Count - 1);
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void ComboBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void IntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ApplyButton.IsEnabled)
            {
                ApplySettings();
            }

            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            ApplyButton.IsEnabled = false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SelectedIndex = -1;
            string s = LocationBox.Text;
            ThreadStart threadStarter = delegate
            {
                GetLocation(s);
            };
            Thread thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void ApplySettings()
        {
            if (Widget.Sett.WeatherProvider != ProviderComboBox.Text)
                Widget.Sett.LastCities.Clear();

            Widget.Sett.ShowIconOnTaskbar = (bool)TaskbarIconCheckBox.IsChecked;
            Widget.Sett.EnableWeather = (bool)WeatherCheckBox.IsChecked;
            Widget.Sett.EnableWeatherAnimation = (bool)WeatherAnimationCheckBox.IsChecked;
            Widget.Sett.Interval = Convert.ToInt32(IntervalComboBox.Text);
            Widget.Sett.EnableWallpaperChanging = (bool)WallpaperChangingCheckBox.IsChecked;
            Widget.Sett.ShowForecast = (bool)ShowForecastCheckBox.IsChecked;
            Widget.Sett.WeatherProvider = ProviderComboBox.Text;
            Widget.Sett.WallpapersFolder = WallpapersFolderTextBox.Text;
            Widget.Sett.DegreeType = (bool)Fahrenheit.IsChecked ? 1 : 0;
            Widget.Sett.TimeMode = (bool)TimeMode2.IsChecked ? 1 : 0;
            Widget.Sett.EnableSounds = (bool)EnableSoundsCheckBox.IsChecked;
            Widget.Sett.Skin = skins[SkinsComboBox.SelectedIndex];

            if (TaskbarIconCheckBox.IsChecked == true)
            {
                Widget.Parent.ShowInTaskbar = true;
                if (!string.IsNullOrEmpty(widget.weatherReport.NowSky))
                {
                    Widget.Parent.Title = widget.weatherReport.NowSky;
                }
                Widget.Parent.Icon = new BitmapImage(new Uri(Widget.ResourceManager.GetResourcePath(string.Format("Weather\\weather_{0}.png", widget.weatherReport.NowSkyCode))));
            }
            else
                Widget.Parent.ShowInTaskbar = false;

            foreach (WeatherProvider p in widget.providers)
            {
                if (p.Name == Widget.Sett.WeatherProvider)
                {
                    if (!p.IsLoaded)
                        p.Load();
                    widget.currentProvider = p;
                }
            }

            Widget.Sett.Write(HTCHome.Core.Environment.ConfigDirectory + "\\WeatherClockWidget.conf");
            widget.UpdateSettings();
        }

        private void SkinsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XDocument doc = XDocument.Load(E.Path + "\\WeatherClock\\Skins\\" + skins[SkinsComboBox.SelectedIndex] + "\\Skin.xml");
            AuthorTextBlock.Text = Widget.LocaleManager.GetString("Author") + " " + doc.Root.Element("Author").Value;
            VersionTextBlock.Text = Widget.LocaleManager.GetString("Version") + " " + doc.Root.Element("Version").Value;
            ContactsTextBlock.Text = Widget.LocaleManager.GetString("Contacts") + " " + doc.Root.Element("Contacts").Value;
            DescriptionTextBlock.Text = doc.Root.Element("Description").Value;

            if (File.Exists(E.Path + "\\WeatherClock\\Skins\\" + skins[SkinsComboBox.SelectedIndex] + "\\Preview.png"))
                SkinPreview.Source = new BitmapImage(new Uri(E.Path + "\\WeatherClock\\Skins\\" + skins[SkinsComboBox.SelectedIndex] + "\\Preview.png"));
            WeatherClock.UseClockAnimation = Convert.ToBoolean(doc.Root.Element("UseClockAnimation").Value);

            ApplyButton.IsEnabled = true;
        }

        private void ContactString_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            WinAPI.ShellExecute(IntPtr.Zero, "open", ContactString.Text, string.Empty, string.Empty, 0);
        }
    }
}
