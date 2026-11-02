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
    
    // v1.2 — keyboard "scan mode": phím vật lý (X11 keycode), không phụ thuộc layout máy đích
    KeyEventRaw = 0x43,  // C→S  u8 x11Keycode + u8 down

    // v1.2 — multi-monitor
    MonitorListReq = 0x53, // C→S  yêu cầu danh sách màn hình
    MonitorList    = 0x54, // S→C  JSON [{index,name,x,y,w,h}]
    MonitorSelect  = 0x55, // C→S  u8 index; 0xFF = toàn bộ desktop

    // v1.2 — file transfer (chunked, hai chiều). Chỉ đọc/ghi trong thư mục TransferDir của agent.
    FileBegin   = 0x70, // ↔  u8 id + u64 size BE + utf8 name (tên file, không có đường dẫn)
    FileChunk   = 0x71, // ↔  u8 id + data
    FileEnd     = 0x72, // ↔  u8 id
    FileAck     = 0x73, // S→C u8 id + u8 status(0=sẵn sàng,1=hoàn tất,2=lỗi) + utf8 message
    FileListReq = 0x74, // C→S  liệt kê file trong TransferDir
    FileList    = 0x75, // S→C  JSON [{name,size}]
    FileGet     = 0x76, // C→S  utf8 name — yêu cầu agent gửi file về
    FileGetPath = 0x77, // C→S  utf8 đường dẫn tuyệt đối — chỉ chấp nhận file nằm trong danh sách ClipboardFileList agent vừa báo
    ClipboardFiles    = 0x57, // C→S  JSON ["tên file đã upload", …] — đặt các file đó lên clipboard ZCU như vừa Copy
    ClipboardFileList = 0x58, // S→C  JSON [{path,name,size}] — người dùng vừa Copy file trong trình quản lý file của ZCU

    // v2.0 — Phase 6 Enterprise Features
    ChatText    = 0x41,  // C↔S  UTF-8 text message
    PrivacyMode = 0x42,  // C→S  Boolean: true=blank screen, false=normal
    ClipboardData = 0x50, // C↔S  UTF-8 text for clipboard sync
    ClipboardGet = 0x56, // C→S  yêu cầu agent gửi ngay clipboard hiện tại của ZCU (sau Ctrl+C trong phiên remote)
    SysInfoReq  = 0x51,  // C→S  Request system info
    SysInfoResp = 0x52,  // S→C  JSON payload with system info

    // Reserved (v2):
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
    WheelLeft = 6,   // X11 button 6 — cuộn ngang
    WheelRight = 7,  // X11 button 7 — cuộn ngang
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

    /// <summary>
    /// F29: bump 15s → 30s (xác nhận thật trên ZCU 192.168.21.97, CPU Intel Atom/
    /// Celeron J3xxx yếu + MIT-SHM bị X server từ chối BadAccess): đo trực tiếp 1 lần
    /// chụp màn hình đầy đủ qua X11 (`xwd -root`, tương đương XGetImage code đang dùng)
    /// tốn 0.55-0.75s MỖI LẦN, đều đặn — máy này không đạt nổi TargetFps=15 mặc định.
    /// Khi TcpServer đang xử lý 1 session cũ có capture/cleanup bị kẹt trong lời gọi
    /// X11 đồng bộ (không thể huỷ giữa chừng), session MỚI phải chờ tới khi đó mới được
    /// accept — verify thật: idle test (KHÔNG gửi chuột/bàn phím) vẫn rớt kết nối đều
    /// đặn ở mốc PingTimeoutMs cũ = 15000ms. Tăng lên 30s cho đủ dư địa với phần cứng
    /// yếu, kèm giảm TargetFps/JpegQuality trong appsettings.json của máy đó.
    /// </summary>
    public const int  PingTimeoutMs        = 30_000;

    /// <summary>F29: cùng lý do PingTimeoutMs — bump 5s → 10s vì AUTH đôi khi bị delay
    /// do TcpServer's accept loop chờ session cũ (kẹt trong X11 cleanup) mới nhận session
    /// mới.</summary>
    public const int  HandshakeTimeoutMs   = 10_000;

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
