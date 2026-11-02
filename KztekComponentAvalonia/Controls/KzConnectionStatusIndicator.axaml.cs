using System;
using Avalonia;
using Avalonia.Controls;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Icon trạng thái kết nối nhỏ gọn: chấm tròn (xanh = kết nối, xám = mất kết nối) + nhãn ngắn.
/// Dùng chung cho 4 icon trong MainHorizontalView: SQL Server, Parking, RabbitMQ, máy nuốt tiền.
/// IsConnected và Label nhận giá trị qua StyledProperty — bind trực tiếp từ MainViewModel.
/// Cập nhật visual state từ code-behind (OnPropertyChanged) — không cần binding trong AXAML nội bộ.
/// </summary>
public partial class KzConnectionStatusIndicator : UserControl
{
    // ── StyledProperties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<bool> IsConnectedProperty =
        AvaloniaProperty.Register<KzConnectionStatusIndicator, bool>(
            nameof(IsConnected), defaultValue: false);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<KzConnectionStatusIndicator, string>(
            nameof(Label), defaultValue: string.Empty);

    public static readonly StyledProperty<string> LabelViProperty =
        AvaloniaProperty.Register<KzConnectionStatusIndicator, string>(
            nameof(LabelVi), defaultValue: string.Empty);

    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    /// <summary>Dòng 1 — tên viết tắt tiếng Anh (VD: "SQL", "Parking", "RabbitMQ", "Cash").</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Dòng 2 — tên đầy đủ tiếng Việt (VD: "SQL", "Bãi đỗ xe", "Hàng đợi", "Tiền mặt").</summary>
    public string LabelVi
    {
        get => GetValue(LabelViProperty);
        set => SetValue(LabelViProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public KzConnectionStatusIndicator()
    {
        InitializeComponent();
    }

    // ── Property change handler ───────────────────────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsConnectedProperty)
        {
            UpdateDotState((bool)change.NewValue!);
        }
        else if (change.Property == LabelProperty)
        {
            var label = (string)(change.NewValue ?? string.Empty);
            if (StatusLabelEn is not null)
                StatusLabelEn.Text = label;
            UpdateTooltip();
        }
        else if (change.Property == LabelViProperty)
        {
            var labelVi = (string)(change.NewValue ?? string.Empty);
            if (StatusLabelVi is not null)
                StatusLabelVi.Text = labelVi;
            UpdateTooltip();
        }
    }

    private void UpdateTooltip()
    {
        var en = Label;
        var vi = LabelVi;
        ToolTip.SetTip(this, string.IsNullOrEmpty(vi) || vi == en ? en : $"{en} - {vi}");
    }

    // ── Visual state ──────────────────────────────────────────────────────────

    private void UpdateDotState(bool connected)
    {
        // Guard: OnPropertyChanged có thể fire trước InitializeComponent trong edge case
        if (StatusDot is null) return;

        if (connected)
        {
            StatusDot.Classes.Remove("disconnected");
            StatusDot.Classes.Add("connected");
        }
        else
        {
            StatusDot.Classes.Remove("connected");
            StatusDot.Classes.Add("disconnected");
        }
    }
}
