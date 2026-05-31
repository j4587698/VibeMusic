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

public partial class PlaylistDetailViewModel : ViewModelBase
{
    public KugouPlaylist Playlist { get; }

    [ObservableProperty]
    private ObservableCollection<KugouSong> _songs = new();

    public ObservableCollection<object> PageItems { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "正在加载歌单歌曲";

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
        Songs.Clear();
        RebuildPageItems();
        StatusMessage = "正在加载歌单歌曲";

        try
        {
            KugouListResult<KugouSong>? result = null;
            if (Playlist.Listid is int listId && listId > 0)
            {
                result = await MusicService.Client.GetPlaylistTracksNewTypedAsync(listId.ToString(), page: 1, pageSize: 120);
            }

            if (result is null || result.Items.Count == 0)
            {
                var globalId = !string.IsNullOrWhiteSpace(Playlist.GlobalCollectionId) ? Playlist.GlobalCollectionId! : Playlist.Id.ToString();
                result = await MusicService.Client.GetPlaylistTracksTypedAsync(globalId, page: 1, pageSize: 120);
            }

            foreach (var song in result.Items)
            {
                Songs.Add(song);
            }

            if (Songs.Count == 0 && Playlist.Songs is { Count: > 0 })
            {
                foreach (var song in Playlist.Songs)
                {
                    Songs.Add(song);
                }
            }

            StatusMessage = Songs.Count > 0 ? $"已加载 {Songs.Count} 首歌曲" : "该歌单暂时没有歌曲";
            RebuildPageItems();
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"歌单歌曲加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
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
    }
}