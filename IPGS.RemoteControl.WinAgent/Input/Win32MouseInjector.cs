using IPGS.RemoteControl.Protocol;
using IPGS.RemoteControl.WinAgent.Capture;
using IPGS.RemoteControl.ZcuAgent;
using Microsoft.Extensions.Logging;
using static IPGS.RemoteControl.WinAgent.Native.NativeMethods;

namespace IPGS.RemoteControl.WinAgent.Input;

/// <summary>
/// Chuột qua <c>SendInput</c> toạ độ tuyệt đối trên toàn virtual screen (<c>MOUSEEVENTF_VIRTUALDESK</c>).
/// Toạ độ nhận vào là toạ độ trong khung hình đã chụp (gốc (0,0) = góc trên-trái virtual screen, xem
/// <see cref="GdiScreenCapturer"/>) nên cộng lại gốc ảo rồi chuẩn hoá về 0..65535 theo kích thước virtual screen.
/// <para>
/// Nếu SendInput báo ERROR_ACCESS_DENIED (5) thì thường do UIPI: cửa sổ đích chạy quyền cao hơn agent
/// (Task Manager, cmd Admin…). Chạy agent với quyền Administrator (Task Scheduler "highest privileges").
/// </para>
/// </summary>
internal sealed class Win32MouseInjector : IMouseInjector
{
    private const uint WheelDelta = 120;

    private readonly ILogger<Win32MouseInjector> _logger;
    private long _lastErrorLogTick;
    private bool _initialized;
    private bool _disposed;

    public Win32MouseInjector(ILogger<Win32MouseInjector> logger) => _logger = logger;

    public void Initialize() => _initialized = true;

    public void Move(int x, int y)
    {
        if (!_initialized || _disposed) return;

        var (_, _, vw, vh) = GdiScreenCapturer.VirtualScreen();
        if (vw <= 0 || vh <= 0) return;

        var nx = Normalize(x, vw);
        var ny = Normalize(y, vh);
        Send(InputSender.Mouse(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, nx, ny));
    }

    public void Button(MouseButton button, bool isDown)
    {
        if (!_initialized || _disposed) return;

        switch (button)
        {
            case MouseButton.Left:
                Send(InputSender.Mouse(isDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP));
                break;
            case MouseButton.Right:
                Send(InputSender.Mouse(isDown ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP));
                break;
            case MouseButton.Middle:
                Send(InputSender.Mouse(isDown ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP));
                break;

            // Cuộn: X11 gửi cặp down+up cho "nút" 4..7 → chỉ phát một nấc khi nhận "down".
            case MouseButton.WheelUp:    if (isDown) Send(InputSender.Mouse(MOUSEEVENTF_WHEEL,  data: unchecked((uint)(int)WheelDelta))); break;
            case MouseButton.WheelDown:  if (isDown) Send(InputSender.Mouse(MOUSEEVENTF_WHEEL,  data: unchecked((uint)-(int)WheelDelta))); break;
            case MouseButton.WheelRight: if (isDown) Send(InputSender.Mouse(MOUSEEVENTF_HWHEEL, data: unchecked((uint)(int)WheelDelta))); break;
            case MouseButton.WheelLeft:  if (isDown) Send(InputSender.Mouse(MOUSEEVENTF_HWHEEL, data: unchecked((uint)-(int)WheelDelta))); break;
        }
    }

    /// <summary>
    /// Pixel p (0..size-1) → giá trị chuẩn hoá trỏ vào GIỮA pixel đó. Windows quy đổi ngược theo
    /// floor(n * size / 65536), nên dùng tâm pixel để không bị lệch 1 pixel do làm tròn.
    /// </summary>
    private static int Normalize(int p, int size)
    {
        p = Math.Clamp(p, 0, size - 1);
        var n = (int)(((2L * p + 1) * 65536) / (2L * size));
        return Math.Clamp(n, 0, 65535);
    }

    private void Send(INPUT input)
    {
        if (InputSender.TrySend([input], out var err)) return;

        // Giới hạn log: tối đa 1 dòng / 5 giây để không ngập log khi UIPI chặn liên tục.
        var now = Environment.TickCount64;
        if (now - _lastErrorLogTick < 5_000) return;
        _lastErrorLogTick = now;
        _logger.LogWarning(
            "SendInput(chuột) thất bại, lỗi Win32 {Err}{Hint}", err,
            err == 5 ? " (ACCESS_DENIED — UIPI: cửa sổ đích có quyền cao hơn agent; chạy agent bằng quyền Administrator)" : "");
    }

    public void Dispose() => _disposed = true;
}
