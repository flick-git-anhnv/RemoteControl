using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using IPGS.RemoteControl.CcuClient;
using IPGS.RemoteControl.Protocol;
using SkiaSharp;

namespace IPGS.RemoteControl.CcuUI.ViewModels;

/// <summary>
/// ViewModel for the remote screen window.
/// Manages CcuClient lifecycle, decodes incoming JPEG frames into Avalonia
/// WriteableBitmaps (on background thread), and exposes status / error info for binding.
/// </summary>
public sealed partial class RemoteScreenViewModel : ObservableObject, IDisposable
{
    // ── Client ──────────────────────────────────────────────────────────────
    private readonly RemoteControlClient _client;
    public RemoteControlClient Client => _client;

    // ── Observable properties (CommunityToolkit.Mvvm source-generated) ──────
    [ObservableProperty] private Bitmap?  _currentFrame;
    [ObservableProperty] private string   _statusText    = "Chưa kết nối";
    [ObservableProperty] private bool     _isStreaming;
    [ObservableProperty] private bool     _isFaulted;
    [ObservableProperty] private bool     _showSshHelp;
    [ObservableProperty] private string?  _errorMessage;
    /// <summary>Colour of the status dot in the window toolbar.</summary>
    [ObservableProperty] private IBrush   _statusBrush   = Brushes.Gray;

    // ── ZCU screen dimensions — written by background thread, read on UI thread
    // (volatile gives memory-ordering guarantee for single-reader/single-writer int).
    private volatile int _screenWidth;
    private volatile int _screenHeight;

    // ── Disposal flag — set on UI thread in Dispose(), read inside queued
    // Dispatcher.UIThread.Post lambdas and on the frame-decode thread (L5 fix).
    private volatile bool _disposed;

    /// <summary>
    /// Khi true, frame tới vẫn cập nhật kích thước màn hình nhưng BỎ QUA decode JPEG +
    /// cấp phát WriteableBitmap — dùng cho MultiRemoteWindow: session ở tab ẩn không
    /// cần render, tiết kiệm CPU (SkiaSharp decode full-rate × N session) và GC pressure.
    /// Volatile vì được set từ UI thread, đọc trên thread nhận TCP.
    /// </summary>
    private volatile bool _isRenderPaused;
    public bool IsRenderPaused
    {
        get => _isRenderPaused;
        set => _isRenderPaused = value;
    }

    // ── Mouse throttle state ─────────────────────────────────────────────────
    private long _lastMouseMoveSent;
    private int  _lastMouseX = -99999;
    private int  _lastMouseY = -99999;

    // ── Keyboard pressed-key tracking (TDD §17 — 2-layer stuck-key protection) ─
    // Tracks keysyms currently held down so we can send release-all on
    // LostFocus or Dispose without relying solely on ZCU-side ReleaseAllKeys().
    private readonly HashSet<uint> _downKeysyms = new();

    // ── Constructor ──────────────────────────────────────────────────────────

    public RemoteScreenViewModel()
    {
        _client = new RemoteControlClient();
        _client.FrameReceived += OnFrameReceived;
        _client.StateChanged  += OnStateChanged;
    }

    // ── Public commands called by code-behind ────────────────────────────────

    /// <summary>Starts the connection loop (fire-and-forget via CcuClient).</summary>
    public Task ConnectAsync(string host, int port, string token)
    {
        IsFaulted     = false;
        ErrorMessage  = null;
        StatusText    = "Đang kết nối...";
        return _client.ConnectAsync(host, port, token);
    }

    /// <summary>Gracefully disconnects and updates UI state.</summary>
    public Task DisconnectAsync() => _client.DisconnectAsync();

    // ── Mouse event helpers (called from RemoteScreenControl code-behind) ────

