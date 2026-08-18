using System;
using KuGou.Lite;

namespace KuGouMusicAvalonia.Services;

public sealed class ShellNavigationService
{
    public static ShellNavigationService Instance { get; } = new();

    private ShellNavigationService()
    {
    }

    public event Action<string>? NavigationRequested;
    public event Action? BackRequested;
    public event Action? NowPlayingRequested;
    public event Action? NowPlayingCloseRequested;
    public event Action? LyricsRequested;
    public event Action? QueueToggleRequested;
    public event Action<KugouPlaylist>? PlaylistDetailRequested;
    public event Action<KugouRank>? RankingDetailRequested;
    public event Action<KugouArtist>? ArtistDetailRequested;
    public event Action<string, TimeSpan?>? ToastRequested;
    public event Action? PlaylistsRefreshRequested;

    public void Navigate(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            NavigationRequested?.Invoke(key);
        }
    }

    public void GoBack()
    {
        BackRequested?.Invoke();
    }

    public void OpenNowPlaying()
    {
        NowPlayingRequested?.Invoke();
    }

    public void CloseNowPlaying()
    {
        NowPlayingCloseRequested?.Invoke();
    }

    public void OpenLyrics()
    {
        LyricsRequested?.Invoke();
    }

    public void ToggleQueue()
    {
        QueueToggleRequested?.Invoke();
    }

    public void OpenPlaylistDetail(KugouPlaylist playlist)
    {
        PlaylistDetailRequested?.Invoke(playlist);
    }

    public void OpenRankingDetail(KugouRank rank)
    {
        RankingDetailRequested?.Invoke(rank);
    }

    public void OpenArtistDetail(KugouArtist artist)
    {
        ArtistDetailRequested?.Invoke(artist);
    }

    public void ShowToast(string message, TimeSpan? duration = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            ToastRequested?.Invoke(message, duration);
        }
    }

    public void RefreshPlaylists()
    {
        PlaylistsRefreshRequested?.Invoke();
    }
}
