using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Com_ELF.Models
{
    public class WatchVariable : INotifyPropertyChanged
    {
        private string _currentValue = string.Empty;
        private string _pendingValue = string.Empty;
        private string _status = string.Empty;
        private string _lastReadTimestamp = string.Empty;

        public string Name { get; set; } = string.Empty;
        public uint Address { get; set; }
        public uint ByteSize { get; set; }
        public WatchDataType DataType { get; set; }

        public string CurrentValue
        {
            get => _currentValue;
            set => SetField(ref _currentValue, value);
        }

        public string PendingValue
        {
            get => _pendingValue;
            set => SetField(ref _pendingValue, value);
        }

        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        public string LastReadTimestamp
        {
            get => _lastReadTimestamp;
            set => SetField(ref _lastReadTimestamp, value);
        }

        public string AddressHex => $"0x{Address:X8}";

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}