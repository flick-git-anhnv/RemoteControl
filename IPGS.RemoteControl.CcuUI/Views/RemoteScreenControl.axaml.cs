using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

    // ── Keyboard event handlers (Tunnel — registered in constructor) ─────────

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not RemoteScreenViewModel vm) return;

        vm.HandleKeyDown(e.Key, e.KeySymbol, e.KeyModifiers);

        // Mark handled so Avalonia does not additionally process Tab for
        // focus-navigation or trigger menu mnemonics (TDD §17.5).
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not RemoteScreenViewModel vm) return;

        vm.HandleKeyUp(e.Key, e.KeySymbol, e.KeyModifiers);
        e.Handled = true;
    }

    // ── LostFocus handler ────────────────────────────────────────────────────

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        // Release all held keys so ZCU does not get stuck keys when the user
        // alt-tabs away or clicks outside the remote screen area.
        if (DataContext is RemoteScreenViewModel vm)
            vm.ReleaseAllDownKeys();
    }
}
