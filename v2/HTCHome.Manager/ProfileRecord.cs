using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace HTCHome.Manager
{
    [DataContract]
    public sealed class ProfileRecord : INotifyPropertyChanged
    {
        private string name;
        private bool autoStart;
        private bool resumeHideControl;
        private string resumeDiagnosticMode;
        private bool isRunning;

        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string Name
        {
            get { return name; }
            set { name = value; OnPropertyChanged(); }
        }

        [DataMember(Order = 3)]
        public bool AutoStart
        {
            get { return autoStart; }
            set
            {
                if (autoStart == value) return;
                autoStart = value;
                OnPropertyChanged();
                OnPropertyChanged("AutoStartText");
            }
        }

        [DataMember(Order = 4)]
        public bool ResumeHideControl
        {
            get { return resumeHideControl; }
            set { resumeHideControl = value; }
        }

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public string ResumeDiagnosticMode
        {
            get { return resumeDiagnosticMode; }
            set
            {
                if (string.Equals(resumeDiagnosticMode, value, StringComparison.OrdinalIgnoreCase)) return;
                resumeDiagnosticMode = value;
                OnPropertyChanged();
                OnPropertyChanged("EffectiveResumeDiagnosticMode");
                OnPropertyChanged("ResumeDiagnosticText");
            }
        }

        public string EffectiveResumeDiagnosticMode
        {
            get
            {
                string mode = (ResumeDiagnosticMode ?? string.Empty).Trim().ToLowerInvariant();

                // Timing-matrix migration. Reuse the exact profile slots from run #54:
                // old Hide -> immediate target restore, old TargetOff/Cloak -> +3s,
                // old Minimize -> +12s. No Window Hide/Minimize is used by these modes.
                if (mode == "hide") return "target0";
                if (mode == "targetoff" || mode == "cloak") return "target3";
                if (mode == "minimize") return "target12";
                if (mode == "target0" || mode == "target3" || mode == "target12" || mode == "normal")
                    return mode;

                return ResumeHideControl ? "target0" : "normal";
            }
        }

        public bool IsRunning
        {
            get { return isRunning; }
            set
            {
                if (isRunning == value) return;
                isRunning = value;
                OnPropertyChanged();
                OnPropertyChanged("StatusText");
            }
        }

        public string StatusText { get { return IsRunning ? ManagerText.Running : ManagerText.Stopped; } }
        public string AutoStartText { get { return AutoStart ? ManagerText.Yes : ManagerText.No; } }
        public string ResumeDiagnosticText { get { return ManagerText.ResumeDiagnosticModeText(EffectiveResumeDiagnosticMode); } }
        public string ShortId { get { return string.IsNullOrEmpty(Id) ? string.Empty : Id.Substring(0, Math.Min(8, Id.Length)); } }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RefreshLocalizedText()
        {
            OnPropertyChanged("StatusText");
            OnPropertyChanged("AutoStartText");
            OnPropertyChanged("ResumeDiagnosticText");
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
