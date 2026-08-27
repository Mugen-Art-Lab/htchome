using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HTCHome.Manager
{
    public sealed class ProfileRecord : INotifyPropertyChanged
    {
        private string name;
        private bool isRunning;

        public string Id { get; set; }

        public string Name
        {
            get { return name; }
            set { name = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get { return isRunning; }
            set { isRunning = value; OnPropertyChanged(); OnPropertyChanged("StatusText"); }
        }

        public string StatusText { get { return IsRunning ? "Запущен" : "Остановлен"; } }
        public string ShortId { get { return string.IsNullOrEmpty(Id) ? string.Empty : Id.Substring(0, Math.Min(8, Id.Length)); } }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
