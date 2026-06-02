using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public enum SearchType
{
    Song,
    Playlist,
    Artist
}

public sealed class SearchHeaderSection
{
    public static SearchHeaderSection Instance { get; } = new();

    private SearchHeaderSection()
    {
    }
}

public partial class SearchViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _keyword = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<object> _searchResults = new();

    public ObservableCollection<object> PageItems { get; } = new();

    [ObservableProperty]
    private SearchType _currentSearchType = SearchType.Song;

    [ObservableProperty]
    private ObservableCollection<string> _hotKeywords = new()
    {
        "周杰伦",
        "新歌速递",
        "说唱巅峰",
        "深夜电台",
        "Eason Chan",
        "冥想空间"
    };

    [ObservableProperty]
    private string _statusMessage = "输入关键词开始搜索";

    [ObservableProperty]
    private ObservableCollection<string> _searchHistories = new();

    public SearchViewModel()
    {
        var state = AppStateStore.Load();
        if (state.SearchHistories != null)
        {
            foreach (var h in state.SearchHistories)
            {
                SearchHistories.Add(h);
            }
        }
        RebuildPageItems();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Keyword)) return;
        
        if (IsLoading) return;
        IsLoading = true;

        var trimmed = Keyword.Trim();
        if (SearchHistories.Contains(trimmed))
        {
            SearchHistories.Remove(trimmed);
        }
        SearchHistories.Insert(0, trimmed);
        if (SearchHistories.Count > 15)
        {
            SearchHistories.RemoveAt(SearchHistories.Count - 1);
        }
        var state = AppStateStore.Load();
        state.SearchHistories = SearchHistories.ToList();
        AppStateStore.Save(state);

        try
        {
            SearchResults.Clear();
            RebuildPageItems();

            if (CurrentSearchType == SearchType.Song)
            {
                var result = await MusicService.Client.SearchSongsTypedAsync(Keyword, page: 1, pageSize: 60);
                if (result?.Items != null) SearchResults = new ObservableCollection<object>(result.Items);
            }
            else if (CurrentSearchType == SearchType.Playlist)
            {
                var result = await MusicService.Client.SearchPlaylistsTypedAsync(Keyword, page: 1, pageSize: 60);
                if (result?.Items != null) SearchResults = new ObservableCollection<object>(result.Items);
            }
            else if (CurrentSearchType == SearchType.Artist)
            {
                var result = await MusicService.Client.SearchArtistsTypedAsync(Keyword, page: 1, pageSize: 60);
                if (result?.Items != null) SearchResults = new ObservableCollection<object>(result.Items);
            }

            RebuildPageItems();

            if (SearchResults.Count == 0)
            {
                StatusMessage = "没有找到相关内容";
                return;
            }

            StatusMessage = $"找到 {SearchResults.Count} 个结果";
        }
        catch (System.Exception ex)
        {
            SearchResults.Clear();
            RebuildPageItems();
            StatusMessage = $"搜索失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlaySongAsync(KugouSong song)
    {
        if (song == null) return;
        if (PlayerService.IsSameSong(song, PlayerService.Instance.CurrentSong))
        {
            PlayerService.Instance.TogglePlayPause();
            return;
        }
        var songs = SearchResults.OfType<KugouSong>().ToList();
        var index = songs.IndexOf(song);
        await PlayerService.Instance.PlayQueueAsync(songs, index < 0 ? 0 : index, $"搜索：{Keyword}", replaceQueue: true);
    }

    [RelayCommand]
    private async Task DownloadSongAsync(KugouSong song)
    {
        await PlayerService.Instance.DownloadAsync(song);
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        var songs = SearchResults.OfType<KugouSong>().ToList();
        if (songs.Count == 0) return;
        await PlayerService.Instance.PlayQueueAsync(songs, 0, $"搜索：{Keyword}", replaceQueue: true);
    }

    [RelayCommand]
    private void QueueAll()
    {
        var songs = SearchResults.OfType<KugouSong>().ToList();
        if (songs.Count == 0) return;
        var added = PlayerService.Instance.AppendToQueue(songs, $"搜索：{Keyword}");
        StatusMessage = added > 0 ? $"已加入 {added} 首到播放队列" : "这些歌曲已在播放队列中";
    }

    [RelayCommand]
    private async Task UseKeywordAsync(string keyword)
    {
        Keyword = keyword;
        await SearchAsync();
    }

    public bool IsSongSearch => CurrentSearchType == SearchType.Song;

    [RelayCommand]
    private async Task SetSearchTypeAsync(SearchType type)
    {
        if (CurrentSearchType == type) return;
        CurrentSearchType = type;
        OnPropertyChanged(nameof(IsSongSearch));
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            await SearchAsync();
        }
    }

    [RelayCommand]
    private void OpenPlaylist(KugouPlaylist playlist)
    {
        if (playlist != null) ShellNavigationService.Instance.OpenPlaylistDetail(playlist);
    }

    [RelayCommand]
    private void OpenArtist(KugouArtist artist)
    {
        if (artist != null) ShellNavigationService.Instance.OpenArtistDetail(artist);
    }

    [RelayCommand]
    private void ClearHistory()
    {
        SearchHistories.Clear();
        var state = AppStateStore.Load();
        state.SearchHistories.Clear();
        AppStateStore.Save(state);
    }

    private void RebuildPageItems()
    {
        PageItems.Clear();
        PageItems.Add(SearchHeaderSection.Instance);
        foreach (var song in SearchResults)
        {
            PageItems.Add(song);
        }
    }
}
