using System;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Renci.SshNet.Common;

namespace IPGS.RemoteControl.CcuUI.Views
{
    /// <summary>
    /// F07: Helper dùng chung cho các cửa sổ chạy lệnh SSH (RemoteCommandWindow, BulkActionWindow).
    /// (1) Nhận diện lệnh reboot/shutdown — khi lệnh này làm SSH ngắt thì đó là kết quả MONG ĐỢI,
    ///     không phải lỗi → hiển thị thông báo thông tin thân thiện thay vì báo LỖI đỏ.
    /// (2) Gate dòng chẩn đoán "[debug] ..." — ẩn mặc định với người dùng cuối (lộ chi tiết
    ///     kỹ thuật nội bộ + dạng chuỗi escape của lệnh sudo), chỉ hiện khi bật debug.
    /// </summary>
    internal static class SshCommandHints
    {
        /// <summary>
        /// Match lệnh tắt/khởi động lại máy ở VỊ TRÍ LỆNH (đầu chuỗi hoặc sau ; &amp; | ( ),
        /// cho phép tiền tố "sudo [options]": reboot / poweroff / halt / shutdown /
        /// init 0|6 / telinit 0|6 / systemctl reboot|poweroff|halt.
        /// </summary>
        private static readonly Regex ShutdownPattern = new(
            @"(^|[;&|(])\s*(sudo\s+(-\S+\s+)*)?(reboot|poweroff|halt|shutdown|(tel)?init\s+[06]|systemctl\s+(reboot|poweroff|halt))(\s|$|[;&|)])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// F07: bật dòng chẩn đoán "[debug] ..." bằng biến môi trường IPGS_RC_DEBUG=1.
        /// Chọn env var (thay vì #if DEBUG) để kỹ sư hỗ trợ bật được ngay trên bản Release
        /// tại hiện trường khi cần chẩn đoán, không phải build lại; mặc định người dùng
        /// cuối không thấy. (Codebase chưa có cơ chế debug-flag nào khác để tái dùng.)
        /// </summary>
        internal static readonly bool DiagEnabled =
            Environment.GetEnvironmentVariable("IPGS_RC_DEBUG") == "1";

        /// <summary>Thông báo thân thiện khi lệnh reboot/shutdown làm ngắt kết nối SSH.</summary>
        internal const string ShutdownInfoMessage =
            "ℹ️ Đã gửi lệnh khởi động lại / tắt máy tới máy đích — kết nối SSH sẽ ngắt trong giây lát " +
            "(đây là hành vi bình thường, KHÔNG phải lỗi). Vui lòng kết nối lại sau khoảng 1 phút " +
            "(nếu là lệnh tắt máy, cần bật lại máy trước).";

        /// <summary>Lệnh người dùng nhập có thuộc nhóm reboot/shutdown không (kể cả kèm sudo).</summary>
        internal static bool IsShutdownCommand(string command)
            => !string.IsNullOrWhiteSpace(command) && ShutdownPattern.IsMatch(command);

        /// <summary>
        /// Exception có phải dạng MẤT KẾT NỐI (aborted/closed/reset/EOF/timeout) không.
        /// CHỈ dùng kết hợp với <see cref="IsShutdownCommand"/> — lỗi khác (sai password sudo,
        /// command not found...) KHÔNG rơi vào đây nên vẫn được báo lỗi bình thường.
        /// </summary>
        internal static bool IsConnectionDropped(Exception? ex)
        {
            for (; ex != null; ex = ex.InnerException)
            {
                // SshConnectionException = transport SSH đã đứt (VD "An established connection
                // was aborted by the server." khi máy đích reboot giữa chừng).
                if (ex is SshConnectionException || ex is SocketException)
                    return true;

                var msg = ex.Message ?? string.Empty;
                if (msg.Contains("connection was aborted", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("connection was closed", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("end of stream", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("EOF", StringComparison.Ordinal)
                    || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
