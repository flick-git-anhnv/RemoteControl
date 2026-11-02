using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

public enum KzMessageBoxKind { Info, Success, Warning, Error, Question }

public enum KzMessageBoxButtons { Ok, OkCancel, YesNo, YesNoCancel }

public enum KzMessageBoxResult { None, Ok, Cancel, Yes, No }

/// <summary>
/// KZTEK message box — thay thế MsBox.Avalonia (3.0.0-rc2 KHÔNG tương thích Avalonia 12 —
/// lesson msbox-avalonia-3rc2-incompatible-avalonia-12). Build window thuần Avalonia + Kz token,
/// cross-platform (#3).
/// Dùng: var r = await KzMessageBox.ShowAsync(ownerWindow, "Xác nhận", "Lưu thay đổi?",
///                    KzMessageBoxButtons.YesNo, KzMessageBoxKind.Question);
/// owner null → mở window độc lập (non-modal ShowDialog không có owner → Show + chờ Closed).
/// </summary>
public static class KzMessageBox
{
    public static async Task<KzMessageBoxResult> ShowAsync(
        Window? owner,
        string title,
        string message,
        KzMessageBoxButtons buttons = KzMessageBoxButtons.Ok,
        KzMessageBoxKind kind = KzMessageBoxKind.Info)
    {
        var result = KzMessageBoxResult.None;

        var (accent, icon) = kind switch
        {
            KzMessageBoxKind.Success  => (KzTokens.SuccessBrush, "✓"),
            KzMessageBoxKind.Warning  => (KzTokens.WarningBrush, "⚠"),
            KzMessageBoxKind.Error    => (KzTokens.ErrorBrush,   "✕"),
            KzMessageBoxKind.Question => (KzTokens.Navy700Brush, "?"),
            _                         => (KzTokens.InfoBrush,    "ℹ")
        };

        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = owner is null,
            Background = KzTokens.BgDefaultBrush,
            MinWidth = 340,
            MaxWidth = 560
        };

        // ── Icon tròn + message ──
        var iconBorder = new Border
        {
            Width = 40, Height = 40,
            CornerRadius = KzTokens.RadiusPill,
            Background = new SolidColorBrush(((SolidColorBrush)accent).Color, 0.12),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = icon,
                Foreground = accent,
                FontSize = KzTokens.FontSizeXl,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var textPanel = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = KzTokens.FontSizeLg,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = KzTokens.TextBrush,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = message,
                    FontSize = KzTokens.FontSizeMd,
                    Foreground = KzTokens.TextMutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 420
                }
            }
        };

        var bodyPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Children = { iconBorder, textPanel }
        };

        // ── Nút theo buttons ──
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        void AddButton(string text, KzMessageBoxResult r, bool primary)
        {
            var btn = new KzButton
            {
                Content = text,
                KzSize = KzSize.Sm,
                MinWidth = 88,
                Variant = primary ? KzButtonVariant.Primary : KzButtonVariant.Secondary
            };
            btn.Click += (_, _) => { result = r; window.Close(); };
            buttonPanel.Children.Add(btn);
            if (primary) btn.AttachedToVisualTree += (_, _) => btn.Focus();
        }

        switch (buttons)
        {
            case KzMessageBoxButtons.OkCancel:
                AddButton("Hủy", KzMessageBoxResult.Cancel, primary: false);
                AddButton("OK", KzMessageBoxResult.Ok, primary: true);
                break;
            case KzMessageBoxButtons.YesNo:
                AddButton("Không", KzMessageBoxResult.No, primary: false);
                AddButton("Có", KzMessageBoxResult.Yes, primary: true);
                break;
            case KzMessageBoxButtons.YesNoCancel:
                AddButton("Hủy", KzMessageBoxResult.Cancel, primary: false);
                AddButton("Không", KzMessageBoxResult.No, primary: false);
                AddButton("Có", KzMessageBoxResult.Yes, primary: true);
                break;
            default:
                AddButton("OK", KzMessageBoxResult.Ok, primary: true);
                break;
        }

        window.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 18,
            Children = { bodyPanel, buttonPanel }
        };

        // Esc = Cancel/None (parity hành vi dialog chuẩn)
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape) window.Close();
        };

        if (owner is not null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            var tcs = new TaskCompletionSource();
            window.Closed += (_, _) => tcs.TrySetResult();
            window.Show();
            await tcs.Task;
        }

        return result;
    }
}
