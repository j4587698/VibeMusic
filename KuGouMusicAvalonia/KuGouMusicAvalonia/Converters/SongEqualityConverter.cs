using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using KuGou.Lite;

namespace KuGouMusicAvalonia.Converters;

public class SongEqualityConverter : IMultiValueConverter
{
    public static readonly SongEqualityConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is KugouSong song1 && values[1] is KugouSong song2)
        {
            if (!string.IsNullOrEmpty(song1.Hash) && !string.IsNullOrEmpty(song2.Hash))
            {
                return song1.Hash == song2.Hash;
            }
            return song1.Id == song2.Id;
        }
        return false;
    }
}
