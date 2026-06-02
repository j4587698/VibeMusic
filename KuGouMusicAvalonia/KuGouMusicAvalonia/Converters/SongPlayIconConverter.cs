using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using KuGou.Lite;
using Material.Icons;

namespace KuGouMusicAvalonia.Converters;

public class SongPlayIconConverter : IMultiValueConverter
{
    public static readonly SongPlayIconConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        bool isSame = false;
        bool isPlaying = false;
        if (values.Count >= 3 && values[0] is KugouSong song1 && values[1] is KugouSong song2)
        {
            if (!string.IsNullOrEmpty(song1.Hash) && !string.IsNullOrEmpty(song2.Hash))
            {
                isSame = song1.Hash == song2.Hash;
            }
            else
            {
                isSame = song1.Id == song2.Id;
            }
            
            if (values[2] is bool b)
            {
                isPlaying = b;
            }
        }
        
        return (isSame && isPlaying) ? MaterialIconKind.Pause : MaterialIconKind.Play;
    }
}
