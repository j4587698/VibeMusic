using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace KuGouMusicAvalonia.Converters;

public sealed class ShellPageContentPaddingConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is "NavNowPlaying" or "NavLyrics")
        {
            return default(Thickness);
        }

        return string.Equals(parameter?.ToString(), "Headered", StringComparison.OrdinalIgnoreCase)
            ? new Thickness(0, 4, 0, 0)
            : new Thickness(0, 20, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
