using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class PlaylistsHeaderSection
{
    public static PlaylistsHeaderSection Instance { get; } = new();

    private PlaylistsHeaderSection()
    {
    }
}

public sealed class PlaylistsFooterSection
{
    public static PlaylistsFooterSection Instance { get; } = new();

    private PlaylistsFooterSection()
    {
    }
}

public sealed class PlaylistCardRow
{
    public PlaylistCardRow(IReadOnlyList<KugouPlaylist> playlists)
    {
        Playlists = playlists;
    }

    public IReadOnlyList<KugouPlaylist> Playlists { get; }
}

public partial class PlaylistsViewModel : ViewModelBase
{
    private int _playlistCardsPerRow = 4;

    [ObservableProperty]
    private ObservableCollection<KugouPlaylist> _playlists = new();

    [ObservableProperty]
    private ObservableCollection<KugouPlaylist> _userPlaylists = new();

    public ObservableCollection<object> PageItems { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    private const int PlaylistFetchSize = 120;
    private const int PlaylistBatchSize = 24;
    private readonly List<KugouPlaylist> _allPlaylists = new();

    public bool CanLoadMore => !IsLoading && !IsLoadingMore && Playlists.Count < _allPlaylists.Count;

    [ObservableProperty]
    private bool _isUserPlaylistsLoading;

    [ObservableProperty]
    private bool _isCreatingPlaylist;

    [ObservableProperty]
    private string _statusMessage = "正在加载精选歌单";

    [ObservableProperty]
    private string _newPlaylistName = string.Empty;

    [ObservableProperty]
    private bool _createPlaylistFromQueue = true;

    [ObservableProperty]
    private string _userPlaylistStatusMessage = "登录后可同步自己的歌单";

    public PlaylistsViewModel()
    {
        RebuildPageItems();
        _ = LoadDataAsync();
    }

    public void SetPlaylistCardsPerRow(int cardsPerRow)
    {
        var normalizedCount = System.Math.Max(1, cardsPerRow);
        if (_playlistCardsPerRow == normalizedCount)
        {
            return;
        }

        _playlistCardsPerRow = normalizedCount;
        RebuildPageItems();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLoadMore));
    }

    partial void OnIsLoadingMoreChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLoadMore));
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var result = await MusicService.Client.GetTopPlaylistsTypedAsync(categoryId: 0, page: 1, pageSize: PlaylistFetchSize);
            Playlists.Clear();
            _allPlaylists.Clear();
            if (result?.Items != null)
            {
                _allPlaylists.AddRange(result.Items.Where(p => p != null));
            }

            AddNextPlaylistBatch();

            if (_allPlaylists.Count == 0)
            {
                UseDemoData();
                StatusMessage = "接口暂时无内容，已展示示例歌单";
                await LoadUserPlaylistsAsync();
                return;
            }

            StatusMessage = "歌单已同步";
            await LoadUserPlaylistsAsync();
        }
        catch (System.Exception ex)
        {
            UseDemoData();
            StatusMessage = $"接口加载失败，已展示示例歌单：{ex.Message}";
            await LoadUserPlaylistsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void LoadMore()
    {
        if (!CanLoadMore) return;
        IsLoadingMore = true;

        try
        {
            AddNextPlaylistBatch();
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private void AddNextPlaylistBatch()
    {
        var nextPlaylists = _allPlaylists.Skip(Playlists.Count).Take(PlaylistBatchSize).ToArray();
        foreach (var playlist in nextPlaylists)
        {
            Playlists.Add(playlist);
        }

        RebuildPageItems();
        OnPropertyChanged(nameof(CanLoadMore));
    }

    private void UseDemoData()
    {
        Playlists.Clear();
        _allPlaylists.Clear();
        foreach (var playlist in DemoMusicData.Playlists)
        {
            _allPlaylists.Add(playlist);
        }

        AddNextPlaylistBatch();
    }

    private void RebuildPageItems()
    {
        PageItems.Clear();
        PageItems.Add(PlaylistsHeaderSection.Instance);

        foreach (var row in Playlists.Chunk(_playlistCardsPerRow))
        {
            PageItems.Add(new PlaylistCardRow(row));
        }

        PageItems.Add(PlaylistsFooterSection.Instance);
    }



    [RelayCommand]
    private void OpenPlaylist(KugouPlaylist playlist)
    {
        if (playlist is null) return;
        ShellNavigationService.Instance.OpenPlaylistDetail(playlist);
    }

    [RelayCommand]
    private async Task LoadUserPlaylistsAsync()
    {
        if (IsUserPlaylistsLoading) return;
        UserPlaylists.Clear();

        if (!MusicService.IsLoggedIn)
        {
            UserPlaylistStatusMessage = "登录后可查看和创建自己的歌单";
            return;
        }

        IsUserPlaylistsLoading = true;
        UserPlaylistStatusMessage = "正在同步我的歌单";

        try
        {
            var result = await MusicService.Client.GetUserPlaylistsTypedAsync(page: 1, pageSize: 40);
            foreach (var playlist in result.Items)
            {
                UserPlaylists.Add(playlist);
            }

            UserPlaylistStatusMessage = UserPlaylists.Count > 0 ? $"已同步 {UserPlaylists.Count} 个歌单" : "还没有同步到自己的歌单";
        }
        catch (System.Exception ex)
        {
            UserPlaylistStatusMessage = $"我的歌单同步失败：{ex.Message}";
        }
        finally
        {
            IsUserPlaylistsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreatePlaylistAsync()
    {
        var name = NewPlaylistName.Trim();
        if (string.IsNullOrWhiteSpace(name) || IsCreatingPlaylist)
        {
            return;
        }

        if (!MusicService.IsLoggedIn)
        {
            UserPlaylistStatusMessage = "请先在设置页登录，再创建歌单";
            return;
        }

        IsCreatingPlaylist = true;
        UserPlaylistStatusMessage = "正在创建歌单";

        try
        {
            await MusicService.CreatePlaylistAsync(name);
            await LoadUserPlaylistsAsync();

            var created = UserPlaylists.FirstOrDefault(playlist => playlist.Name == name && playlist.Listid is int);
            if (CreatePlaylistFromQueue && created?.Listid is int listId && PlayerService.Instance.Queue.Count > 0)
            {
                await MusicService.AddSongsToPlaylistAsync(listId, PlayerService.Instance.Queue.ToList());
                UserPlaylistStatusMessage = $"已创建歌单，并保存当前队列 {PlayerService.Instance.Queue.Count} 首";
            }
            else
            {
                UserPlaylistStatusMessage = "歌单已创建";
            }

            NewPlaylistName = string.Empty;
        }
        catch (System.Exception ex)
        {
            UserPlaylistStatusMessage = $"创建歌单失败：{ex.Message}";
        }
        finally
        {
            IsCreatingPlaylist = false;
        }
    }

}