    /// <summary>
    /// Maps a pointer-move event from Avalonia control space to ZCU screen space
    /// and sends via CcuClient.  Throttled to ≤ 60 Hz, minimum delta 2 px (TDD §14).
    /// </summary>
    public void HandleMouseMove(double controlX, double controlY,
                                double controlW,  double controlH)
    {
        if (!IsStreaming) return;

        int sw = _screenWidth, sh = _screenHeight;
        if (sw <= 0 || sh <= 0 || controlW <= 0 || controlH <= 0) return;

        // Throttle: max ~60 Hz
        long now = Environment.TickCount64;
        if (now - _lastMouseMoveSent < 16) return;

        int cx = (int)controlX, cy = (int)controlY;
        if (_lastMouseX >= 0 && Math.Abs(cx - _lastMouseX) < 2 && Math.Abs(cy - _lastMouseY) < 2) return;

        _lastMouseMoveSent = now;
        _lastMouseX = cx;
        _lastMouseY = cy;

        MapToZcuCoords(controlX, controlY, controlW, controlH, sw, sh,
                       out int zx, out int zy);

        _ = _client.SendMouseMoveAsync(zx, zy);
    }

    /// <summary>
    /// Maps a pointer-press/release event from Avalonia control space to ZCU screen
    /// space and sends a MOUSE_BUTTON message via CcuClient.
    /// </summary>
    public void HandleMouseButton(Avalonia.Input.PointerUpdateKind kind, bool isDown,
                                  double controlX, double controlY,
                                  double controlW,  double controlH)
    {
        if (!IsStreaming) return;

        int sw = _screenWidth, sh = _screenHeight;
        if (sw <= 0 || sh <= 0 || controlW <= 0 || controlH <= 0) return;

        MouseButton? button = kind switch
        {
            Avalonia.Input.PointerUpdateKind.LeftButtonPressed  or
            Avalonia.Input.PointerUpdateKind.LeftButtonReleased   => MouseButton.Left,
            Avalonia.Input.PointerUpdateKind.RightButtonPressed or
            Avalonia.Input.PointerUpdateKind.RightButtonReleased  => MouseButton.Right,
            Avalonia.Input.PointerUpdateKind.MiddleButtonPressed or
            Avalonia.Input.PointerUpdateKind.MiddleButtonReleased => MouseButton.Middle,
            _ => null
        };

        if (button is null) return;

        MapToZcuCoords(controlX, controlY, controlW, controlH, sw, sh,
                       out int zx, out int zy);

        _ = _client.SendMouseButtonAsync(button.Value, isDown, zx, zy);
    }

    // ── Keyboard event helpers (called from RemoteScreenControl code-behind) ──

    /// <summary>
    /// Processes a key-press event. Deduplicates (ignores OS auto-repeat when the
    /// keysym is already tracked as held down) and sends KEY_EVENT press via CcuClient.
    /// </summary>
    /// <param name="key">Avalonia logical key.</param>
    /// <param name="keySymbol">
    /// Avalonia KeySymbol (single Unicode char after Shift/AltGr modifiers applied).
    /// May be null for modifier keys.
    /// </param>
    /// <param name="modifiers">
    /// Modifier keys held during this event. Required so <see cref="KeyboardMapper"/>
    /// can detect Ctrl+letter shortcuts (Ctrl+C/Ctrl+V/...) and avoid misreading the
    /// Windows-reported ASCII control code as a literal keysym.
    /// </param>
    public void HandleKeyDown(Avalonia.Input.Key key, string? keySymbol,
        Avalonia.Input.KeyModifiers modifiers = Avalonia.Input.KeyModifiers.None)
    {
        if (!IsStreaming) return;

        uint? keysym = KeyboardMapper.Resolve(key, keySymbol, modifiers);
        if (keysym is null) return;

        // Deduplicate: skip if already tracked as pressed (OS auto-repeat, TDD §17.8)
        if (!_downKeysyms.Add(keysym.Value)) return;

        _ = _client.SendKeyEventAsync(keysym.Value, isDown: true);
    }

