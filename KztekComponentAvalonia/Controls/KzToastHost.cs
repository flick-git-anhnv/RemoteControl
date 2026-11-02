using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

/// <summary>Loại toast — quyết định màu + icon.</summary>
public enum KzToastKind { Success, Error, Warning, Info }

/// <summary>
/// KZTEK toast host — overlay góc phải-dưới hiển thị toast notification tự tắt.
/// Cách dùng:
///   1. Đặt &lt;kz:KzToastHost /&gt; làm CON CUỐI CÙNG của Panel gốc trong Window
///      (Grid/Panel — để toast nổi trên mọi nội dung).
///   2. Gọi từ bất kỳ đâu trên UI thread:
///      KzToastHost.Show("Đã lưu cấu hình", KzToastKind.Success);
///      — static Show() route về host đăng ký gần nhất còn attach (window đang mở).
///      Hoặc giữ reference host và gọi instance host.ShowToast(...).
/// </summary>
public class KzToastHost : Border
{
    private static readonly List<WeakReference<KzToastHost>> Hosts = new();

    private readonly StackPanel _stack;

    public KzToastHost()
    {
        // Host trong suốt, không bắt chuột ngoài vùng toast, neo phải-dưới.
        Background = Brushes.Transparent;
        IsHitTestVisible = true;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(0, 0, 16, 16);
        _stack = new StackPanel { Spacing = 8 };
        Child = _stack;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        lock (Hosts) Hosts.Add(new WeakReference<KzToastHost>(this));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        lock (Hosts) Hosts.RemoveAll(w => !w.TryGetTarget(out var h) || ReferenceEquals(h, this));
    }

    /// <summary>
    /// Hiện toast trên host còn attach đăng ký GẦN NHẤT (thường là window đang mở).
    /// An toàn gọi khi chưa có host nào — no-op (không throw).
    /// </summary>
    public static void Show(string message, KzToastKind kind = KzToastKind.Info, TimeSpan? duration = null)
    {
        KzToastHost? target = null;
        lock (Hosts)
        {
            for (int i = Hosts.Count - 1; i >= 0; i--)
            {
                if (Hosts[i].TryGetTarget(out var h) && h.VisualRoot is not null)
                {
                    target = h;
                    break;
                }
            }
        }
        target?.ShowToast(message, kind, duration);
    }

    /// <summary>Hiện toast trên chính host này. Tự tắt sau <paramref name="duration"/> (mặc định 3s).</summary>
    public void ShowToast(string message, KzToastKind kind = KzToastKind.Info, TimeSpan? duration = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowToast(message, kind, duration));
            return;
        }

        var item = BuildToastItem(message, kind);
        _stack.Children.Add(item);

        var timer = new DispatcherTimer { Interval = duration ?? TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _stack.Children.Remove(item);
        };
        timer.Start();
    }

    private Control BuildToastItem(string message, KzToastKind kind)
    {
        var (accent, icon) = kind switch
        {
            KzToastKind.Success => (KzTokens.SuccessBrush, "✓"), // ✓
            KzToastKind.Error   => (KzTokens.ErrorBrush,   "✕"), // ✕
            KzToastKind.Warning => (KzTokens.WarningBrush, "⚠"), // ⚠
            _                   => (KzTokens.InfoBrush,    "ℹ")  // ℹ
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = icon,
            Foreground = accent,
            FontSize = KzTokens.FontSizeMd,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = KzTokens.TextBrush,
            FontSize = KzTokens.FontSizeMd,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
            VerticalAlignment = VerticalAlignment.Center
        });

        var border = new Border
        {
            Background = KzTokens.BgDefaultBrush,
            BorderBrush = KzTokens.BorderBrush,
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = KzTokens.RadiusMd,
            Padding = new Thickness(14, 10),
            MinWidth = 220,
            // Elevation e2 (dialog/toast) — BoxShadow cross-platform (#3, không System.Drawing)
            BoxShadow = BoxShadows.Parse("0 8 24 0 #1E251C53"),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("4,10,*"),
                Children =
                {
                    new Border
                    {
                        Background = accent,
                        CornerRadius = KzTokens.RadiusPill,
                        Width = 4,
                        [Grid.ColumnProperty] = 0
                    },
                    WithColumn(panel, 2)
                }
            }
        };
        return border;
    }

    private static Control WithColumn(Control c, int col)
    {
        Grid.SetColumn(c, col);
        return c;
    }
}
