using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualBasic;

namespace HTCHome.Manager
{
    public partial class MainWindow : Window
    {
        private readonly string rootDirectory;
        private readonly ProfileStore store;
        private readonly ProcessController processes;
        private readonly AutostartController autostart;
        private readonly ObservableCollection<ProfileRecord> profiles;
        private readonly DispatcherTimer refreshTimer;
        private ManagerSettings settings;
        private bool refreshing;
        private bool languageChanging;
        private bool checkChanging;

        public MainWindow()
        {
            InitializeComponent();

            rootDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            store = new ProfileStore(rootDirectory);
            processes = new ProcessController(rootDirectory);
            autostart = new AutostartController(rootDirectory);
            settings = store.LoadManagerSettings();
            profiles = new ObservableCollection<ProfileRecord>(store.LoadAll());
            ProfilesList.ItemsSource = profiles;

            ManagerText.SetLanguage(string.IsNullOrWhiteSpace(settings.Language) ? ManagerText.DetectLanguage() : settings.Language);
            SelectLanguage(ManagerText.Language);
            ApplyLanguage();
            RestoreWindowPlacement();

            checkChanging = true;
            ManagerAutoStartCheckBox.IsChecked = autostart.IsEnabled();
            checkChanging = false;

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
            refreshTimer.Tick += async delegate { await RefreshStatusesAsync(); };
            refreshTimer.Start();

            Loaded += async delegate
            {
                await RefreshStatusesAsync();
                if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase)))
                    await StartAutoProfilesAsync();
            };

            UpdateButtons();
        }

        private ProfileRecord SelectedProfile { get { return ProfilesList.SelectedItem as ProfileRecord; } }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = Interaction.InputBox(ManagerText.NewInstancePrompt, "HTC Home Mugen", ManagerText.NewInstanceDefault);
            if (string.IsNullOrWhiteSpace(name)) return;
            var profile = store.Create(name);
            profiles.Add(profile);
            ProfilesList.SelectedItem = profile;
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = SelectedProfile;
            if (profile == null) return;
            string name = Interaction.InputBox(ManagerText.RenamePrompt, "HTC Home Mugen", profile.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            profile.Name = name.Trim();
            store.Save(profile);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try { processes.Start(SelectedProfile); await Task.Delay(250); await RefreshStatusesAsync(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            processes.Stop(SelectedProfile);
            await Task.Delay(150);
            await RefreshStatusesAsync();
        }

        private async void StartAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var profile in profiles) processes.Start(profile);
            await Task.Delay(300);
            await RefreshStatusesAsync();
        }

        private async void StopAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var profile in profiles) processes.Stop(profile);
            await Task.Delay(200);
            await RefreshStatusesAsync();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = SelectedProfile;
            if (profile == null) return;
            if (profile.IsRunning)
            {
                MessageBox.Show(ManagerText.StopBeforeDelete, "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show(ManagerText.DeleteQuestion(profile.Name), "HTC Home Mugen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            store.Delete(profile);
            profiles.Remove(profile);
        }

        private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            checkChanging = true;
            ProfileAutoStartCheckBox.IsChecked = SelectedProfile != null && SelectedProfile.AutoStart;
            checkChanging = false;
            UpdateButtons();
        }

        private void ProfileAutoStartCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (checkChanging || SelectedProfile == null) return;
            SelectedProfile.AutoStart = ProfileAutoStartCheckBox.IsChecked == true;
            store.Save(SelectedProfile);
        }

        private void ManagerAutoStartCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (checkChanging) return;
            bool enabled = ManagerAutoStartCheckBox.IsChecked == true;
            autostart.SetEnabled(enabled);
            settings.AutoStartManager = enabled;
            SaveManagerSettings();
        }

        private async Task StartAutoProfilesAsync()
        {
            foreach (var profile in profiles.Where(p => p.AutoStart)) processes.Start(profile);
            await Task.Delay(300);
            await RefreshStatusesAsync();
        }

        private async Task RefreshStatusesAsync()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                var running = await Task.Run(() => processes.GetRunningProfileIds());
                foreach (var profile in profiles) profile.IsRunning = running.Contains(profile.Id);
                UpdateButtons();
            }
            finally { refreshing = false; }
        }

        private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (languageChanging || LanguageBox.SelectedItem == null) return;
            var item = LanguageBox.SelectedItem as ComboBoxItem;
            if (item == null) return;
            ManagerText.SetLanguage(item.Tag as string);
            settings.Language = ManagerText.Language;
            ApplyLanguage();
            SaveManagerSettings();
        }

        private void SelectLanguage(string language)
        {
            languageChanging = true;
            try
            {
                foreach (ComboBoxItem item in LanguageBox.Items)
                    if (string.Equals(item.Tag as string, language, StringComparison.OrdinalIgnoreCase)) { LanguageBox.SelectedItem = item; break; }
            }
            finally { languageChanging = false; }
        }

        private void ApplyLanguage()
        {
            Title = ManagerText.WindowTitle;
            HeaderText.Text = ManagerText.Header;
            SubtitleText.Text = ManagerText.Subtitle;
            LanguageLabel.Text = ManagerText.LanguageLabel;
            NameColumn.Header = ManagerText.NameHeader;
            StatusColumn.Header = ManagerText.StatusHeader;
            AutoStartColumn.Header = ManagerText.AutoStartHeader;
            AddButton.Content = ManagerText.Add;
            RenameButton.Content = ManagerText.Rename;
            StartButton.Content = ManagerText.Start;
            StopButton.Content = ManagerText.Stop;
            DeleteButton.Content = ManagerText.Delete;
            StartAllButton.Content = ManagerText.StartAll;
            StopAllButton.Content = ManagerText.StopAll;
            ManagerAutoStartCheckBox.Content = ManagerText.ManagerAutoStart;
            ProfileAutoStartCheckBox.Content = ManagerText.ProfileAutoStart;
            foreach (var profile in profiles) profile.RefreshLocalizedText();
        }

        private void RestoreWindowPlacement()
        {
            if (!settings.HasWindowPlacement) return;
            if (settings.Width >= MinWidth) Width = settings.Width;
            if (settings.Height >= MinHeight) Height = settings.Height;
            Left = settings.Left;
            Top = settings.Top;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            settings.Language = ManagerText.Language;
            settings.AutoStartManager = ManagerAutoStartCheckBox.IsChecked == true;
            settings.Left = RestoreBounds.Left;
            settings.Top = RestoreBounds.Top;
            settings.Width = RestoreBounds.Width;
            settings.Height = RestoreBounds.Height;
            settings.HasWindowPlacement = true;
            SaveManagerSettings();
        }

        private void SaveManagerSettings()
        {
            try { store.SaveManagerSettings(settings); } catch { }
        }

        private void UpdateButtons()
        {
            var profile = SelectedProfile;
            bool selected = profile != null;
            RenameButton.IsEnabled = selected;
            DeleteButton.IsEnabled = selected && !profile.IsRunning;
            StartButton.IsEnabled = selected && !profile.IsRunning;
            StopButton.IsEnabled = selected && profile.IsRunning;
            ProfileAutoStartCheckBox.IsEnabled = selected;
            StartAllButton.IsEnabled = profiles.Any(p => !p.IsRunning);
            StopAllButton.IsEnabled = profiles.Any(p => p.IsRunning);
        }
    }
}
