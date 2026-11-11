using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace IPGS.RemoteControl.CcuUI.Converters
{
    /// <summary>
    /// Đổi bool? sang bool: true CHỈ KHI giá trị gốc là true (null hoặc false đều thành false).
    /// Dùng để disable nút khi chưa xác nhận được kết nối SSH (SshReachable == true).
    /// ConverterParameter="not" đảo kết quả (true khi giá trị gốc KHÔNG phải true) — dùng cho IsVisible.
    /// </summary>
    public class NullableBoolIsTrueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isTrue = value is true;
            return parameter is "not" ? !isTrue : isTrue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
