using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using KuGou.Lite;

namespace KuGouMusicAvalonia.Converters;

public class SongPlayingBrushConverter : IMultiValueConverter
{
    public static readonly SongPlayingBrushConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        bool isPlaying = false;
        if (values.Count >= 2 && values[0] is KugouSong song1 && values[1] is KugouSong song2)
        {
            if (!string.IsNullOrEmpty(song1.Hash) && !string.IsNullOrEmpty(song2.Hash))
            {
                isPlaying = song1.Hash == song2.Hash;
            }
            else
            {
                isPlaying = song1.Id == song2.Id;
            }
        }
        
        if (Application.Current != null && Application.Current.TryGetResource(isPlaying ? "MusicPrimaryBrush" : "MusicTextBrush", Application.Current.ActualThemeVariant, out var res) && res is IBrush brush)
        {
            return brush;
        }
        
        return Brushes.White;
    }
}
