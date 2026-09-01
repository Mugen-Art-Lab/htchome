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

                // Prototype-fix convergence after run #56. All previously protected
                // laboratory slots now use the same proven path:
                // TargetOff on Suspend -> synchronous TargetOn at PowerModes.Resume.
                if (mode == "normal") return "normal";
                if (mode == "target0" || mode == "target3" || mode == "target12" ||
                    mode == "targetoff" || mode == "hide" || mode == "cloak" || mode == "minimize")
                    return "target0";

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
