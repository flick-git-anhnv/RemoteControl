using IPGS.RemoteControl.Protocol;

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

    /// <summary>
    /// Release every key that is currently tracked as pressed.
    /// MUST be called when a session ends (disconnect / BYE / timeout / exception)
    /// to avoid "stuck keys" on the ZCU desktop. TDD §17.4.
    /// </summary>
    void ReleaseAllKeys();
}
