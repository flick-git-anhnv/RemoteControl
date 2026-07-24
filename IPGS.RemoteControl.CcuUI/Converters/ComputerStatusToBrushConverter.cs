using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using IPGS.RemoteControl.CcuClient;
using KztekComponentAvalonia.Theme;

namespace IPGS.RemoteControl.CcuUI.Converters
{
    /// <summary>
    /// Đổi <see cref="ComputerConnectivityStatus"/> sang màu icon trạng thái:
    /// xanh = Online (SSH + Agent Service đều OK), đỏ = Offline (thiếu 1 trong 2),
    /// xám = Checking/Unknown.
    /// </summary>
    public class ComputerStatusToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ComputerConnectivityStatus status)
            {
                return status switch
                {
                    ComputerConnectivityStatus.Online => KzTokens.SuccessBrush,
                    ComputerConnectivityStatus.Offline => KzTokens.ErrorBrush,
                    _ => KzTokens.TextMutedBrush
                };
            }

            return KzTokens.TextMutedBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
