using IPGS.RemoteControl.Protocol;

namespace IPGS.RemoteControl.ZcuAgent;

/// <summary>
/// Bound from <c>appsettings.json</c> section <c>"RemoteControl"</c>. See TDD §12.
/// </summary>
public sealed class AgentOptions
{
    public const string Section = "RemoteControl";

    /// <summary>TCP listen port. Default: <see cref="RemoteControlConstants.DefaultPort"/> (17600).</summary>
    public int Port { get; set; } = RemoteControlConstants.DefaultPort;

    /// <summary>
    /// Chỉ định X11 Display cụ thể (ví dụ: ":0", ":1", ":10.0").
    /// Mặc định rỗng = tự động dò tìm qua <see cref="Interop.X11DisplayResolver"/> (Unix socket /tmp/.X11-unix hoặc biến DISPLAY).
    /// </summary>
    public string Display { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret token for authentication (TDD §8.1).
    /// MUST be set to a strong random value in production. NEVER log this value.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Allowed client IP addresses or CIDR ranges (TDD §8.2).
    /// Example: <c>["192.168.1.10", "192.168.1.0/24"]</c>.
    /// Empty list = DENY ALL (deny-by-default, security audit S4). A catch-all
    /// range like <c>0.0.0.0/0</c> is accepted but logged prominently at startup.
    /// </summary>
    public List<string> AllowedClientIPs { get; set; } = [];

    /// <summary>
    /// Enables desktop integration features driven by the remote CCU client:
    /// chat notifications (<c>notify-send</c>) and clipboard sync (<c>xclip</c>).
    /// Set to <c>false</c> to disable spawning external processes from client
    /// input entirely (security audit S2). Default: true (behavior parity).
    /// </summary>
    public bool EnableDesktopIntegration { get; set; } = true;

    /// <summary>
    /// true = chỉ nhận kết nối TLS, từ chối client cũ dùng TCP thường. Mặc định false để
    /// tương thích client đời cũ; khi mọi CCU đã cập nhật nên bật true.
    /// </summary>
    public bool RequireTls { get; set; } = false;

    /// <summary>Đường dẫn file PFX chứng chỉ TLS tự ký. Rỗng = <c>agent-tls.pfx</c> cạnh agent.</summary>
    public string TlsCertPath { get; set; } = string.Empty;

    /// <summary>Cho phép truyền file qua giao thức remote (chỉ trong <see cref="TransferDir"/>).</summary>
    public bool EnableFileTransfer { get; set; } = true;

    /// <summary>Thư mục nhận/gửi file. Rỗng = <c>~/RemoteTransfer</c> của user chạy agent.</summary>
    public string TransferDir { get; set; } = string.Empty;

    /// <summary>Dung lượng tối đa mỗi file (byte). Mặc định 1 GB.</summary>
    public long MaxTransferBytes { get; set; } = 1L * 1024 * 1024 * 1024;

    /// <summary>Target capture frame rate (5–30). Default: <see cref="RemoteControlConstants.TargetFps"/> (15).</summary>
    public int TargetFps { get; set; } = RemoteControlConstants.TargetFps;

    /// <summary>
    /// true = tự thích ứng FPS theo tải thực tế của máy (X-Latency, CPU Pressure, Network backpressure).
    /// Sàn tối thiểu là <see cref="MinFps"/> (mặc định 15). false = không tự điều chỉnh theo tải máy.
    /// </summary>
    public bool CpuAdaptive { get; set; } = true;

    /// <summary>
    /// Sàn FPS tối thiểu khi tính năng thích ứng tải máy hoạt động. Mặc định 15 (cố định theo yêu cầu người dùng).
    /// </summary>
    public int MinFps { get; set; } = 15;

    /// <summary>
    /// true = tự hạ FPS/chất lượng JPEG khi mạng nghẽn hoặc CCU xử lý chậm, tăng lại khi ổn định
    /// (TargetFps/JpegQuality là mức tối đa). false = giữ cố định như bản cũ.
    /// </summary>
    public bool AdaptiveQuality { get; set; } = true;

    /// <summary>
    /// true = khung hình giống hệt khung trước thì KHÔNG nén/gửi (màn hình tĩnh gần như không tốn CPU);
    /// vẫn gửi 1 khung làm mới mỗi ~2s. false = luôn nén + gửi mọi khung như bản cũ.
    /// </summary>
    public bool SkipUnchangedFrames { get; set; } = true;

    /// <summary>
    /// Chu kỳ (giây) ghi log thống kê hiệu năng stream: FPS gửi/bỏ qua, thời gian capture / so sánh /
    /// nén / ghi (trung bình + p95), KB/frame. 0 = tắt.
    /// </summary>
    public int PerfLogIntervalSec { get; set; } = 10;

    /// <summary>JPEG quality (40–95). Default: <see cref="RemoteControlConstants.JpegQuality"/> (70).</summary>
    public int JpegQuality { get; set; } = RemoteControlConstants.JpegQuality;

    /// <summary>Maximum single-frame payload in bytes. Default: 8 MB.</summary>
    public int MaxFrameBytes { get; set; } = RemoteControlConstants.MaxFrameBytes;

    /// <summary>
    /// true = tự động nhận diện khi máy ZCU không cắm màn hình vật lý (headless / không có EDID)
    /// để nâng độ phân giải ảo lên <see cref="HeadlessTargetResolution"/> (mặc định 1920x1080);
    /// khi cắm màn hình thật vào (có EDID), tự động trả về độ phân giải gốc của màn hình thật.
    /// </summary>
    public bool AutoHeadlessResolution { get; set; } = true;

    /// <summary>
    /// Độ phân giải mục tiêu khi máy ZCU chạy ở chế độ không cắm màn hình vật lý. Mặc định "1920x1080".
    /// </summary>
    public string HeadlessTargetResolution { get; set; } = "1920x1080";
}
