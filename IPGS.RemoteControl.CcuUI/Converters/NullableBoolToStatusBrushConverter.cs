using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KztekComponentAvalonia.Theme;

namespace IPGS.RemoteControl.CcuUI.Converters
{
    /// <summary>
    /// Đổi bool? (kết quả dò 1 loại kết nối riêng lẻ: SSH hoặc Agent Remote Control)
    /// sang màu icon: xanh = true, đỏ = false, xám = null (chưa/đang kiểm tra).
    /// </summary>
    public class NullableBoolToStatusBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                true => KzTokens.SuccessBrush,
                false => KzTokens.ErrorBrush,
                _ => KzTokens.TextMutedBrush
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
