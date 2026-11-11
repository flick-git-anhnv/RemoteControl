using System.Runtime.InteropServices;
using IPGS.RemoteControl.WinAgent.Native;

namespace IPGS.RemoteControl.WinAgent.Input;

/// <summary>Gói <c>SendInput</c> kèm ghi nhận lỗi (UIPI chặn → ERROR_ACCESS_DENIED).</summary>
internal static class InputSender
{
    private static readonly int InputSize = Marshal.SizeOf<NativeMethods.INPUT>();

    /// <summary>Gửi các sự kiện theo đúng thứ tự. Trả về false (kèm mã lỗi Win32) nếu không gửi đủ.</summary>
    public static bool TrySend(NativeMethods.INPUT[] inputs, out int win32Error)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, InputSize);
        win32Error = sent == inputs.Length ? 0 : Marshal.GetLastWin32Error();
        return sent == inputs.Length;
    }

    public static NativeMethods.INPUT Mouse(uint flags, int dx = 0, int dy = 0, uint data = 0) => new()
    {
        type = NativeMethods.INPUT_MOUSE,
        U = new NativeMethods.InputUnion
        {
            mi = new NativeMethods.MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags },
        },
    };

    public static NativeMethods.INPUT Key(ushort vk, ushort scan, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags },
        },
    };
}
