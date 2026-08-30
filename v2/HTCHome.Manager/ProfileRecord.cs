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
            set
            {
                if (resumeHideControl == value) return;
                resumeHideControl = value;
                OnPropertyChanged();
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
        public string ShortId { get { return string.IsNullOrEmpty(Id) ? string.Empty : Id.Substring(0, Math.Min(8, Id.Length)); } }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RefreshLocalizedText()
        {
            OnPropertyChanged("StatusText");
            OnPropertyChanged("AutoStartText");
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