    /// <summary>
    /// Processes a key-release event. Removes the keysym from the held-down set
    /// and sends KEY_EVENT release via CcuClient.
    /// </summary>
    public void HandleKeyUp(Avalonia.Input.Key key, string? keySymbol,
        Avalonia.Input.KeyModifiers modifiers = Avalonia.Input.KeyModifiers.None)
    {
        if (!IsStreaming) return;

        uint? keysym = KeyboardMapper.Resolve(key, keySymbol, modifiers);
        if (keysym is null) return;

        _downKeysyms.Remove(keysym.Value);
        _ = _client.SendKeyEventAsync(keysym.Value, isDown: false);
    }

    /// <summary>
    /// Sends KEY_EVENT release for every keysym currently tracked as held down,
    /// then clears the tracking set. Call on LostFocus or before Disconnect to
    /// prevent stuck keys on ZCU (CCU-side of the 2-layer protection — TDD §17).
    /// </summary>
    public void ReleaseAllDownKeys()
    {
        if (_downKeysyms.Count == 0) return;

        // Snapshot to avoid mutation-while-iterating issues.
        var toRelease = _downKeysyms.ToArray();
        _downKeysyms.Clear();

        foreach (uint keysym in toRelease)
            _ = _client.SendKeyEventAsync(keysym, isDown: false);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Maps control-space coordinates to ZCU screen space, accounting for
    /// Stretch.Uniform letterboxing so clicks outside the image area are clamped.
    /// </summary>
    private static void MapToZcuCoords(
        double controlX, double controlY,
        double controlW,  double controlH,
        int screenW,      int screenH,
        out int zx,       out int zy)
    {
        // Compute rendered image dimensions inside control (Stretch.Uniform)
        double controlAspect = controlW / controlH;
        double imageAspect   = (double)screenW / screenH;

        double imageW, imageH;
        if (controlAspect > imageAspect)
        {
            imageH = controlH;
            imageW = controlH * imageAspect;
        }
        else
        {
            imageW = controlW;
            imageH = controlW / imageAspect;
        }

        double offsetX = (controlW - imageW) / 2.0;
        double offsetY = (controlH - imageH) / 2.0;

        // Map into image space; clamp to valid range
        double relX = Math.Clamp(controlX - offsetX, 0, imageW - 1);
        double relY = Math.Clamp(controlY - offsetY, 0, imageH - 1);

        zx = (int)(relX / imageW * screenW);
        zy = (int)(relY / imageH * screenH);
    }

    // ── CcuClient event handlers ─────────────────────────────────────────────

    private void OnStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsStreaming = e.Current == ConnectionState.Streaming;
            IsFaulted   = e.Current == ConnectionState.Faulted;

            StatusText = e.Current switch
            {
                ConnectionState.Connecting      => "Đang kết nối...",
                ConnectionState.Authenticating  => "Đang xác thực...",
                ConnectionState.Streaming       => "Đã kết nối — Đang xem màn hình ZCU",
                ConnectionState.Faulted         => "Lỗi kết nối",
                ConnectionState.Disconnected    => "Đã ngắt kết nối",
                _                               => e.Current.ToString()
            };

            StatusBrush = e.Current switch
            {
                ConnectionState.Streaming                                     => Brushes.LimeGreen,
                ConnectionState.Connecting or ConnectionState.Authenticating  => Brushes.Orange,
                ConnectionState.Faulted                                       => Brushes.OrangeRed,
                _                                                             => Brushes.Gray
            };

            if (e.Current == ConnectionState.Faulted && e.Reason is not null)
            {
                ErrorMessage = e.Reason.Contains("AUTH_FAIL", StringComparison.OrdinalIgnoreCase)
                    ? "Token không hợp lệ. Kiểm tra lại cấu hình token trong appsettings."
                    : e.Reason.Contains("Max reconnect", StringComparison.OrdinalIgnoreCase)
                        ? "Không thể kết nối sau nhiều lần thử. Kiểm tra địa chỉ và kết nối mạng."
                        : $"Lỗi: {e.Reason}";
            }
            else if (e.Current != ConnectionState.Faulted)
            {
                ErrorMessage = null;
            }
        });
    }

    private void OnFrameReceived(object? sender, FrameReceivedEventArgs e)
    {
        // Capture screen dimensions from every frame (set by ZcuAgent in FRAME_JPEG payload)
        _screenWidth  = e.Width;
        _screenHeight = e.Height;

        // Q19: session ở tab ẩn (MultiRemoteWindow) không cần render — bỏ qua decode
        // để tiết kiệm CPU/RAM; kích thước màn hình phía trên vẫn được cập nhật.
        // L5: sau Dispose cũng không decode thêm (event đã unsubscribe nhưng frame
        // đang bay trên receive-thread vẫn có thể gọi vào đây một nhịp cuối).
        if (_isRenderPaused || _disposed) return;

        // Decode JPEG on the calling (background) thread — SkiaSharp is thread-safe.
        using var skBmp = SKBitmap.Decode(e.JpegData.Span);
        if (skBmp is null) return;

        // Ensure BGRA8888 to match Avalonia WriteableBitmap's pixel format.
        SKBitmap toRender;
        bool mustDispose = false;

        if (skBmp.ColorType == SKColorType.Bgra8888)
        {
            toRender = skBmp;
        }
        else
        {
            var converted = skBmp.Copy(SKColorType.Bgra8888);
            if (converted is null) return;
            toRender   = converted;
            mustDispose = true;
        }

        try
        {
            int w = toRender.Width, h = toRender.Height;

            // Allocate a fresh WriteableBitmap for each frame.
            // At 15 fps + 1080p this is acceptable for v1; double-buffer optimisation
            // can be added as v1.1 when profiling confirms GC pressure.
            var wb = new WriteableBitmap(
                new Avalonia.PixelSize(w, h),
                new Avalonia.Vector(96.0, 96.0),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);   // JPEG has no alpha channel

            // Copy pixels: SKBitmap unmanaged → byte[] → WriteableBitmap (2-copy,
            // avoids unsafe blocks; acceptable latency at target FPS).
            IntPtr pixPtr  = toRender.GetPixels();
            byte[] pixData = new byte[toRender.ByteCount];
            Marshal.Copy(pixPtr, pixData, 0, pixData.Length);

            using (var locked = wb.Lock())
                Marshal.Copy(pixData, 0, locked.Address, pixData.Length);

            // Assign CurrentFrame on the UI thread to trigger binding update,
            // then dispose the previous bitmap to release its native pixel buffer.
            // Without this, WriteableBitmap accumulates ~8 MB per frame (1080p BGRA)
            // and only gets reclaimed when the GC decides to run — memory pressure
            // becomes visible within seconds at 10–15 fps.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // L5: nếu Dispose() đã chạy trước khi lambda này ra khỏi queue thì
                // KHÔNG gán CurrentFrame — VM đã chết, không ai dispose bitmap này nữa
                // (leak ~8MB/lần đóng ở 1080p; MultiRemote 3×3 "Close All" = 9 bitmap).
                // Dispose thẳng wb tại đây thay vì gán.
                if (_disposed)
                {
                    wb.Dispose();
                    return;
                }
                var old = CurrentFrame;
                CurrentFrame = wb;
                old?.Dispose();
            });
        }
        finally
        {
            if (mustDispose) toRender.Dispose();
        }
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        // Set cờ TRƯỚC mọi thứ khác: các lambda Dispatcher.Post đã nằm sẵn trong queue
        // sẽ thấy _disposed = true và tự dispose bitmap của chúng (L5).
        _disposed = true;
        _client.FrameReceived -= OnFrameReceived;
        _client.StateChanged  -= OnStateChanged;
        // Release any stuck keys on ZCU BEFORE disconnect so the release messages
        // still travel over the live connection (CCU-side layer 1 of 2-layer protection).
        ReleaseAllDownKeys();
        // Best-effort disconnect; ignore exceptions during teardown.
        try { _ = _client.DisconnectAsync(); } catch { /* ignored */ }
        _client.Dispose();
        // Release the last WriteableBitmap's native buffer.
        CurrentFrame?.Dispose();
        CurrentFrame = null;
    }
}
