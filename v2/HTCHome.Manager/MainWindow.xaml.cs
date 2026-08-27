using System;
using System.Collections.ObjectModel;
using System.IO;
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
        private readonly ObservableCollection<ProfileRecord> profiles;
        private readonly DispatcherTimer refreshTimer;
        private bool refreshing;
        private bool languageChanging;

        public MainWindow()
        {
            InitializeComponent();

            rootDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            store = new ProfileStore(rootDirectory);
            processes = new ProcessController(rootDirectory);
            profiles = new ObservableCollection<ProfileRecord>(store.LoadAll());
            ProfilesList.ItemsSource = profiles;

            ManagerText.SetLanguage(ManagerText.DetectLanguage());
            SelectLanguage(ManagerText.Language);
            ApplyLanguage();

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            refreshTimer.Tick += async delegate { await RefreshStatusesAsync(); };
            refreshTimer.Start();

            Loaded += async delegate { await RefreshStatusesAsync(); };
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
            try
            {
                processes.Start(SelectedProfile);
                await Task.Delay(250);
                await RefreshStatusesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            processes.Stop(SelectedProfile);
            await Task.Delay(150);
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
            if (MessageBox.Show(ManagerText.DeleteQuestion(profile.Name), "HTC Home Mugen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            store.Delete(profile);
            profiles.Remove(profile);
        }

        private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();
        }

        private async Task RefreshStatusesAsync()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                var running = await Task.Run(() => processes.GetRunningProfileIds());
                foreach (var profile in profiles)
                    profile.IsRunning = running.Contains(profile.Id);
                UpdateButtons();
            }
            finally
            {
                refreshing = false;
            }
        }

        private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (languageChanging || LanguageBox.SelectedItem == null) return;
            var item = LanguageBox.SelectedItem as ComboBoxItem;
            if (item == null) return;
            ManagerText.SetLanguage(item.Tag as string);
            ApplyLanguage();
        }

        private void SelectLanguage(string language)
        {
            languageChanging = true;
            try
            {
                foreach (ComboBoxItem item in LanguageBox.Items)
                {
                    if (string.Equals(item.Tag as string, language, StringComparison.OrdinalIgnoreCase))
                    {
                        LanguageBox.SelectedItem = item;
                        break;
                    }
                }
            }
            finally
            {
                languageChanging = false;
            }
        }

        private void ApplyLanguage()
        {
            Title = ManagerText.WindowTitle;
            HeaderText.Text = ManagerText.Header;
            SubtitleText.Text = ManagerText.Subtitle;
            LanguageLabel.Text = ManagerText.LanguageLabel;
            NameColumn.Header = ManagerText.NameHeader;
            StatusColumn.Header = ManagerText.StatusHeader;
            AddButton.Content = ManagerText.Add;
            RenameButton.Content = ManagerText.Rename;
            StartButton.Content = ManagerText.Start;
            StopButton.Content = ManagerText.Stop;
            DeleteButton.Content = ManagerText.Delete;

            foreach (var profile in profiles)
                profile.RefreshStatusText();
        }

        private void UpdateButtons()
        {
            var profile = SelectedProfile;
            bool selected = profile != null;
            RenameButton.IsEnabled = selected;
            DeleteButton.IsEnabled = selected && !profile.IsRunning;
            StartButton.IsEnabled = selected && !profile.IsRunning;
            StopButton.IsEnabled = selected && profile.IsRunning;
        }
    }
}
