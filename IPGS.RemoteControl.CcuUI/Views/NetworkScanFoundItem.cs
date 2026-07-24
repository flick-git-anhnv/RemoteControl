using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPGS.RemoteControl.CcuClient;

namespace IPGS.RemoteControl.CcuUI.Views
{
    /// <summary>
    /// Wrapper hiển thị cho 1 kết quả quét mạng — bọc thêm trạng thái "đã thêm vào
    /// danh sách chưa" (chỉ dùng cho UI, KHÔNG thuộc <see cref="DiscoveredZcuAgent"/>
    /// vì đó là record thuần bên CcuClient, không cần biết về UI state).
    /// </summary>
    public sealed class NetworkScanFoundItem : INotifyPropertyChanged
    {
        public DiscoveredZcuAgent Source { get; }

        public string Host => Source.Host;
        public int Port => Source.Port;
        public string ServerName => Source.ServerName;
        public int ScreenWidth => Source.ScreenWidth;
        public int ScreenHeight => Source.ScreenHeight;

        private bool _isAdded;
        public bool IsAdded
        {
            get => _isAdded;
            set
            {
                if (_isAdded == value) return;
                _isAdded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AddButtonLabel));
            }
        }

        public string AddButtonLabel => IsAdded ? "✓ Đã thêm" : "+ Thêm vào danh sách";

        public NetworkScanFoundItem(DiscoveredZcuAgent source, bool isAdded)
        {
            Source = source;
            _isAdded = isAdded;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
