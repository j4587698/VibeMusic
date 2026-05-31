using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KuGouMusicAvalonia.Converters;

public sealed class EmptyStringToNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string text && string.IsNullOrWhiteSpace(text) ? null : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}