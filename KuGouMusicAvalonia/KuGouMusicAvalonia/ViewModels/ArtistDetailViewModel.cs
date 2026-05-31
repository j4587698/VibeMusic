using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class ArtistDetailHeaderSection
{
    public static ArtistDetailHeaderSection Instance { get; } = new();

    private ArtistDetailHeaderSection()
    {
    }
}

public sealed class ArtistDetailFooterSection
{
    public static ArtistDetailFooterSection Instance { get; } = new();

    private ArtistDetailFooterSection()
    {
    }
}

public partial class ArtistDetailViewModel : ViewModelBase
{
    private const int SongPageSize = 80;
    private int _currentPage;
    private int _totalSongs;

    public KugouArtist Artist { get; }

    [ObservableProperty]
    private ObservableCollection<KugouSong> _songs = new();

    public ObservableCollection<object> PageItems { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private string _statusMessage = "正在加载歌手歌曲";

    public string Title => Artist.Name;
    public string CoverUrl => Artist.Pic;
    public string Intro => string.IsNullOrWhiteSpace(Artist.Intro) ? "这个歌手还没有简介" : Artist.Intro;
    public string MetaText => Songs.Count > 0
        ? $"已加载 {Songs.Count}/{(_totalSongs > 0 ? _totalSongs : Songs.Count)} 首可播放歌曲 · 热度 {Artist.Heat} · {Artist.FansCount} 粉丝"
        : $"热度 {Artist.Heat} · {Artist.FansCount} 粉丝";

    public bool CanLoadMore => !IsLoading && !IsLoadingMore && _totalSongs > Songs.Count;

    public ArtistDetailViewModel(KugouArtist artist)
    {
        Artist = artist;
        RebuildPageItems();
        _ = LoadSongsAsync();
    }

    [RelayCommand]
    private async Task LoadSongsAsync()
    {
        if (IsLoading) return;
        _currentPage = 0;
        _totalSongs = 0;
        Songs.Clear();
        RebuildPageItems();
        OnSongsChanged();
        await LoadSongPageAsync(reset: true);
    }

    [RelayCommand]
    private async Task LoadMoreSongsAsync()
    {
        if (!CanLoadMore) return;
        await LoadSongPageAsync(reset: false);
    }

    private async Task LoadSongPageAsync(bool reset)
    {
        if (reset && IsLoading || !reset && IsLoadingMore) return;
        IsLoading = true;
        if (!reset)
        {
            IsLoading = false;
            IsLoadingMore = true;
        }

        StatusMessage = reset ? "正在加载歌手歌曲" : "正在加载更多歌曲";

        try
        {
            var nextPage = reset ? 1 : _currentPage + 1;
            var result = await MusicService.Client.GetArtistSongsTypedAsync(Artist.Id, page: nextPage, pageSize: SongPageSize);
            _currentPage = nextPage;
            _totalSongs = result.Total > 0 ? result.Total : Songs.Count + result.Items.Count;

            foreach (var song in result.Items.Where(song => !Songs.Any(existing => IsSameSong(existing, song))))
            {
                Songs.Add(song);
            }

            StatusMessage = Songs.Count > 0
                ? $"已加载 {Songs.Count}/{_totalSongs} 首歌曲"
                : "该歌手暂时没有可播放歌曲";
            RebuildPageItems();
            OnSongsChanged();
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"歌手歌曲加载失败：{ex.Message}";
        }
        finally
        {
            if (reset)
            {
                IsLoading = false;
            }
            else
            {
                IsLoadingMore = false;
            }

            OnSongsChanged();
        }
    }

    [RelayCommand]
    private void Back()
    {
           ShellNavigationService.Instance.Navigate("NavArtists");
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

    private void OnSongsChanged()
    {
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(CanLoadMore));
    }

    private void RebuildPageItems()
    {
        PageItems.Clear();
        PageItems.Add(ArtistDetailHeaderSection.Instance);
        foreach (var song in Songs)
        {
            PageItems.Add(song);
        }

        PageItems.Add(ArtistDetailFooterSection.Instance);
    }

    private static bool IsSameSong(KugouSong left, KugouSong right)
    {
        if (left.MixSongId > 0 && right.MixSongId > 0)
        {
            return left.MixSongId == right.MixSongId;
        }

        return !string.IsNullOrWhiteSpace(left.Hash) && string.Equals(left.Hash, right.Hash, System.StringComparison.OrdinalIgnoreCase);
    }
}