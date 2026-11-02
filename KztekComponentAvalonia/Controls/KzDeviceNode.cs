using Avalonia.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>Observable data node for KzDeviceTree.</summary>
public class KzDeviceNode : INotifyPropertyChanged
{
    private string         _name       = "";
    private KzDeviceType   _deviceType = KzDeviceType.Generic;
    private KzDeviceStatus _status     = KzDeviceStatus.Unknown;
    private string         _ipAddress  = "";
    private string         _deviceId   = "";
    private bool           _isExpanded;
    private int            _slotTotal     = 0;
    private int            _slotAvailable = 0;
    private bool           _showGroupCounters = false;  // Backward compat: mặc định TẮT
    private int            _groupOnlineCount  = 0;
    private int            _groupOfflineCount = 0;
    private bool           _showStatus        = true;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public KzDeviceType DeviceType
    {
        get => _deviceType;
        set { _deviceType = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeviceIcon)); OnPropertyChanged(nameof(NameBrush)); OnPropertyChanged(nameof(StatusLabel)); OnPropertyChanged(nameof(StatusBrush)); OnPropertyChanged(nameof(StatusBadgeBrush)); OnPropertyChanged(nameof(IsLeaf)); OnPropertyChanged(nameof(IsGroup)); OnPropertyChanged(nameof(ShowGroupBadges)); OnPropertyChanged(nameof(ShowStatus)); }
    }

    public KzDeviceStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeviceIcon));
            OnPropertyChanged(nameof(DeviceIconBrush));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusBadgeBrush));
        }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(); }
    }

    public string DeviceId
    {
        get => _deviceId;
        set { _deviceId = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public int SlotTotal
    {
        get => _slotTotal;
        set { _slotTotal = value; OnPropertyChanged(); OnPropertyChanged(nameof(SlotText)); OnPropertyChanged(nameof(SlotLabel)); OnPropertyChanged(nameof(SlotBrush)); OnPropertyChanged(nameof(HasSlots)); OnPropertyChanged(nameof(NameBrush)); }
    }

    public int SlotAvailable
    {
        get => _slotAvailable;
        set { _slotAvailable = value; OnPropertyChanged(); OnPropertyChanged(nameof(SlotText)); OnPropertyChanged(nameof(SlotLabel)); OnPropertyChanged(nameof(SlotBrush)); OnPropertyChanged(nameof(NameBrush)); }
    }

    public ObservableCollection<KzDeviceNode> Children { get; } = new();

    public KzDeviceNode()
    {
        Children.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasChildren)); OnPropertyChanged(nameof(ShowGroupBadges)); };
    }

    /// <summary>Whether this node has child nodes to expand — independent of DeviceType.</summary>
    public bool HasChildren => Children.Count > 0;

    // ── Computed display properties (used in AXAML bindings) ──────────────────

    public string DeviceIcon => DeviceType switch
    {
        KzDeviceType.Group      => "📁",
        // Vẫn là ý nghĩa "group" (không phải thiết bị Gate vật lý) — dùng icon folder khác (mở) để
        // phân biệt trực quan với group gốc, không dùng icon cổng/barrier gây hiểu nhầm là thiết bị.
        KzDeviceType.GateGroup  => "📂",
        // Camera CHỈ có 2 trạng thái thiết bị: có kết nối / mất kết nối.
        // Tình trạng slot (còn/hết) hiển thị riêng qua SlotText + SlotLabel, KHÔNG đổi icon camera.
        KzDeviceType.Camera     => Status == KzDeviceStatus.Online ? "📷" : "🚫",
        // Sensor = zone/slot đỗ: Online = không có xe, Offline = có xe, Warning = mất kết nối.
        KzDeviceType.Sensor     => Status switch
        {
            KzDeviceStatus.Online  => "🅿",
            KzDeviceStatus.Offline => "🚗",
            KzDeviceStatus.Warning => "⚠",
            _                      => "○",
        },
        KzDeviceType.Controller => Status switch
        {
            KzDeviceStatus.Online  => "🖥",
            KzDeviceStatus.Offline => "🚫",
            KzDeviceStatus.Warning => "⚠",
            _                      => "🖥",
        },
        KzDeviceType.EntryGate  => "🚗",
        KzDeviceType.ExitGate   => "🚪",
        KzDeviceType.Door       => "🚪",
        KzDeviceType.Server     => "🖧",
        KzDeviceType.Network    => "🌐",
        _                       => "⚙",
    };

    public string SlotText => _slotTotal > 0 ? $"{_slotAvailable}/{_slotTotal}" : "";
    public bool   HasSlots => _slotTotal > 0;

    /// <summary>Tình trạng slot của camera — tách hoàn toàn khỏi <see cref="Status"/>
    /// (Status của camera chỉ mang nghĩa kết nối). Rỗng khi node không có slot.</summary>
    public string SlotLabel => _slotTotal <= 0 ? "" : _slotAvailable > 0 ? "Còn slot" : "Hết slot";

    public IBrush SlotBrush => _slotTotal <= 0
        ? new SolidColorBrush(Color.Parse("#9CA3AF"))
        : _slotAvailable == 0 ? new SolidColorBrush(Color.Parse("#EF4444"))
        : _slotAvailable == 1 ? new SolidColorBrush(Color.Parse("#F59E0B"))
        : new SolidColorBrush(Color.Parse("#22C55E"));

    public IBrush NameBrush => DeviceType == KzDeviceType.Camera && _slotTotal > 0
        ? SlotBrush
        : new SolidColorBrush(Color.Parse("#1A1A1A"));

    public IBrush DeviceIconBrush => Status == KzDeviceStatus.Warning
        ? new SolidColorBrush(Color.Parse("#F05922"))
        : new SolidColorBrush(Colors.Black);

    /// <summary>Fix 2026-08-03: đổi string → IBrush (như <see cref="SlotBrush"/>/<see cref="DeviceIconBrush"/>).
    /// Bind thẳng <c>Color="{Binding StatusColor}"</c> (string) vào <c>SolidColorBrush.Color</c>
    /// (kiểu <c>Avalonia.Media.Color</c>) không tự convert qua runtime Binding → spam lỗi
    /// "Could not convert '#EF4444' (System.String) to 'Avalonia.Media.Color'" mỗi lần Status đổi,
    /// và màu KHÔNG được set (rơi về mặc định) — badge/icon trạng thái thiết bị hiển thị sai màu.</summary>
    public IBrush StatusBrush => new SolidColorBrush(Color.Parse(StatusColorHex));

    /// <summary>Màu trạng thái theo NGỮ NGHĨA của từng loại thiết bị:
    /// Sensor (zone đỗ) Offline = "có xe" — trạng thái bình thường, dùng Cam KZTEK, không phải đỏ lỗi;
    /// Sensor Warning = "mất kết nối" — lỗi thật, dùng đỏ.
    /// Camera chỉ có xanh (có kết nối) / đỏ (mất kết nối).</summary>
    private string StatusColorHex => DeviceType == KzDeviceType.Sensor
        ? Status switch
        {
            KzDeviceStatus.Online  => "#22C55E",   // Không có xe
            KzDeviceStatus.Offline => "#F05922",   // Có xe
            KzDeviceStatus.Warning => "#EF4444",   // Mất kết nối
            _                      => "#9CA3AF",
        }
        : Status switch
        {
            KzDeviceStatus.Online  => "#22C55E",
            KzDeviceStatus.Offline => "#EF4444",
            KzDeviceStatus.Warning => "#F59E0B",
            _                      => "#9CA3AF",
        };

    /// <summary>Fix 2026-08-03: string → IBrush — cùng lý do <see cref="StatusBrush"/>.</summary>
    public IBrush StatusBadgeBrush => new SolidColorBrush(Color.Parse(
        DeviceType == KzDeviceType.Sensor
            ? Status switch
            {
                KzDeviceStatus.Online  => "#DCFCE7",   // Không có xe
                KzDeviceStatus.Offline => "#FFEDE3",   // Có xe
                KzDeviceStatus.Warning => "#FEE2E2",   // Mất kết nối
                _                      => "#F3F4F6",
            }
            : Status switch
            {
                KzDeviceStatus.Online  => "#DCFCE7",
                KzDeviceStatus.Offline => "#FEE2E2",
                KzDeviceStatus.Warning => "#FEF3C7",
                _                      => "#F3F4F6",
            }));

    public string StatusIcon => Status switch
    {
        KzDeviceStatus.Online  => "●",
        KzDeviceStatus.Offline => "●",
        KzDeviceStatus.Warning => "⚠",
        _                      => "○",
    };

    /// <summary>Nhãn trạng thái theo ngữ nghĩa từng loại thiết bị — KHÔNG dùng Online/Offline
    /// chung chung cho camera và zone đỗ (gây hiểu sai: zone "Offline" thực ra là "có xe").</summary>
    public string StatusLabel => DeviceType switch
    {
        KzDeviceType.Camera => Status == KzDeviceStatus.Online ? "Có kết nối" : "Mất kết nối",
        KzDeviceType.Sensor => Status switch
        {
            KzDeviceStatus.Online  => "Không có xe",
            KzDeviceStatus.Offline => "Có xe",
            KzDeviceStatus.Warning => "Mất kết nối",
            _                      => "Không xác định",
        },
        _ => Status switch
        {
            KzDeviceStatus.Online  => "Online",
            KzDeviceStatus.Offline => "Offline",
            KzDeviceStatus.Warning => "Cảnh báo",
            _                      => "Không xác định",
        },
    };

    public bool IsGroup => DeviceType is KzDeviceType.Group or KzDeviceType.GateGroup;
    public bool IsLeaf  => !IsGroup;

    /// <summary>Cờ cho phép hiển thị trạng thái kết nối (dot icon và badge Online/Offline).
    /// Mặc định true cho các leaf node, riêng Door tự động tắt.</summary>
    public bool ShowStatus
    {
        get => _showStatus && IsLeaf && DeviceType != KzDeviceType.Door;
        set { if (_showStatus == value) return; _showStatus = value; OnPropertyChanged(); }
    }

    /// <summary>Cờ bật badge đếm online/offline cho node group. Mặc định false để
    /// project khác đang dùng KzDeviceTree không thấy chip mới. App set true khi wire.</summary>
    public bool ShowGroupCounters
    {
        get => _showGroupCounters;
        set { _showGroupCounters = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowGroupBadges)); }
    }

    /// <summary>Số thiết bị Online tính đệ quy trong group. App tính, node giữ giá trị.</summary>
    public int GroupOnlineCount
    {
        get => _groupOnlineCount;
        set { if (_groupOnlineCount == value) return; _groupOnlineCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowGroupBadges)); }
    }

    /// <summary>Số thiết bị Offline tính đệ quy trong group. App tính, node giữ giá trị.</summary>
    public int GroupOfflineCount
    {
        get => _groupOfflineCount;
        set { if (_groupOfflineCount == value) return; _groupOfflineCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowGroupBadges)); }
    }

    /// <summary>Điều kiện IsVisible tổng hợp: chỉ hiện chip khi (a) không phải leaf,
    /// (b) cờ bật, (c) tổng > 0. Bind trực tiếp, không cần MultiBinding/Converter.</summary>
    public bool ShowGroupBadges => !IsLeaf && _showGroupCounters
                                   && (_groupOnlineCount + _groupOfflineCount) > 0;

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
