using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class PlaylistDetailHeaderSection
{
    public static PlaylistDetailHeaderSection Instance { get; } = new();

    private PlaylistDetailHeaderSection()
    {
    }
}

public sealed class PlaylistLoadMoreSentinel
{
    public static PlaylistLoadMoreSentinel Instance { get; } = new();

    private PlaylistLoadMoreSentinel()
    {
    }
}

public partial class PlaylistDetailViewModel : ViewModelBase
{
    public KugouPlaylist Playlist { get; }

    [ObservableProperty]
    private ObservableCollection<KugouSong> _songs = new();

    public ObservableCollection<object> PageItems { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private bool _canLoadMore = true;

    [ObservableProperty]
    private string _statusMessage = "正在加载歌单歌曲";

    private int _currentPage = 1;
    private const int PageSize = 100;

    public string Title => Playlist.Name;
    public string Subtitle => string.IsNullOrWhiteSpace(Playlist.Nickname) ? "歌单" : Playlist.Nickname;
    public string CoverUrl => Playlist.Pic;
    public string Intro => string.IsNullOrWhiteSpace(Playlist.Intro) ? "这个歌单还没有简介" : Playlist.Intro;
    public string MetaText => $"{Playlist.Count} 首 · 播放 {Playlist.PlayCount}";

    public PlaylistDetailViewModel(KugouPlaylist playlist)
    {
        Playlist = playlist;
        RebuildPageItems();
        _ = LoadSongsAsync();
    }

    [RelayCommand]
    private async Task LoadSongsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        _currentPage = 1;
        CanLoadMore = true;
        Songs.Clear();
        RebuildPageItems();
        StatusMessage = "正在加载歌单歌曲";

        try
        {
            await FetchPageAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadNextPageAsync()
    {
        if (IsLoading || IsLoadingMore || !CanLoadMore) return;
        IsLoadingMore = true;

        try
        {
            _currentPage++;
            await FetchPageAsync();
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private async Task FetchPageAsync()
    {
        var listIdStr = (Playlist.Listid as int?)?.ToString();
        var globalId = !string.IsNullOrWhiteSpace(Playlist.GlobalCollectionId) ? Playlist.GlobalCollectionId! : Playlist.Id.ToString();
        
        KugouListResult<KugouSong>? result = null;
        if (!string.IsNullOrWhiteSpace(listIdStr) && listIdStr != "0")
        {
            result = await MusicService.Client.GetPlaylistTracksNewTypedAsync(listIdStr, page: _currentPage, pageSize: PageSize);
        }
        
        if (result is null || result.Items.Count == 0)
        {
            result = await MusicService.Client.GetPlaylistTracksTypedAsync(globalId, page: _currentPage, pageSize: PageSize);
        }
        
        var newSongs = new List<KugouSong>();
        if (result?.Items != null && result.Items.Count > 0)
        {
            newSongs.AddRange(result.Items);
            if (result.Items.Count < PageSize)
            {
                CanLoadMore = false;
            }
        }
        else
        {
            CanLoadMore = false;
        }

        if (_currentPage == 1 && newSongs.Count == 0 && Playlist.Songs is { Count: > 0 })
        {
            newSongs.AddRange(Playlist.Songs);
            CanLoadMore = false;
        }
        
        foreach (var song in newSongs)
        {
            Songs.Add(song);
        }

        AppendToPageItems(newSongs);
        StatusMessage = Songs.Count > 0 ? $"已加载 {Songs.Count} 首歌曲" : "该歌单暂时没有歌曲";
    }

    [RelayCommand]
    private void Back()
    {
        ShellNavigationService.Instance.Navigate("NavPlaylists");
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        if (Songs.Count == 0) return;
        await PlayerService.Instance.PlayQueueAsync(Songs.ToList(), 0, Title, replaceQueue: true);
    }

    [RelayCommand]
    private void QueueAll()
    {
        if (Songs.Count == 0) return;
        var added = PlayerService.Instance.AppendToQueue(Songs.ToList(), Title);
        StatusMessage = added > 0 ? $"已加入 {added} 首到播放队列" : "这些歌曲已在播放队列中";
    }

    [RelayCommand]
    private async Task PlaySongAsync(KugouSong song)
    {
        if (song is null) return;
        var index = Songs.IndexOf(song);
        await PlayerService.Instance.PlayQueueAsync(Songs.ToList(), index < 0 ? 0 : index, Title, replaceQueue: true);
    }

    [RelayCommand]
    private async Task DownloadSongAsync(KugouSong song)
    {
        await PlayerService.Instance.DownloadAsync(song);
    }

    private void RebuildPageItems()
    {
        PageItems.Clear();
        PageItems.Add(PlaylistDetailHeaderSection.Instance);
        foreach (var song in Songs)
        {
            PageItems.Add(song);
        }
        if (CanLoadMore)
        {
            PageItems.Add(PlaylistLoadMoreSentinel.Instance);
        }
    }

    private void AppendToPageItems(IEnumerable<KugouSong> newSongs)
    {
        if (PageItems.LastOrDefault() is PlaylistLoadMoreSentinel)
        {
            PageItems.RemoveAt(PageItems.Count - 1);
        }
        foreach (var song in newSongs)
        {
            PageItems.Add(song);
        }
        if (CanLoadMore)
        {
            PageItems.Add(PlaylistLoadMoreSentinel.Instance);
        }
    }
}