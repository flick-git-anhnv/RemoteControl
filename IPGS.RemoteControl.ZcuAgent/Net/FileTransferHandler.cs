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
    private readonly HashSet<string> _advertised = new();
    private Interop.X11ClipboardOwner? _owner;
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

        if (options.EnableFileTransfer) CleanOldPasteDirs();
    }

    public bool Enabled => _options.EnableFileTransfer;

    // ── Upload: CCU → ZCU ─────────────────────────────────────────────────

    public async Task OnBeginAsync(byte[] payload, CancellationToken ct)
    {
        if (payload.Length < 10) { await AckAsync(0, 2, "FileBegin không hợp lệ", ct); return; }
        var id   = payload[0];
        var size = (long)BinaryPrimitives.ReadUInt64BigEndian(payload.AsSpan(1));
        var rawName = Encoding.UTF8.GetString(payload.AsSpan(9));
        // Tiền tố 0001 = client đã được người dùng đồng ý GHI ĐÈ file trùng tên (hộp thoại "📁 File").
        var overwrite = rawName.StartsWith('\u0001');
        if (overwrite) rawName = rawName[1..];
        var isDir   = rawName.EndsWith('/') || rawName.EndsWith('\\');
        var segments = SanitizePath(rawName);

        if (segments is null)                 { await AckAsync(id, 2, "Tên file không hợp lệ", ct); return; }
        if (size < 0 || size > _options.MaxTransferBytes)
                                              { await AckAsync(id, 2, $"File vượt giới hạn {_options.MaxTransferBytes / 1024 / 1024} MB", ct); return; }
        if (_uploads.ContainsKey(id))         { await AckAsync(id, 2, "Đang có luồng cùng id", ct); return; }

        try
        {
            Directory.CreateDirectory(_dir);
            var target = Path.Combine(_dir, Path.Combine(segments.ToArray()));

            // Thư mục (kể cả thư mục rỗng): chỉ tạo, báo xong ngay — không có chunk/end.
            if (isDir)
            {
                Directory.CreateDirectory(target);
                await AckAsync(id, 1, string.Join('/', segments), ct);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            // File lẻ ở gốc (hộp thoại "📁 File") → tự đổi tên nếu trùng, không ghi đè. File thuộc một lần dán
            // (nằm trong thư mục .paste-xxxx) giữ nguyên tên gốc để trình quản lý file tự hỏi ghi đè khi dán.
            var finalPath = segments.Count == 1 && !overwrite ? UniquePath(segments[0]) : target;
            var partPath  = finalPath + ".part";
            var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            _uploads[id] = new Upload { Stream = fs, PartPath = partPath, FinalPath = finalPath, Size = size };
            _logger.LogInformation("File upload bắt đầu: {Name} ({Size} B) → {Path}", rawName, size, finalPath);
            await AckAsync(id, 0, RelativeToRoot(finalPath), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không tạo được file nhận {Name}", rawName);
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
            File.Move(up.PartPath, up.FinalPath, overwrite: true);
            _logger.LogInformation("File upload xong: {Path}", up.FinalPath);
            await AckAsync(id, 1, RelativeToRoot(up.FinalPath), ct);
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

    /// <summary>Gửi file (theo tên trong thư mục nhận) về CCU trên task nền. Lỗi → FileAck status 2, id 0.</summary>
    public Task OnGetAsync(byte[] payload, CancellationToken ct)
    {
        var name = SanitizeName(Encoding.UTF8.GetString(payload));
        var path = name is null ? null : Path.Combine(_dir, name);
        if (path is null || !File.Exists(path))
            return AckAsync(0, 2, "Không tìm thấy file trên máy đích", ct);

        StreamFileInBackground(path, name!, ct);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gửi file theo đường dẫn tuyệt đối — CHỈ khi đường dẫn nằm trong danh sách vừa được báo qua
    /// <see cref="MessageType.ClipboardFileList"/> (file người dùng vừa Copy trên ZCU). Không đọc file tuỳ ý.
    /// </summary>
    public Task OnGetPathAsync(byte[] payload, CancellationToken ct)
    {
        var path = Encoding.UTF8.GetString(payload);
        bool allowed;
        lock (_advertised) allowed = _advertised.Contains(path);
        if (!allowed || !File.Exists(path))
            return AckAsync(0, 2, "File không nằm trong danh sách vừa Copy trên máy đích", ct);

        StreamFileInBackground(path, Path.GetFileName(path), ct);
        return Task.CompletedTask;
    }

    /// <summary>Ghi nhận danh sách đường dẫn được phép tải qua <see cref="OnGetPathAsync"/> (thay thế danh sách cũ).</summary>
    public void SetAdvertised(IEnumerable<string> paths)
    {
        lock (_advertised) { _advertised.Clear(); foreach (var p in paths) _advertised.Add(p); }
    }

    private void StreamFileInBackground(string path, string displayName, CancellationToken ct)
    {
        var id = _nextDownloadId++;
        if (_nextDownloadId < 0x80) _nextDownloadId = 0x80;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                if (fs.Length > _options.MaxTransferBytes)
                {
                    await AckAsync(id, 2, $"File vượt giới hạn {_options.MaxTransferBytes / 1024 / 1024} MB", ct);
                    return;
                }
                var nameBytes = Encoding.UTF8.GetBytes(displayName);
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
                _logger.LogWarning(ex, "Gửi file {Name} về CCU thất bại", displayName);
                try { await AckAsync(id, 2, "Lỗi đọc/gửi file: " + ex.Message, ct); } catch { /* ignore */ }
            }
        }, ct);
    }

    // ── Dán file: CCU → ZCU ───────────────────────────────────────────────

    /// <summary>
    /// Đặt các file đã upload (tên trong TransferDir) lên clipboard ZCU như vừa "Copy" trong trình quản lý
    /// file. Trả về khoá (các đường dẫn nối bằng LF) — caller dùng để chặn gửi ngược lại CCU — hoặc null nếu lỗi.
    /// </summary>
    public string? OnClipboardFiles(byte[] payload)
    {
        try
        {
            var names = System.Text.Json.JsonSerializer.Deserialize<string[]>(MessageCodec.DecodeStringMessage(payload)) ?? [];
            // Mỗi tên là đường dẫn tương đối trong TransferDir (vd ".paste-ab12cd34/Thư mục" hoặc ".paste-ab12cd34/a.txt");
            // có thể là file hoặc thư mục.
            var paths = names.Select(SanitizePath).Where(s => s is not null)
                             .Select(s => Path.Combine(_dir, Path.Combine(s!.ToArray())))
                             .Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
            if (paths.Count == 0) return null;

            _owner ??= new Interop.X11ClipboardOwner(_logger);
            if (!_owner.SetFiles(paths)) return null;
            _logger.LogInformation("Clipboard ZCU: đặt {Count} file để dán", paths.Count);
            // "Khoá" nhận diện nội dung clipboard: các đường dẫn nối bằng LF — trùng cách ClientSession tính khi poll.
            return string.Join("\n", paths.Select(p => new Uri(p).LocalPath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đặt được file lên clipboard ZCU");
            return null;
        }
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

    /// <summary>
    /// Tách đường dẫn tương đối thành các đoạn đã kiểm tra (không rỗng, không "."/"..", không ký tự cấm, đoạn cuối
    /// không đuôi ".part", tối đa 32 cấp, mỗi đoạn ≤ 200 ký tự). Trả null nếu có đoạn không hợp lệ — chặn thoát
    /// khỏi TransferDir bằng "../" hay đường dẫn tuyệt đối.
    /// </summary>
    internal static List<string>? SanitizePath(string raw)
    {
        var parts = raw.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 32) return null;

        foreach (var p in parts)
        {
            if (string.IsNullOrWhiteSpace(p) || p is "." or ".." || p.Length > 200) return null;
            if (p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || p.Contains('\0')) return null;
        }
        if (parts[^1].EndsWith(".part", StringComparison.OrdinalIgnoreCase)) return null;
        return parts.ToList();
    }

    private string RelativeToRoot(string fullPath)
        => Path.GetRelativePath(_dir, fullPath).Replace('\\', '/');

    private string UniquePath(string name)
    {
        var path = Path.Combine(_dir, name);
        if (!File.Exists(path) && !Directory.Exists(path) && !File.Exists(path + ".part")) return path;

        var stem = Path.GetFileNameWithoutExtension(name);
        var ext  = Path.GetExtension(name);
        for (var i = 1; ; i++)
        {
            path = Path.Combine(_dir, $"{stem} ({i}){ext}");
            if (!File.Exists(path) && !Directory.Exists(path) && !File.Exists(path + ".part")) return path;
        }
    }

    /// <summary>Xoá các thư mục tạm ".paste-*" của những lần dán cũ hơn 1 ngày (giữ ổ đĩa gọn).</summary>
    private void CleanOldPasteDirs()
    {
        try
        {
            if (!Directory.Exists(_dir)) return;
            foreach (var d in Directory.GetDirectories(_dir, ".paste-*"))
                if (Directory.GetCreationTimeUtc(d) < DateTime.UtcNow.AddDays(-1)) Directory.Delete(d, true);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Không dọn được thư mục .paste-*"); }
    }

    private void Abort(byte id)
    {
        if (!_uploads.Remove(id, out var up)) return;
        try { up.Stream.Dispose(); File.Delete(up.PartPath); } catch { /* ignore */ }
    }

    public void Dispose()
    {
        foreach (var id in _uploads.Keys.ToArray()) Abort(id);
        _owner?.Dispose();
    }
}
