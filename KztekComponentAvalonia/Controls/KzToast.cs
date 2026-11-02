using System;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Facade gọn cho toast notification — API theo design spec iAccess Phase 8:
/// <c>KzToast.Show("Đã lưu cấu hình", KzToastKind.Success)</c>.
/// Yêu cầu window đã đặt <see cref="KzToastHost"/> làm con cuối của Panel gốc
/// (xem chú thích KzToastHost). Không có host đang mở → no-op an toàn.
/// </summary>
public static class KzToast
{
    /// <inheritdoc cref="KzToastHost.Show(string, KzToastKind, TimeSpan?)"/>
    public static void Show(string message, KzToastKind kind = KzToastKind.Info, TimeSpan? duration = null)
        => KzToastHost.Show(message, kind, duration);
}
