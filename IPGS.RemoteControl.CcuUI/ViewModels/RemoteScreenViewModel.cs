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
    [ObservableProperty] private bool     _isTlsEncrypted;
    [ObservableProperty] private string?  _tlsFingerprint;
    [ObservableProperty] private bool     _isTlsVerified;
    [ObservableProperty] private string   _networkHudText = "⚡ -- FPS  |  📶 -- ms  |  📊 -- KB/s";
    [ObservableProperty] private bool     _isHudVisible;

    // ── ZCU screen dimensions — written by background thread, read on UI thread
    // (volatile gives memory-ordering guarantee for single-reader/single-writer int).
    private volatile int _screenWidth;
    private volatile int _screenHeight;

    // ── Disposal flag — set on UI thread in Dispose(), read inside queued
    // Dispatcher.UIThread.Post lambdas and on the frame-decode thread (L5 fix).
    private volatile bool _disposed;

    // ── Ping-Pong Double-Buffering & Pool (triệt tiêu 100% rác WriteableBitmap LOH) ──
    private WriteableBitmap? _pendingFrame;   // frame mới nhất đang chờ UI hiển thị
    private WriteableBitmap? _shownFrame;     // frame đang hiển thị (chỉ đụng trên UI thread)
    private WriteableBitmap? _recycledBitmap; // bitmap buffer rảnh để tái sử dụng
    private int _postQueued;
    private readonly object _bufferLock = new();

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

    // Chế độ phím vật lý (scan mode): keycode X11 đang giữ + cờ bật/tắt.
    private readonly HashSet<byte> _downRawKeys = new();
    /// <summary>true = gửi phím theo vị trí vật lý (cần agent ≥ 1.2); false = gửi theo ký tự/keysym.</summary>
    public bool UseScanMode { get; set; }

    // Ctrl/Shift đang được giữ (theo dõi cả hai chế độ gửi phím) — dùng khi dán chậm để biết có cần bấm bù.
    // keycode X11: ControlLeft=37, ControlRight=105, ShiftLeft=50, ShiftRight=62.
    public bool IsCtrlHeld  => _downKeysyms.Contains(0xFFE3) || _downKeysyms.Contains(0xFFE4) || _downRawKeys.Contains(37) || _downRawKeys.Contains(105);
    public bool IsShiftHeld => _downKeysyms.Contains(0xFFE1) || _downKeysyms.Contains(0xFFE2) || _downRawKeys.Contains(50) || _downRawKeys.Contains(62);

    // ── Constructor ──────────────────────────────────────────────────────────

    public RemoteScreenViewModel()
    {
        _client = new RemoteControlClient();
        _client.FrameReceived += OnFrameReceived;
        _client.StateChanged  += OnStateChanged;
        _client.TlsCertificateValidated += (sender, hash) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsTlsEncrypted = true;
                TlsFingerprint = hash;
                IsTlsVerified  = true;
            });
        };
        _client.NetworkMetricsUpdated += (sender, metrics) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                string bitrateStr = metrics.BitrateKbps >= 1000.0
                    ? $"{metrics.BitrateKbps / 1000.0:F1} MB/s"
                    : $"{metrics.BitrateKbps:F0} KB/s";
                NetworkHudText = $"⚡ {metrics.Fps:F0} FPS  |  📶 {metrics.RttMs:F0} ms  |  📊 {bitrateStr}";
                IsHudVisible = IsStreaming;
            });
        };
    }

    // ── Public commands called by code-behind ────────────────────────────────

    /// <summary>Starts the connection loop (fire-and-forget via CcuClient).</summary>
    public Task ConnectAsync(string host, int port, string token, string? expectedFingerprint = null)
    {
        IsFaulted        = false;
        ErrorMessage     = null;
        StatusText       = "Đang kết nối...";
        IsTlsEncrypted   = false;
        IsTlsVerified    = false;
        TlsFingerprint   = null;
        _client.ExpectedCertFingerprint = expectedFingerprint;
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

    /// <summary>
    /// Cuộn chuột: X11 mô phỏng con lăn bằng nút 4/5 (dọc) và 6/7 (ngang), mỗi "nấc" là một cặp
    /// nhấn+nhả. Delta của Avalonia: Y &gt; 0 = cuộn lên; X &gt; 0 = cuộn sang phải.
    /// Touchpad báo delta lẻ (&lt;1) nên làm tròn lên tối thiểu 1 nấc; giới hạn 10 nấc/sự kiện.
    /// </summary>
    public void HandleMouseWheel(double deltaX, double deltaY,
                                 double controlX, double controlY,
                                 double controlW,  double controlH)
    {
        if (!IsStreaming) return;

        int sw = _screenWidth, sh = _screenHeight;
        if (sw <= 0 || sh <= 0 || controlW <= 0 || controlH <= 0) return;

        MapToZcuCoords(controlX, controlY, controlW, controlH, sw, sh, out int zx, out int zy);

        SendWheel(deltaY, MouseButton.WheelUp,    MouseButton.WheelDown,  zx, zy);
        SendWheel(deltaX, MouseButton.WheelRight, MouseButton.WheelLeft,  zx, zy);
    }

    private void SendWheel(double delta, MouseButton positive, MouseButton negative, int zx, int zy)
    {
        if (Math.Abs(delta) < 0.01) return;
        var button = delta > 0 ? positive : negative;
        int notches = Math.Clamp((int)Math.Ceiling(Math.Abs(delta)), 1, 10);
        for (int i = 0; i < notches; i++)
        {
            _ = _client.SendMouseButtonAsync(button, true,  zx, zy);
            _ = _client.SendMouseButtonAsync(button, false, zx, zy);
        }
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
        Avalonia.Input.KeyModifiers modifiers = Avalonia.Input.KeyModifiers.None,
        Avalonia.Input.PhysicalKey physicalKey = Avalonia.Input.PhysicalKey.None)
    {
        if (!IsStreaming) return;

        // Chế độ phím vật lý: gửi keycode, layout/IME máy đích tự dịch ra ký tự.
        if (UseScanMode && PhysicalKeyMapper.ToX11Keycode(physicalKey) is { } rawCode)
        {
            if (_downRawKeys.Add(rawCode))
                _ = _client.SendRawKeyEventAsync(rawCode, isDown: true);
            return;
        }

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
        Avalonia.Input.KeyModifiers modifiers = Avalonia.Input.KeyModifiers.None,
        Avalonia.Input.PhysicalKey physicalKey = Avalonia.Input.PhysicalKey.None)
    {
        if (!IsStreaming) return;

        // Nhả phím nào đang giữ ở chế độ vật lý thì nhả đúng phím đó (kể cả khi vừa tắt scan mode).
        if (PhysicalKeyMapper.ToX11Keycode(physicalKey) is { } rawCode && _downRawKeys.Remove(rawCode))
        {
            _ = _client.SendRawKeyEventAsync(rawCode, isDown: false);
            return;
        }

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
        foreach (var code in _downRawKeys.ToArray())
            _ = _client.SendRawKeyEventAsync(code, isDown: false);
        _downRawKeys.Clear();

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

        var jpegSpan = e.JpegData.Span;
        if (jpegSpan.IsEmpty) return;

        // Direct Zero-Copy JPEG Decode into WriteableBitmap framebuffer via SKCodec:
        // Eliminates intermediate SKBitmap allocation, skBmp.Copy(Bgra8888) allocation,
        // and the 5.2–8.3 MB row-by-row Buffer.MemoryCopy loop on the CCU host.
        WriteableBitmap? displayBitmap = null;
        unsafe
        {
            fixed (byte* pJpeg = jpegSpan)
            {
                using var skData = SKData.Create((IntPtr)pJpeg, jpegSpan.Length);
                if (skData is null) return;
                using var codec = SKCodec.Create(skData);
                if (codec is null) return;

                int w = codec.Info.Width, h = codec.Info.Height;
                if (w <= 0 || h <= 0) return;

                var targetInfo = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);

                lock (_bufferLock)
                {
                    if (_disposed) return;

                    var candidate = System.Threading.Interlocked.Exchange(ref _recycledBitmap, null);
                    if (candidate is not null && candidate.PixelSize.Width == w && candidate.PixelSize.Height == h)
                    {
                        displayBitmap = candidate;
                    }
                    else
                    {
                        candidate?.Dispose();
                        displayBitmap = new WriteableBitmap(
                            new Avalonia.PixelSize(w, h), new Avalonia.Vector(96.0, 96.0),
                            PixelFormat.Bgra8888, AlphaFormat.Opaque);
                    }

                    using var locked = displayBitmap.Lock();
                    var result = codec.GetPixels(targetInfo, locked.Address, locked.RowBytes, SKCodecOptions.Default);
                    if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                    {
                        var discarded = System.Threading.Interlocked.Exchange(ref _recycledBitmap, displayBitmap);
                        discarded?.Dispose();
                        return;
                    }
                }
            }
        }

        if (displayBitmap is null) return;

        // Latest-wins: UI chậm thì đưa frame chờ cũ về recycled buffer thay vì hủy.
        var superseded = System.Threading.Interlocked.Exchange(ref _pendingFrame, displayBitmap);
        if (superseded is not null)
        {
            var oldRecycled = System.Threading.Interlocked.Exchange(ref _recycledBitmap, superseded);
            oldRecycled?.Dispose();
        }

        if (System.Threading.Interlocked.Exchange(ref _postQueued, 1) == 0)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                System.Threading.Interlocked.Exchange(ref _postQueued, 0);
                var next = System.Threading.Interlocked.Exchange(ref _pendingFrame, null);
                if (next is null) return;
                if (_disposed) { next.Dispose(); return; }
                var old = _shownFrame;
                _shownFrame = next;
                CurrentFrame = next;
                if (old is not null)
                {
                    var oldRecycled = System.Threading.Interlocked.Exchange(ref _recycledBitmap, old);
                    oldRecycled?.Dispose();
                }
            });
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
        lock (_bufferLock)
        {
            System.Threading.Interlocked.Exchange(ref _pendingFrame, null)?.Dispose();
            System.Threading.Interlocked.Exchange(ref _recycledBitmap, null)?.Dispose();
            CurrentFrame = null;
            _shownFrame?.Dispose();
            _shownFrame = null;
        }
    }
}
