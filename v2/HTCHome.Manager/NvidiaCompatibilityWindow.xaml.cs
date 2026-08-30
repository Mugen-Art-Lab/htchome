using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace HTCHome.Manager
{
    public partial class NvidiaCompatibilityWindow : Window
    {
        private readonly NvidiaCompatibilityController controller;
        private readonly IEnumerable<ProfileRecord> profiles;
        private readonly ObservableCollection<NvidiaRow> rows = new ObservableCollection<NvidiaRow>();
        private readonly Dictionary<string, int> baselineHandles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer timer;
        private bool refreshing;

        public NvidiaCompatibilityWindow(string rootDirectory, IEnumerable<ProfileRecord> profiles)
        {
            InitializeComponent();
            controller = new NvidiaCompatibilityController(rootDirectory);
            this.profiles = profiles;
            RowsList.ItemsSource = rows;
            ApplyLanguage();

            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += async delegate { await RefreshAsync(); };
            timer.Start();
            Loaded += async delegate { await RefreshAsync(); };
            Closed += delegate { timer.Stop(); };
        }

        private async Task RefreshAsync()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                var snapshots = await Task.Run(() => controller.GetSnapshots(profiles));
                rows.Clear();
                foreach (var snapshot in snapshots)
                {
                    int baseline;
                    if (snapshot.ProcessId.HasValue && !baselineHandles.TryGetValue(snapshot.ProfileId, out baseline))
                    {
                        baseline = snapshot.Handles;
                        baselineHandles[snapshot.ProfileId] = baseline;
                    }

                    int delta = snapshot.ProcessId.HasValue && baselineHandles.TryGetValue(snapshot.ProfileId, out baseline)
                        ? snapshot.Handles - baseline : 0;

                    rows.Add(new NvidiaRow(snapshot, delta));
                }

                ApplyExclusionStatus();
                UpdatedText.Text = ManagerText.NvidiaUpdated(DateTime.Now);
            }
            finally { refreshing = false; }
        }

        private void ApplyLanguage()
        {
            Title = ManagerText.NvidiaWindowTitle;
            HeaderText.Text = ManagerText.NvidiaHeader;
            DescriptionText.Text = ManagerText.NvidiaDescription;
            ProfileColumn.Header = ManagerText.NvidiaProfile;
            NvidiaColumn.Header = ManagerText.NvidiaModule;
            DeltaColumn.Header = ManagerText.NvidiaDelta;
            HealthColumn.Header = ManagerText.NvidiaHealth;
            ExclusionHeaderText.Text = ManagerText.NvidiaExclusionHeader;
            ExclusionNoteText.Text = ManagerText.NvidiaExclusionNote;
            ApplyExclusionsButton.Content = ManagerText.NvidiaApplyExclusions;
            RefreshButton.Content = ManagerText.NvidiaRefresh;
            CloseButton.Content = ManagerText.NvidiaClose;
            ApplyExclusionStatus();
        }

        private void ApplyExclusionStatus()
        {
            bool present = NvidiaCompatibilityController.AreFrameViewExclusionsPresent();
            ExclusionStatusText.Text = present ? ManagerText.NvidiaExclusionsPresent : ManagerText.NvidiaExclusionsMissing;
            ApplyExclusionsButton.IsEnabled = !present;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async void ApplyExclusionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(ManagerText.NvidiaApplyQuestion, "HTC Home Mugen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await Task.Run(() => NvidiaCompatibilityController.ApplyFrameViewExclusions());
                ApplyExclusionStatus();
                MessageBox.Show(ManagerText.NvidiaApplySuccess, "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(ManagerText.NvidiaAdminRequired, "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ManagerText.NvidiaApplyFailed + Environment.NewLine + ex.Message, "HTC Home Mugen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private sealed class NvidiaRow : INotifyPropertyChanged
        {
            public string ProfileName { get; private set; }
            public string PidText { get; private set; }
            public string NvidiaText { get; private set; }
            public string HandlesText { get; private set; }
            public string DeltaText { get; private set; }
            public string HealthText { get; private set; }

            public NvidiaRow(NvidiaProcessSnapshot snapshot, int delta)
            {
                ProfileName = snapshot.ProfileName;
                if (!snapshot.ProcessId.HasValue)
                {
                    PidText = "—";
                    NvidiaText = "—";
                    HandlesText = "—";
                    DeltaText = "—";
                    HealthText = ManagerText.NvidiaStopped;
                    return;
                }

                PidText = snapshot.ProcessId.Value.ToString();
                NvidiaText = snapshot.NvidiaModuleLoaded ? ManagerText.Yes : ManagerText.No;
                HandlesText = snapshot.Handles.ToString();
                DeltaText = delta > 0 ? "+" + delta : delta.ToString();

                if (delta >= 1500 || snapshot.Handles >= 3000)
                    HealthText = ManagerText.NvidiaSuspicious;
                else if (delta >= 500 || snapshot.Handles >= 1800)
                    HealthText = ManagerText.NvidiaWatch;
                else
                    HealthText = ManagerText.NvidiaNormal;
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
