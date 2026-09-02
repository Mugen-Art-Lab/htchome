using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using Forms = System.Windows.Forms;

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
        private readonly bool launchedFromAutostart;
        private ManagerSettings settings;
        private Forms.NotifyIcon trayIcon;
        private Forms.ToolStripMenuItem trayOpenItem;
        private Forms.ToolStripMenuItem trayStartAllItem;
        private Forms.ToolStripMenuItem trayStopAllItem;
        private Forms.ToolStripMenuItem trayExitItem;
        private bool refreshing;
        private bool languageChanging;
        private bool checkChanging;
        private bool allowExit;

        public MainWindow()
        {
            InitializeComponent();

            rootDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            store = new ProfileStore(rootDirectory);
            processes = new ProcessController(rootDirectory);
            autostart = new AutostartController(rootDirectory);
            settings = store.LoadManagerSettings();
            profiles = new ObservableCollection<ProfileRecord>(store.LoadAll());
            MigrateLegacyResumeDiagnostics();
            ProfilesList.ItemsSource = profiles;
            launchedFromAutostart = Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

            ManagerText.SetLanguage(string.IsNullOrWhiteSpace(settings.Language) ? ManagerText.DetectLanguage() : settings.Language);
            SelectLanguage(ManagerText.Language);
            ApplyLanguage();
            RestoreWindowPlacement();
            InitializeTray();

            checkChanging = true;
            ManagerAutoStartCheckBox.IsChecked = autostart.IsEnabled();
            SelectResumeDiagnosticMode("normal");
            checkChanging = false;

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
            refreshTimer.Tick += async delegate { await RefreshStatusesAsync(); };
            refreshTimer.Start();

            Loaded += async delegate
            {
                await RefreshStatusesAsync();
                if (launchedFromAutostart)
                {
                    await StartAutoProfilesAsync();
                    HideToTray();
                }
            };

            UpdateButtons();
        }

        private ProfileRecord SelectedProfile { get { return ProfilesList.SelectedItem as ProfileRecord; } }

        private void MigrateLegacyResumeDiagnostics()
        {
            foreach (ProfileRecord profile in profiles)
            {
                bool changed = false;
                if (profile.ResumeHideControl && string.IsNullOrWhiteSpace(profile.ResumeDiagnosticMode))
                {
                    profile.ResumeDiagnosticMode = "hide";
                    changed = true;
                }
                if (profile.ResumeHideControl)
                {
                    profile.ResumeHideControl = false;
                    changed = true;
                }
                if (changed)
                {
                    try { store.Save(profile); } catch { }
                }
            }
        }

        private void InitializeTray()
        {
            trayOpenItem = new Forms.ToolStripMenuItem();
            trayStartAllItem = new Forms.ToolStripMenuItem();
            trayStopAllItem = new Forms.ToolStripMenuItem();
            trayExitItem = new Forms.ToolStripMenuItem();

            trayOpenItem.Click += delegate { Dispatcher.BeginInvoke(new Action(ShowManager)); };
            trayStartAllItem.Click += delegate { Dispatcher.BeginInvoke(new Action(StartAllFromTray)); };
            trayStopAllItem.Click += delegate { Dispatcher.BeginInvoke(new Action(StopAllFromTray)); };
            trayExitItem.Click += delegate { Dispatcher.BeginInvoke(new Action(ExitManager)); };

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add(trayOpenItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(trayStartAllItem);
            menu.Items.Add(trayStopAllItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(trayExitItem);

            System.Drawing.Icon icon = null;
            try { icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }
            trayIcon = new Forms.NotifyIcon
            {
                Icon = icon ?? SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = menu
            };
            trayIcon.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(ShowManager)); };
            ApplyTrayLanguage();
        }

        private void ApplyTrayLanguage()
        {
            if (trayIcon == null) return;
            trayIcon.Text = ManagerText.TrayTip.Length > 63 ? ManagerText.TrayTip.Substring(0, 63) : ManagerText.TrayTip;
            trayOpenItem.Text = ManagerText.TrayOpen;
            trayStartAllItem.Text = ManagerText.StartAll;
            trayStopAllItem.Text = ManagerText.StopAll;
            trayExitItem.Text = ManagerText.TrayExit;
        }

        private void ShowManager()
        {
            ShowInTaskbar = true;
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            ManagerPresentationRecovery.Arm(this, "show-manager");
        }

        private void HideToTray()
        {
            SaveWindowPlacement();
            ShowInTaskbar = false;
            Hide();
        }

        private void ExitManager()
        {
            allowExit = true;
            SaveWindowPlacement();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            Application.Current.Shutdown();
        }

        private void NvidiaCompatibilityButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new NvidiaCompatibilityWindow(rootDirectory, profiles) { Owner = this };
            window.ShowDialog();
        }

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
                ManagerPresentationRecovery.Arm(this, "start-profile");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = SelectedProfile;
            if (profile == null) return;
            await Task.Run(() => processes.Stop(profile));
            await RefreshStatusesAsync();
            ManagerPresentationRecovery.Arm(this, "stop-profile");
        }

        private async void StartAllButton_Click(object sender, RoutedEventArgs e) { await StartAllAsync(); }
        private async void StopAllButton_Click(object sender, RoutedEventArgs e) { await StopAllAsync(); }
        private async void StartAllFromTray() { await StartAllAsync(); }
        private async void StopAllFromTray() { await StopAllAsync(); }

        private async Task StartAllAsync()
        {
            foreach (var profile in profiles) processes.Start(profile);
            await Task.Delay(300);
            await RefreshStatusesAsync();
            ManagerPresentationRecovery.Arm(this, "start-all");
        }

        private async Task StopAllAsync()
        {
            var snapshot = profiles.ToArray();
            await Task.Run(() =>
            {
                foreach (var profile in snapshot) processes.Stop(profile);
            });
            await RefreshStatusesAsync();
            ManagerPresentationRecovery.Arm(this, "stop-all");
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
            SelectResumeDiagnosticMode(SelectedProfile == null ? "normal" : SelectedProfile.EffectiveResumeDiagnosticMode);
            checkChanging = false;
            UpdateButtons();
        }

        private void ProfileAutoStartCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (checkChanging || SelectedProfile == null) return;
            SelectedProfile.AutoStart = ProfileAutoStartCheckBox.IsChecked == true;
            store.Save(SelectedProfile);
        }

        private void ResumeDiagnosticBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (checkChanging || SelectedProfile == null || ResumeDiagnosticBox.SelectedItem == null) return;
            ComboBoxItem item = ResumeDiagnosticBox.SelectedItem as ComboBoxItem;
            if (item == null) return;
            string mode = item.Tag as string;
            if (string.IsNullOrWhiteSpace(mode)) return;

            SelectedProfile.ResumeDiagnosticMode = mode;
            SelectedProfile.ResumeHideControl = false;
            store.Save(SelectedProfile);
        }

        private void SelectResumeDiagnosticMode(string mode)
        {
            string wanted = string.IsNullOrWhiteSpace(mode) ? "normal" : mode;
            foreach (ComboBoxItem item in ResumeDiagnosticBox.Items)
            {
                if (string.Equals(item.Tag as string, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    ResumeDiagnosticBox.SelectedItem = item;
                    return;
                }
            }
            ResumeDiagnosticBox.SelectedItem = ResumeNormalItem;
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
            ResumeDiagnosticColumn.Header = ManagerText.ResumeDiagnosticHeader;
            AddButton.Content = ManagerText.Add;
            RenameButton.Content = ManagerText.Rename;
            StartButton.Content = ManagerText.Start;
            StopButton.Content = ManagerText.Stop;
            DeleteButton.Content = ManagerText.Delete;
            StartAllButton.Content = ManagerText.StartAll;
            StopAllButton.Content = ManagerText.StopAll;
            NvidiaCompatibilityButton.Content = ManagerText.NvidiaCompatibility;
            ManagerAutoStartCheckBox.Content = ManagerText.ManagerAutoStart;
            ProfileAutoStartCheckBox.Content = ManagerText.ProfileAutoStart;
            ResumeDiagnosticLabel.Text = ManagerText.ResumeDiagnosticLabel;
            ResumeDiagnosticHint.Text = ManagerText.ResumeDiagnosticHint;
            ResumeNormalItem.Content = ManagerText.ResumeDiagnosticNormal;
            ResumeHideItem.Content = ManagerText.ResumeDiagnosticHide;
            ResumeCloakItem.Content = ManagerText.ResumeDiagnosticCloak;
            ResumeMinimizeItem.Content = ManagerText.ResumeDiagnosticMinimize;
            foreach (var profile in profiles) profile.RefreshLocalizedText();
            ApplyTrayLanguage();
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

        private void SaveWindowPlacement()
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

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized) HideToTray();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveWindowPlacement();
            if (!allowExit)
            {
                e.Cancel = true;
                HideToTray();
            }
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
            ResumeDiagnosticBox.IsEnabled = selected && !profile.IsRunning;
            StartAllButton.IsEnabled = profiles.Any(p => !p.IsRunning);
            StopAllButton.IsEnabled = profiles.Any(p => p.IsRunning);
            if (trayStartAllItem != null) trayStartAllItem.Enabled = profiles.Any(p => !p.IsRunning);
            if (trayStopAllItem != null) trayStopAllItem.Enabled = profiles.Any(p => p.IsRunning);
        }
    }
}
