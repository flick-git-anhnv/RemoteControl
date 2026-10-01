using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using IPGS.RemoteControl.CcuUI.ViewModels;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// UserControl that renders the incoming ZCU remote-desktop frame and
/// translates Avalonia pointer events into ZCU-space mouse commands,
/// and keyboard events into X11 keysym KEY_EVENT messages (TDD §17.5).
/// <para>
/// DataContext must be <see cref="RemoteScreenViewModel"/>; set by the parent
/// <see cref="RemoteScreenWindow"/> via inherited DataContext.
/// </para>
/// </summary>
public partial class RemoteScreenControl : UserControl
{
    public RemoteScreenControl()
    {
        InitializeComponent();

        // Register key handlers with Tunnel strategy so that Tab, Alt+F4, etc. are
        // intercepted BEFORE Avalonia's built-in focus-navigation processes them.
        // Without Tunnel, Tab would change focus between controls instead of being
        // forwarded as a keystroke to ZCU (TDD §17.5 gotcha).
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent,   OnKeyUp,   RoutingStrategies.Tunnel);

        // Release all held keys when this control loses keyboard focus to prevent
        // stuck keys on ZCU (CCU-side layer of the 2-layer protection — TDD §17).
        AddHandler(LostFocusEvent, OnLostFocus, RoutingStrategies.Bubble);
    }

    // ── Pointer event handlers ───────────────────────────────────────────────
    // Coordinate mapping (Avalonia control space → ZCU screen space) is
    // delegated to the ViewModel to keep it testable and in one place.

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not RemoteScreenViewModel vm) return;

        var pos = e.GetPosition(this);
        vm.HandleMouseMove(pos.X, pos.Y, Bounds.Width, Bounds.Height);

        // Mark handled so the move does not bubble to the local Window (drag,
        // hover-highlight on local controls, etc.) — this control owns all pointer
        // interaction inside the remote-screen area.
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Request keyboard focus here (not in an OnPointerPressed override) because
        // once e.Handled is set below, Avalonia will not invoke ancestor class
        // handlers for this routed event — focusing later would never run.
        Focus();

        if (DataContext is not RemoteScreenViewModel vm) { e.Handled = true; return; }

        var point = e.GetCurrentPoint(this);
        var kind  = point.Properties.PointerUpdateKind;
        var pos   = point.Position;

        vm.HandleMouseButton(kind, isDown: true,
            pos.X, pos.Y, Bounds.Width, Bounds.Height);

        // Without this, the click also bubbles to the local Window/parent controls —
        // the reported "both sides receive the click" bug: the CCU side reacts to the
        // click as ordinary local UI input in addition to it being forwarded to ZCU.
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not RemoteScreenViewModel vm) { e.Handled = true; return; }

        var point = e.GetCurrentPoint(this);
        var kind  = point.Properties.PointerUpdateKind;
        var pos   = point.Position;

        vm.HandleMouseButton(kind, isDown: false,
            pos.X, pos.Y, Bounds.Width, Bounds.Height);

        e.Handled = true;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is RemoteScreenViewModel vm)
        {
            var pos = e.GetPosition(this);
            vm.HandleMouseWheel(e.Delta.X, e.Delta.Y, pos.X, pos.Y, Bounds.Width, Bounds.Height);
        }

        // Chặn cuộn cục bộ của cửa sổ cha — sự kiện thuộc về màn hình từ xa.
        e.Handled = true;
    }

    // ── Keyboard event handlers (Tunnel — registered in constructor) ─────────

    // Ctrl+V: nội dung clipboard máy này phải sang ZCU TRƯỚC khi phím V tới nơi, nên giữ phím V lại, đồng bộ
    // clipboard xong mới gửi cặp nhấn+nhả V. _swallowVUp nuốt sự kiện nhả V thật của người dùng (đã gửi bù).
    private bool _swallowVUp;
    private const int ClipboardApplyDelayMs = 200;  // chờ agent đặt xong clipboard (xclip)
    private const int ClipboardPullDelayMs  = 350;  // chờ ứng dụng trên ZCU copy xong rồi mới kéo về

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not RemoteScreenViewModel vm) return;

        var ctrlOnly = e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (ctrlOnly && e.Key == Key.V && vm.IsStreaming)
        {
            e.Handled = true;
            if (!_swallowVUp) // bỏ qua auto-repeat khi giữ phím
            {
                _swallowVUp = true;
                _ = PasteAsync(vm, e.Key, e.KeySymbol, e.KeyModifiers, e.PhysicalKey);
            }
            return;
        }

        // Ctrl+C / Ctrl+X: sau khi ZCU copy xong, kéo clipboard của ZCU về máy này ngay (không đợi chu kỳ poll).
        if (ctrlOnly && (e.Key == Key.C || e.Key == Key.X) && vm.IsStreaming)
            _ = PullClipboardLaterAsync(vm);

        vm.HandleKeyDown(e.Key, e.KeySymbol, e.KeyModifiers, e.PhysicalKey);

        // Mark handled so Avalonia does not additionally process Tab for
        // focus-navigation or trigger menu mnemonics (TDD §17.5).
        e.Handled = true;
    }

    private async Task PasteAsync(RemoteScreenViewModel vm, Key key, string? symbol, KeyModifiers mods, PhysicalKey physical)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                var text = await clipboard.TryGetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    await vm.Client.SendClipboardTextAsync(text);
                    await Task.Delay(ClipboardApplyDelayMs);
                }
            }
        }
        catch { /* clipboard Windows đang bị tiến trình khác giữ — vẫn gửi phím V */ }
        finally
        {
            vm.HandleKeyDown(key, symbol, mods, physical);
            vm.HandleKeyUp(key, symbol, mods, physical);
        }
    }

    private async Task PullClipboardLaterAsync(RemoteScreenViewModel vm)
    {
        await Task.Delay(ClipboardPullDelayMs);
        await vm.Client.RequestClipboardAsync();
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not RemoteScreenViewModel vm) return;

        if (e.Key == Key.V && _swallowVUp)
        {
            _swallowVUp = false;
            e.Handled = true;
            return;
        }

        vm.HandleKeyUp(e.Key, e.KeySymbol, e.KeyModifiers, e.PhysicalKey);
        e.Handled = true;
    }

    // ── LostFocus handler ────────────────────────────────────────────────────

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        _swallowVUp = false;
        // Release all held keys so ZCU does not get stuck keys when the user
        // alt-tabs away or clicks outside the remote screen area.
        if (DataContext is RemoteScreenViewModel vm)
            vm.ReleaseAllDownKeys();
    }
}
