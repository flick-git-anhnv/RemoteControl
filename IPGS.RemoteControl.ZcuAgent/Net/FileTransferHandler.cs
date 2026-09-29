using System.Buffers.Binary;
using System.Text;
using IPGS.RemoteControl.Protocol;
using Microsoft.Extensions.Logging;

namespace IPGS.RemoteControl.ZcuAgent.Net;

/// <summary>
/// Truyền file hai chiều cho 1 session. Bảo mật: chỉ dùng TÊN file (Path.GetFileName), luôn nằm trong
/// <see cref="AgentOptions.TransferDir"/>; không cho đường dẫn, không ghi đè file có sẵn (tự đổi tên).
/// Upload ghi vào <c>*.part</c> rồi đổi tên khi nhận đủ; session kết thúc giữa chừng thì xoá file .part.
/// </summary>
internal sealed class FileTransferHandler : IDisposable
{
    private const int ChunkSize = 64 * 1024;

    private sealed class Upload
    {
        public required FileStream Stream { get; init; }
        public required string PartPath { get; init; }
        public required string FinalPath { get; init; }
        public required long Size { get; init; }
        public long Received;
    }

    private readonly AgentOptions _options;
    private readonly ILogger _logger;
    private readonly Func<MessageType, byte[], CancellationToken, Task> _send;
    private readonly Dictionary<byte, Upload> _uploads = new();
    private readonly string _dir;
    private byte _nextDownloadId = 0x80; // id do agent cấp cho luồng S→C (tách khỏi id client cấp)

    public FileTransferHandler(AgentOptions options, ILogger logger,
        Func<MessageType, byte[], CancellationToken, Task> send)
    {
        _options = options;
        _logger  = logger;
        _send    = send;

        var home = Environment.GetEnvironmentVariable("HOME");
        _dir = !string.IsNullOrWhiteSpace(options.TransferDir)
            ? options.TransferDir
            : Path.Combine(string.IsNullOrEmpty(home) ? Path.GetTempPath() : home, "RemoteTransfer");
    }

    public bool Enabled => _options.EnableFileTransfer;

    // ── Upload: CCU → ZCU ─────────────────────────────────────────────────

