using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KuGouMusicAvalonia.Converters;

public sealed class DurationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int durationSeconds)
        {
            return TimeSpan.FromSeconds(durationSeconds).ToString(@"mm\:ss");
        }
        if (value is double durationSecondsDouble)
        {
            return TimeSpan.FromSeconds(durationSecondsDouble).ToString(@"mm\:ss");
        }
        return "00:00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
