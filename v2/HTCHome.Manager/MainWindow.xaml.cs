using System;
using System.Collections.ObjectModel;
using System.IO;
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

        public MainWindow()
        {
            InitializeComponent();

            rootDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            store = new ProfileStore(rootDirectory);
            processes = new ProcessController(rootDirectory);
            profiles = new ObservableCollection<ProfileRecord>(store.LoadAll());
            ProfilesList.ItemsSource = profiles;

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            refreshTimer.Tick += delegate { RefreshStatuses(); };
            refreshTimer.Start();

            RefreshStatuses();
            UpdateButtons();
        }

        private ProfileRecord SelectedProfile { get { return ProfilesList.SelectedItem as ProfileRecord; } }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = Interaction.InputBox("Имя нового экземпляра:", "HTC Home Mugen", "Новый экземпляр");
            if (string.IsNullOrWhiteSpace(name)) return;
            var profile = store.Create(name);
            profiles.Add(profile);
            ProfilesList.SelectedItem = profile;
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = SelectedProfile;
            if (profile == null) return;
            string name = Interaction.InputBox("Новое имя экземпляра:", "HTC Home Mugen", profile.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            profile.Name = name.Trim();
            store.Save(profile);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                processes.Start(SelectedProfile);
                RefreshStatuses();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            processes.Stop(SelectedProfile);
            RefreshStatuses();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = SelectedProfile;
            if (profile == null) return;
            if (profile.IsRunning)
            {
                MessageBox.Show("Сначала остановите экземпляр.", "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show("Удалить профиль «" + profile.Name + "»?", "HTC Home Mugen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            store.Delete(profile);
            profiles.Remove(profile);
        }

        private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();
        }

        private void RefreshStatuses()
        {
            foreach (var profile in profiles)
                profile.IsRunning = processes.IsRunning(profile.Id);
            UpdateButtons();
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
