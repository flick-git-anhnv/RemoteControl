using IPGS.RemoteControl.Protocol;
using IPGS.RemoteControl.ZcuAgent.Capture;

namespace IPGS.RemoteControl.ZcuAgent;

// ── Value types shared across internal interfaces ────────────────────────

/// <summary>Screen pixel dimensions returned by <see cref="IScreenCapturer"/>.</summary>
internal readonly record struct ScreenSize(int Width, int Height);

/// <summary>
/// One captured frame worth of raw pixel data (BGRA8888 from X11).
/// The byte[] is a managed copy from the XShm/XImage buffer.
/// GOTCHA (audit Q3): <see cref="PixelData"/> is a REUSED buffer owned by the capturer —
/// valid only until the next <see cref="IScreenCapturer.Capture"/> call. Consume it
/// synchronously (encode) before capturing the next frame; do NOT cache it.
/// </summary>
internal sealed class CapturedFrame
{
    public required byte[] PixelData   { get; init; }
    /// <summary>
    /// Optional unmanaged pointer to raw BGRA8888 pixels (e.g., MIT-SHM shared memory address).
    /// When non-zero, <see cref="IFrameEncoder"/> reads directly from this pointer (zero-copy)
    /// without needing <see cref="PixelData"/> to be populated.
    /// </summary>
    public IntPtr          PixelPtr    { get; init; }
    public required int    Width       { get; init; }
    public required int    Height      { get; init; }
    /// <summary>Bytes per row (may be padded).</summary>
    public required int    BytesPerRow { get; init; }
}

// ── Internal service interfaces (TDD §10.2) ──────────────────────────────

/// <summary>Captures a single frame from the X11 display.</summary>
internal interface IScreenCapturer : IDisposable
{
    ScreenSize ScreenSize { get; }

    /// <summary>
    /// Capture one frame synchronously (blocks until XShmGetImage returns).
    /// Returns <c>null</c> if the display is temporarily unavailable.
    /// </summary>
    CapturedFrame? Capture();

    /// <summary>
    /// Initialize X11 display connection. Must be called once before <see cref="Capture"/>.
    /// Throws <see cref="InvalidOperationException"/> if session is not X11.
    /// </summary>
    void Initialize();
}

/// <summary>Encodes a <see cref="CapturedFrame"/> to JPEG bytes.</summary>
internal interface IFrameEncoder
{
    /// <summary>
    /// Encode <paramref name="frame"/> to JPEG with given <paramref name="quality"/> (1–100).
    /// Returns an EMPTY memory on failure.
    /// GOTCHA (audit Q3): the returned memory is backed by a REUSED buffer owned by the
    /// encoder — valid only until the next EncodeJpeg call. Not thread-safe: call from a
    /// single capture loop only (v1 serves one session at a time).
    /// </summary>
    ReadOnlyMemory<byte> EncodeJpeg(CapturedFrame frame, int quality);
}

/// <summary>Injects synthetic mouse events via XTest (TDD §9.3).</summary>
internal interface IMouseInjector : IDisposable
{
    void Initialize();
    void Move(int x, int y);
    void Button(MouseButton button, bool isDown);
}

/// <summary>
/// Injects synthetic keyboard events via XTest (TDD §17.4).
/// Tracks pressed keys internally and can release all on disconnect.
/// </summary>
internal interface IKeyboardInjector : IDisposable
{
    void Initialize();

    /// <summary>
    /// Inject a key press or release for <paramref name="keysym"/>.
    /// If the keysym has no keycode on the current keymap, logs a warning and does nothing.
    /// </summary>
    void SendKey(uint keysym, bool isDown);

    /// <summary>Inject a physical key by X11 <paramref name="keycode"/> (scan mode) — layout-independent.</summary>
    void SendRawKey(byte keycode, bool isDown);

    /// <summary>
    /// Release every key that is currently tracked as pressed.
    /// MUST be called when a session ends (disconnect / BYE / timeout / exception)
    /// to avoid "stuck keys" on the ZCU desktop. TDD §17.4.
    /// </summary>
    void ReleaseAllKeys();
}

// ── Cầu nối nền tảng (Linux/X11 ↔ Windows) ───────────────────────────────
// Phần dùng chung (ClientSession, FileTransferHandler, TcpServer…) chỉ biết các interface dưới đây;
// ZcuAgent (Linux) và WinAgent (Windows) mỗi bên cài đặt riêng.

/// <summary>Số liệu tải máy tại một lần lấy mẫu. Giá trị &lt; 0 = không đo được (bộ điều khiển FPS bỏ qua).</summary>
internal readonly record struct MachineLoad(double XLatencyMs, double CpuPressureAvg10, long TimestampMs);

/// <summary>Lấy mẫu tải máy ở nền (~500ms/mẫu) để <see cref="Net.AdaptiveFpsController"/> điều tiết FPS.</summary>
internal interface IMachineLoadSampler : IDisposable
{
    MachineLoad Current { get; }
    void Start(CancellationToken ct);
}

/// <summary>Clipboard của máy đích: chữ và danh sách file (Copy trong trình quản lý file).</summary>
internal interface IClipboardService
{
    /// <summary>Text hiện có trong clipboard; null nếu rỗng / không phải text / lỗi.</summary>
    Task<string?> GetTextAsync(CancellationToken ct);

    Task SetTextAsync(string text, CancellationToken ct);

    /// <summary>Đường dẫn tuyệt đối các file/thư mục người dùng vừa Copy trong trình quản lý file; null nếu clipboard không chứa file.</summary>
    Task<List<string>?> GetFilePathsAsync(CancellationToken ct);

    /// <summary>Đặt các đường dẫn lên clipboard như vừa "Copy" trong trình quản lý file. false nếu thất bại.</summary>
    bool SetFiles(IReadOnlyList<string> absolutePaths);

    /// <summary>Nhả quyền sở hữu clipboard đã đặt bằng <see cref="SetFiles"/> (gọi khi session kết thúc).</summary>
    void ReleaseFiles();
}

/// <summary>Liệt kê màn hình. Toạ độ trả về tính từ góc trên-trái của khung hình mà <see cref="IScreenCapturer"/> chụp (không âm).</summary>
internal interface IMonitorProvider
{
    Task<List<MonitorInfo>> ListAsync(CancellationToken ct);
}

/// <summary>Các tác vụ gắn với hệ điều hành máy đích mà phiên làm việc cần.</summary>
internal interface IDesktopPlatformService
{
    /// <summary>Tên agent báo trong HELLO_ACK, dạng "ZcuAgent" / "WinAgent" (phiên bản do ClientSession nối thêm).</summary>
    string AgentName { get; }

    IMachineLoadSampler CreateLoadSampler();

    /// <summary>Hiện tin nhắn chat từ CCU trên màn hình máy đích.</summary>
    Task ShowChatNotificationAsync(string message, CancellationToken ct);

    /// <summary>JSON {cpu, memory, os, arch} trả về cho SysInfoResp.</summary>
    string GatherSystemInfoJson();
}
