using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace IPGS.RemoteControl.CcuUI.Converters
{
    /// <summary>
    /// Đổi bool? sang bool: true CHỈ KHI giá trị gốc là true (null hoặc false đều thành false).
    /// Dùng để disable nút khi chưa xác nhận được kết nối SSH (SshReachable == true).
    /// </summary>
    public class NullableBoolIsTrueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
