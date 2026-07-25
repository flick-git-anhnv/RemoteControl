using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Writes/removes a marker file while a CCU remote-control session is streaming, so that
/// other processes on the same ZCU machine (e.g. <c>IPGS.Kiosk.Avalonia</c>) can tell a
/// mouse/keyboard event was injected remotely rather than from a real touch/keyboard.
/// <para>
/// GOTCHA: the kiosk app's on-screen keyboard (<c>KzKeyboard</c>) auto-shows on
/// <c>TextBox.GotFocus</c> — it cannot distinguish a real touch from an XTest-injected
/// click, so remote-controlling a textbox also pops the on-screen keyboard over the
/// screen. The kiosk app checks for this marker before auto-showing it (see
/// iPGSv4/IPGS.Kiosk.Avalonia — <c>RemoteControlGuard</c>).
/// </para>
/// <para>
/// Uses a plain file under <c>/tmp</c> (not a Unix socket / D-Bus) because ZcuAgent and
/// the kiosk app are separate, independently-deployed processes with no shared IPC
/// channel; both run as the same OS user and <c>ipgs-remote-agent.service</c> (systemd
/// --user) does not set <c>PrivateTmp</c>, so <c>/tmp</c> is a shared filesystem view.
/// </para>
/// </summary>
internal static class RemoteSessionMarker
{
    /// <summary>
    /// Path both ZcuAgent and the kiosk app agree on. Kept as a literal (not
    /// configurable) since it is a same-machine implementation detail, not a
    /// deployment parameter.
    /// </summary>
    public const string Path = "/tmp/kztek-remote-control.active";

    public static void Create(ILogger logger)
    {
        try
        {
            File.WriteAllText(Path, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (Exception ex)
        {
            // Best-effort: if the marker can't be written, the kiosk app just behaves as
            // if no remote session were active (on-screen keyboard may show) — not fatal.
            logger.LogWarning(ex, "RemoteSessionMarker: failed to create {Path}", Path);
        }
    }

    public static void Remove(ILogger logger)
    {
        try
        {
            File.Delete(Path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RemoteSessionMarker: failed to remove {Path}", Path);
        }
    }
}
