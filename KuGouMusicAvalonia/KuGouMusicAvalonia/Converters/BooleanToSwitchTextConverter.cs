using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KuGouMusicAvalonia.Converters;

public sealed class BooleanToSwitchTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "开" : "关";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
