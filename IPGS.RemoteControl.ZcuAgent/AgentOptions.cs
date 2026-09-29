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
    /// true = tự hạ FPS/chất lượng JPEG khi mạng nghẽn hoặc CCU xử lý chậm, tăng lại khi ổn định
    /// (TargetFps/JpegQuality là mức tối đa). false = giữ cố định như bản cũ.
    /// </summary>
    public bool AdaptiveQuality { get; set; } = true;

    /// <summary>JPEG quality (40–95). Default: <see cref="RemoteControlConstants.JpegQuality"/> (70).</summary>
    public int JpegQuality { get; set; } = RemoteControlConstants.JpegQuality;

    /// <summary>Maximum single-frame payload in bytes. Default: 8 MB.</summary>
    public int MaxFrameBytes { get; set; } = RemoteControlConstants.MaxFrameBytes;
}
