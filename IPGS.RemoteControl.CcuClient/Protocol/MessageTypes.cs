namespace IPGS.RemoteControl.Protocol;

/// <summary>
/// Wire-level message type byte values. See TDD §5.2.
/// </summary>
public enum MessageType : byte
{
    Hello       = 0x01,  // C→S  handshake
    HelloAck    = 0x02,  // S→C  server accepts version + reports screen size
    Auth        = 0x03,  // C→S  shared secret
    AuthOk      = 0x04,  // S→C  auth accepted
    AuthFail    = 0x05,  // S→C  auth rejected → server closes immediately

    FrameJpeg   = 0x10,  // S→C  JPEG-encoded screen frame

    MouseMove   = 0x20,  // C→S  pointer move (ZCU screen coordinates)
    MouseButton = 0x21,  // C→S  button press / release

    Ping        = 0x30,  // ↔    keepalive
    Pong        = 0x31,  // ↔    keepalive reply

    Bye         = 0x7F,  // ↔    graceful close

    // v1.1 — keyboard
    KeyEvent    = 0x40,  // C→S  key press / release (u32 keysym BE + u8 down)

    // Reserved (v2):
    // 0x41-0x4F  keyboard (Unicode string paste, …)
    // 0x50-0x5F  clipboard
    // 0x60-0x6F  H.264/video
}

/// <summary>
/// Mouse buttons used in MOUSE_BUTTON messages and in <see cref="IRemoteControlClient"/>.
/// Byte values match the wire protocol (TDD §5.2 MOUSE_BUTTON payload).
/// </summary>
public enum MouseButton : byte
{
    Left      = 1,
    Middle    = 2,
    Right     = 3,
    WheelUp   = 4,
    WheelDown = 5,
}

/// <summary>
/// Shared protocol constants. Values are the defaults; all configurable parameters
/// are also exposed via <c>RemoteControl</c> section in appsettings.json (ZcuAgent)
/// and Avalonia settings (CcuUI). See TDD §11.
/// </summary>
public static class RemoteControlConstants
{
    public const int  DefaultPort          = 17600;
    public const byte ProtocolVersion      = 1;

    /// <summary>Maximum accepted payload size. Server/client MUST close if exceeded.</summary>
    public const int  MaxFrameBytes        = 8 * 1024 * 1024;  // 8 MB

    public const int  TargetFps            = 15;
    public const int  JpegQuality          = 70;

    public const int  PingIntervalMs       = 5_000;
    public const int  PingTimeoutMs        = 15_000;
    public const int  HandshakeTimeoutMs   = 5_000;

    public const int  ReconnectDelayMs     = 3_000;
    public const int  ReconnectJitterMs    = 1_000;
    public const int  MaxReconnectAttempts = 10;

    /// <summary>Number of AUTH failures from a single IP before temporary ban.</summary>
    public const int  AuthFailThreshold    = 3;
    /// <summary>Sliding window (seconds) for counting AUTH failures.</summary>
    public const int  AuthWindowSeconds    = 60;
    /// <summary>Duration (seconds) an IP is banned after hitting the threshold.</summary>
    public const int  AuthBanSeconds       = 300;  // 5 minutes
}
