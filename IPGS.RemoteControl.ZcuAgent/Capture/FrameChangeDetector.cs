namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>
/// Phát hiện khung hình KHÔNG đổi so với khung trước để bỏ qua bước nén JPEG + gửi
/// (Nấc 1 — FPS ZCU). Màn hình kiosk phần lớn thời gian đứng yên: không cần nén lại
/// cả màn hình 1080p (~76ms CPU trên Celeron J3160) chỉ để gửi đi một hình y hệt.
/// <para>
/// So sánh CHÍNH XÁC từng hàng (Span.SequenceEqual — vector hoá, dừng ngay ở hàng khác
/// đầu tiên), không dùng hash lấy mẫu để không bỏ sót thay đổi nhỏ (con trỏ nhập liệu,
/// một ký tự vừa gõ). Chi phí: màn hình tĩnh ≈ đọc 1 lượt ~8MB (vài ms); màn hình đổi
/// ≈ dừng sớm + chép phần từ hàng khác đầu tiên trở xuống vào bản lưu.
/// </para>
/// <para>
/// Không thread-safe: mỗi session sở hữu 1 instance, chỉ gọi từ capture loop.
/// Không có code unsafe để có thể link thẳng vào project test.
/// </para>
/// </summary>
internal sealed class FrameChangeDetector
{
    private byte[] _previous = [];
    private int _width;
    private int _height;
    private int _rowBytes;
    private bool _hasPrevious;

    /// <summary>Hàng đầu tiên khác nhau ở lần so sánh gần nhất (-1 nếu không đổi). Dùng cho thống kê/chẩn đoán.</summary>
    public int LastFirstChangedRow { get; private set; } = -1;

    /// <summary>Quên khung trước — lần gọi <see cref="HasChanged"/> kế tiếp chắc chắn trả về true.</summary>
    public void Reset()
    {
        _hasPrevious = false;
        LastFirstChangedRow = -1;
    }

    /// <summary>
    /// So sánh khung hiện tại với khung đã lưu, đồng thời cập nhật bản lưu khi có thay đổi.
    /// </summary>
    /// <param name="pixels">Dữ liệu điểm ảnh; hàng y bắt đầu tại <c>y * stride</c>.</param>
    /// <param name="width">Chiều rộng (pixel).</param>
    /// <param name="height">Chiều cao (pixel).</param>
    /// <param name="stride">Số byte mỗi hàng trong <paramref name="pixels"/> (có thể có padding).</param>
    /// <param name="bytesPerPixel">Mặc định 4 (BGRA/BGRX).</param>
    /// <returns>
    /// true nếu khác khung trước, là khung đầu tiên, đổi kích thước, hoặc dữ liệu không hợp lệ
    /// (khi không chắc chắn thì coi là "đã đổi" để không bao giờ bỏ sót hình).
    /// </returns>
    public bool HasChanged(ReadOnlySpan<byte> pixels, int width, int height, int stride, int bytesPerPixel = 4)
    {
        if (width <= 0 || height <= 0 || bytesPerPixel <= 0)
        {
            Reset();
            return true;
        }

        var rowBytes = width * bytesPerPixel;
        if (stride < rowBytes || (long)stride * (height - 1) + rowBytes > pixels.Length)
        {
            Reset();
            return true;
        }

        if (!_hasPrevious || width != _width || height != _height || rowBytes != _rowBytes)
        {
            var need = rowBytes * height;
            if (_previous.Length < need)
                _previous = GC.AllocateUninitializedArray<byte>(need);

            CopyRows(pixels, stride, rowBytes, 0, height);
            _width = width;
            _height = height;
            _rowBytes = rowBytes;
            _hasPrevious = true;
            LastFirstChangedRow = 0;
            return true;
        }

        for (var y = 0; y < height; y++)
        {
            var current = pixels.Slice(y * stride, rowBytes);
            var previous = _previous.AsSpan(y * rowBytes, rowBytes);
            if (!current.SequenceEqual(previous))
            {
                // Các hàng phía trên y giống hệt nhau — chỉ cần chép từ y trở xuống.
                CopyRows(pixels, stride, rowBytes, y, height);
                LastFirstChangedRow = y;
                return true;
            }
        }

        LastFirstChangedRow = -1;
        return false;
    }

    private void CopyRows(ReadOnlySpan<byte> pixels, int stride, int rowBytes, int fromRow, int height)
    {
        if (stride == rowBytes)
        {
            pixels.Slice(fromRow * stride, (height - fromRow) * rowBytes)
                  .CopyTo(_previous.AsSpan(fromRow * rowBytes));
            return;
        }

        for (var y = fromRow; y < height; y++)
            pixels.Slice(y * stride, rowBytes).CopyTo(_previous.AsSpan(y * rowBytes, rowBytes));
    }
}
