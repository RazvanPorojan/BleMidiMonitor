using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BleMidiMonitor
{
    public class BleMidiDevice : INotifyPropertyChanged
    {
        private string _name;
        private bool _isConnected;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public ulong BluetoothAddress { get; set; }

        public string DeviceId { get; set; }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string AddressString => BluetoothAddress.ToString("X12");

        public string StatusText => IsConnected ? "Connected" : "Disconnected";

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
