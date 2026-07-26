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

    /// <summary>Target capture frame rate (5–30). Default: <see cref="RemoteControlConstants.TargetFps"/> (15).</summary>
    public int TargetFps { get; set; } = RemoteControlConstants.TargetFps;

    /// <summary>JPEG quality (40–95). Default: <see cref="RemoteControlConstants.JpegQuality"/> (70).</summary>
    public int JpegQuality { get; set; } = RemoteControlConstants.JpegQuality;

    /// <summary>Maximum single-frame payload in bytes. Default: 8 MB.</summary>
    public int MaxFrameBytes { get; set; } = RemoteControlConstants.MaxFrameBytes;
}
