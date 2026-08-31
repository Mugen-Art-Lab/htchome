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

        // Legacy run #51 flag. Kept only so existing profile JSON migrates cleanly.
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
                // Run #52/#53 used "cloak" on the right-monitor profile. Starting
                // with the HwndTarget experiment, transparently reuse that slot as
                // targetoff so the existing four-profile matrix needs no manual edit.
                if (mode == "cloak") return "targetoff";
                if (mode == "hide" || mode == "targetoff" || mode == "minimize" || mode == "normal")
                    return mode;
                return ResumeHideControl ? "hide" : "normal";
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
