using KuGou.Lite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services;

public sealed class FavoriteSongService
{
    public static FavoriteSongService Instance { get; } = new();

    private readonly object _gate = new();
    private readonly HashSet<string> _favoriteKeys;

    private FavoriteSongService()
    {
        _favoriteKeys = new HashSet<string>(LocalMusicStore.Instance.LoadFavoriteKeys(), StringComparer.Ordinal);
    }

    public bool IsFavorite(KugouSong? song)
    {
        var key = GetSongKey(song);
        lock (_gate)
        {
            return !string.IsNullOrWhiteSpace(key) && _favoriteKeys.Contains(key);
        }
    }

    public async Task<bool> ToggleFavoriteAsync(KugouSong? song, CancellationToken cancellationToken = default)
    {
        var key = GetSongKey(song);
        if (song is null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var isFavorite = !IsFavorite(song);
        if (MusicService.IsLoggedIn)
        {
            if (isFavorite)
            {
                await MusicService.AddSongToFavoritePlaylistAsync(song, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await MusicService.RemoveSongFromFavoritePlaylistAsync(song, cancellationToken).ConfigureAwait(false);
            }
        }

        ApplyLocalFavorite(song, isFavorite);
        return isFavorite;
    }

    public async Task RefreshFromCloudAsync(CancellationToken cancellationToken = default)
    {
        if (!MusicService.IsLoggedIn)
        {
            return;
        }

        var songs = await MusicService.GetFavoriteSongsAsync(cancellationToken).ConfigureAwait(false);
        var keys = songs
            .Select(GetSongKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        LocalMusicStore.Instance.ReplaceFavoriteSongs(songs);
        lock (_gate)
        {
            _favoriteKeys.Clear();
            foreach (var key in keys)
            {
                _favoriteKeys.Add(key);
            }
        }
    }

    private void ApplyLocalFavorite(KugouSong song, bool isFavorite)
    {
        var key = GetSongKey(song);
        lock (_gate)
        {
            if (isFavorite)
            {
                _favoriteKeys.Add(key);
            }
            else
            {
                _favoriteKeys.Remove(key);
            }
        }

        LocalMusicStore.Instance.SetFavorite(song, isFavorite);
    }

    private static string GetSongKey(KugouSong? song)
    {
        return LocalMusicStore.GetSongKey(song);
    }
}