    public async Task OnBeginAsync(byte[] payload, CancellationToken ct)
    {
        if (payload.Length < 10) { await AckAsync(0, 2, "FileBegin không hợp lệ", ct); return; }
        var id   = payload[0];
        var size = (long)BinaryPrimitives.ReadUInt64BigEndian(payload.AsSpan(1));
        var name = SanitizeName(Encoding.UTF8.GetString(payload.AsSpan(9)));

        if (name is null)                     { await AckAsync(id, 2, "Tên file không hợp lệ", ct); return; }
        if (size < 0 || size > _options.MaxTransferBytes)
                                              { await AckAsync(id, 2, $"File vượt giới hạn {_options.MaxTransferBytes / 1024 / 1024} MB", ct); return; }
        if (_uploads.ContainsKey(id))         { await AckAsync(id, 2, "Đang có luồng cùng id", ct); return; }

        try
        {
            Directory.CreateDirectory(_dir);
            var finalPath = UniquePath(name);
            var partPath  = finalPath + ".part";
            var fs = new FileStream(partPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            _uploads[id] = new Upload { Stream = fs, PartPath = partPath, FinalPath = finalPath, Size = size };
            _logger.LogInformation("File upload bắt đầu: {Name} ({Size} B) → {Path}", name, size, finalPath);
            await AckAsync(id, 0, Path.GetFileName(finalPath), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không tạo được file nhận {Name}", name);
            await AckAsync(id, 2, "Không tạo được file trên máy đích: " + ex.Message, ct);
        }
    }

    public async Task OnChunkAsync(byte[] payload, CancellationToken ct)
    {
        if (payload.Length < 1) return;
        var id = payload[0];
        if (!_uploads.TryGetValue(id, out var up)) return;

        up.Received += payload.Length - 1;
        if (up.Received > up.Size || up.Received > _options.MaxTransferBytes)
        {
            Abort(id);
            await AckAsync(id, 2, "Dữ liệu nhận vượt dung lượng đã khai báo", ct);
            return;
        }
        try { await up.Stream.WriteAsync(payload.AsMemory(1), ct); }
        catch (Exception ex)
        {
            Abort(id);
            await AckAsync(id, 2, "Lỗi ghi file: " + ex.Message, ct);
        }
    }

    public async Task OnEndAsync(byte[] payload, CancellationToken ct)
    {
        if (payload.Length < 1) return;
        var id = payload[0];
        if (!_uploads.Remove(id, out var up)) return;

        try
        {
            await up.Stream.FlushAsync(ct);
            await up.Stream.DisposeAsync();
            if (up.Received != up.Size)
            {
                File.Delete(up.PartPath);
                await AckAsync(id, 2, $"Thiếu dữ liệu ({up.Received}/{up.Size} B)", ct);
                return;
            }
            File.Move(up.PartPath, up.FinalPath);
            _logger.LogInformation("File upload xong: {Path}", up.FinalPath);
            await AckAsync(id, 1, Path.GetFileName(up.FinalPath), ct);
        }
        catch (Exception ex)
        {
            try { File.Delete(up.PartPath); } catch { /* ignore */ }
            await AckAsync(id, 2, "Lỗi hoàn tất file: " + ex.Message, ct);
        }
    }

    // ── List + Download: ZCU → CCU ────────────────────────────────────────

    public async Task OnListAsync(CancellationToken ct)
    {
        var items = new List<object>();
        try
        {
            if (Directory.Exists(_dir))
                foreach (var f in new DirectoryInfo(_dir).EnumerateFiles().Where(f => !f.Name.EndsWith(".part")))
                    items.Add(new { name = f.Name, size = f.Length });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Không liệt kê được {Dir}", _dir); }

        await _send(MessageType.FileList,
            MessageCodec.EncodeStringMessage(System.Text.Json.JsonSerializer.Serialize(items)), ct);
    }

    /// <summary>Gửi file về CCU trên task nền (không chặn receive loop). Lỗi → FileAck status 2, id 0.</summary>
    public Task OnGetAsync(byte[] payload, CancellationToken ct)
    {
        var name = SanitizeName(Encoding.UTF8.GetString(payload));
        var path = name is null ? null : Path.Combine(_dir, name);
        if (path is null || !File.Exists(path))
            return AckAsync(0, 2, "Không tìm thấy file trên máy đích", ct);

        var id = _nextDownloadId++;
        if (_nextDownloadId < 0x80) _nextDownloadId = 0x80;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                var nameBytes = Encoding.UTF8.GetBytes(name!);
                var begin = new byte[9 + nameBytes.Length];
                begin[0] = id;
                BinaryPrimitives.WriteUInt64BigEndian(begin.AsSpan(1), (ulong)fs.Length);
                nameBytes.CopyTo(begin, 9);
                await _send(MessageType.FileBegin, begin, ct);

                var buf = new byte[1 + ChunkSize];
                buf[0] = id;
                int n;
                while ((n = await fs.ReadAsync(buf.AsMemory(1, ChunkSize), ct)) > 0)
                    await _send(MessageType.FileChunk, buf.AsSpan(0, 1 + n).ToArray(), ct);

                await _send(MessageType.FileEnd, new[] { id }, ct);
            }
            catch (OperationCanceledException) { /* session đóng */ }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gửi file {Name} về CCU thất bại", name);
                try { await AckAsync(id, 2, "Lỗi đọc/gửi file: " + ex.Message, ct); } catch { /* ignore */ }
            }
        }, ct);
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Task AckAsync(byte id, byte status, string message, CancellationToken ct)
    {
        var msg = Encoding.UTF8.GetBytes(message);
        var buf = new byte[2 + msg.Length];
        buf[0] = id; buf[1] = status;
        msg.CopyTo(buf, 2);
        return _send(MessageType.FileAck, buf, ct);
    }

    /// <summary>Chỉ giữ tên file; từ chối rỗng, "."/"..", ký tự cấm và tên ẩn kiểu ".part".</summary>
    internal static string? SanitizeName(string raw)
    {
        var name = Path.GetFileName(raw.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            return null;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('\0')) return null;
        return name.Length > 200 ? null : name;
    }

    private string UniquePath(string name)
    {
        var path = Path.Combine(_dir, name);
        if (!File.Exists(path) && !File.Exists(path + ".part")) return path;

        var stem = Path.GetFileNameWithoutExtension(name);
        var ext  = Path.GetExtension(name);
        for (var i = 1; ; i++)
        {
            path = Path.Combine(_dir, $"{stem} ({i}){ext}");
            if (!File.Exists(path) && !File.Exists(path + ".part")) return path;
        }
    }

    private void Abort(byte id)
    {
        if (!_uploads.Remove(id, out var up)) return;
        try { up.Stream.Dispose(); File.Delete(up.PartPath); } catch { /* ignore */ }
    }

    public void Dispose()
    {
        foreach (var id in _uploads.Keys.ToArray()) Abort(id);
    }
}
