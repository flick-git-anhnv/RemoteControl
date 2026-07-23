using System.Net;
using System.Security.Cryptography;
using System.Text;
using IPGS.RemoteControl.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IPGS.RemoteControl.ZcuAgent.Auth;

/// <summary>
/// Handles IP whitelist checking and auth rate-limiting per TDD §8.
/// <list type="bullet">
///   <item>Token comparison uses <see cref="CryptographicOperations.FixedTimeEquals"/>
///         to prevent timing attacks (TDD §8.1).</item>
///   <item>After <see cref="RemoteControlConstants.AuthFailThreshold"/> failures within
///         <see cref="RemoteControlConstants.AuthWindowSeconds"/> seconds from one IP →
///         ban for <see cref="RemoteControlConstants.AuthBanSeconds"/> (TDD §8.3).</item>
///   <item>NEVER logs the token value — only logs "AUTH_OK for &lt;ip&gt;" (TDD §14).</item>
/// </list>
/// </summary>
internal sealed class AuthManager
{
    private readonly ILogger<AuthManager> _logger;
    private readonly AgentOptions         _options;

    // State: per-IP failure tracking (in-memory, sufficient for v1)
    private readonly object                             _lock  = new();
    private readonly Dictionary<string, FailureRecord> _fails = new(StringComparer.Ordinal);

    public AuthManager(IOptions<AgentOptions> options, ILogger<AuthManager> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="remoteIp"/> is allowed per the whitelist.
    /// An empty whitelist allows all IPs.
    /// </summary>
    public bool IsIpAllowed(string remoteIp)
    {
        if (_options.AllowedClientIPs.Count == 0) return true;
        if (!IPAddress.TryParse(remoteIp, out var ip)) return false;

        foreach (var entry in _options.AllowedClientIPs)
        {
            if (IsInRange(ip, entry)) return true;
        }
        _logger.LogWarning("Connection from {IP} rejected — not in AllowedClientIPs", remoteIp);
        return false;
    }

    /// <summary>
    /// Returns true if the IP is currently rate-limit banned.
    /// </summary>
    public bool IsBanned(string remoteIp)
    {
        lock (_lock)
        {
            if (!_fails.TryGetValue(remoteIp, out var rec)) return false;
            if (rec.BannedUntil.HasValue && rec.BannedUntil.Value > DateTimeOffset.UtcNow)
                return true;
            // Expired ban — clear it
            if (rec.BannedUntil.HasValue)
                _fails.Remove(remoteIp);
            return false;
        }
    }

    /// <summary>
    /// Validate token using constant-time comparison. Records failure on mismatch.
    /// Returns (success, reason) tuple.
    /// NEVER pass token value into log messages.
    /// </summary>
    public (bool Ok, string Reason) ValidateToken(string remoteIp, string token)
    {
        if (IsBanned(remoteIp))
            return (false, "IP temporarily banned due to repeated auth failures");

        var expected = Encoding.UTF8.GetBytes(_options.Token);
        var actual   = Encoding.UTF8.GetBytes(token);

        // Constant-time compare to prevent timing attacks (TDD §8.1)
        var ok = expected.Length == actual.Length
              && CryptographicOperations.FixedTimeEquals(expected, actual);

        if (ok)
        {
            ResetFailures(remoteIp);
            _logger.LogInformation("AUTH_OK for {IP}", remoteIp);
            return (true, string.Empty);
        }

        var attempts = RecordFailure(remoteIp);
        _logger.LogWarning("AUTH_FAIL for {IP} — attempt {N}/{Max}",
            remoteIp, attempts, RemoteControlConstants.AuthFailThreshold);
        return (false, "Invalid token");
    }

    // ── Failure tracking ──────────────────────────────────────────────────

    private int RecordFailure(string ip)
    {
        lock (_lock)
        {
            if (!_fails.TryGetValue(ip, out var rec))
                rec = new FailureRecord();

            // Purge stale timestamps outside the sliding window
            var cutoff = DateTimeOffset.UtcNow.AddSeconds(-RemoteControlConstants.AuthWindowSeconds);
            rec.Timestamps.RemoveAll(t => t < cutoff);
            rec.Timestamps.Add(DateTimeOffset.UtcNow);

            if (rec.Timestamps.Count >= RemoteControlConstants.AuthFailThreshold)
            {
                rec.BannedUntil = DateTimeOffset.UtcNow.AddSeconds(RemoteControlConstants.AuthBanSeconds);
                _logger.LogWarning("IP {IP} banned for {Sec}s after {N} failures",
                    ip, RemoteControlConstants.AuthBanSeconds, rec.Timestamps.Count);
            }

            _fails[ip] = rec;
            return rec.Timestamps.Count;
        }
    }

    private void ResetFailures(string ip)
    {
        lock (_lock) { _fails.Remove(ip); }
    }

    // ── IP range check ────────────────────────────────────────────────────

    private static bool IsInRange(IPAddress ip, string cidrOrIp)
    {
        // Exact match
        if (IPAddress.TryParse(cidrOrIp, out var exact))
            return ip.Equals(exact);

        // CIDR  e.g. "192.168.1.0/24"
        var slash = cidrOrIp.IndexOf('/');
        if (slash < 0) return false;

        if (!IPAddress.TryParse(cidrOrIp[..slash], out var network)) return false;
        if (!int.TryParse(cidrOrIp[(slash + 1)..], out var prefix))  return false;

        var netBytes = network.GetAddressBytes();
        var ipBytes  = ip.GetAddressBytes();
        if (netBytes.Length != ipBytes.Length) return false;

        var fullBytes = prefix / 8;
        var remBits   = prefix % 8;

        for (var i = 0; i < fullBytes && i < netBytes.Length; i++)
            if (netBytes[i] != ipBytes[i]) return false;

        if (remBits > 0 && fullBytes < netBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - remBits));
            if ((netBytes[fullBytes] & mask) != (ipBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }

    // ── Inner types ───────────────────────────────────────────────────────

    private sealed class FailureRecord
    {
        public List<DateTimeOffset> Timestamps { get; } = [];
        public DateTimeOffset?      BannedUntil { get; set; }
    }
}
