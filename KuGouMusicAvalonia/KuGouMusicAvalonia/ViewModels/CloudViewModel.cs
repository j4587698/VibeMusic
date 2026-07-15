using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class CloudHeaderSection
{
    public static CloudHeaderSection Instance { get; } = new();
    private CloudHeaderSection() { }
}

public sealed class CloudLoadMoreSection
{
    public static CloudLoadMoreSection Instance { get; } = new();
    private CloudLoadMoreSection() { }
}

public partial class CloudViewModel : ViewModelBase
{
    private const int PageSize = 60;
    private int _currentPage;

    public ObservableCollection<object> PageItems { get; } = new();
    public ObservableCollection<KugouSong> Songs { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private bool _isLoadingMore;

    [ObservableProperty]
    private string _statusMessage = "正在加载云盘/收藏";

    [ObservableProperty]
    private string _songCountText = "0 首歌曲";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private int _totalCount;

    public bool CanLoadMore => !IsLoading && !IsLoadingMore && TotalCount > Songs.Count;

    public CloudViewModel()
    {
        PageItems.Add(CloudHeaderSection.Instance);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "正在加载云盘/收藏";
        Songs.Clear();
        TotalCount = 0;
        _currentPage = 0;
        RebuildPageItems();

        try
        {
            if (!MusicService.Client.GetLoginState().IsLoggedIn)
            {
                StatusMessage = "登录后可查看云盘/收藏";
                SongCountText = "0 首歌曲";
                return;
            }

            await LoadPageAsync(1);
            StatusMessage = Songs.Count > 0 ? "云盘/收藏已同步" : "云盘/收藏暂无内容";
            UpdateSongCountText();
        }
        catch (Exception ex)
        {
            StatusMessage = $"云盘/收藏加载失败：{ex.Message}";
            SongCountText = "0 首歌曲";
        }
        finally
        {
            IsLoading = false;
            RebuildPageItems();
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!CanLoadMore)
        {
            return;
        }

        IsLoadingMore = true;
        StatusMessage = "正在加载更多云盘/收藏";

        try
        {
            await LoadPageAsync(_currentPage + 1);
            StatusMessage = "云盘/收藏已同步";
            UpdateSongCountText();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载更多失败：{ex.Message}";
        }
        finally
        {
            IsLoadingMore = false;
            RebuildPageItems();
        }
    }

    [RelayCommand]
    private async Task PlaySongAsync(KugouSong song)
    {
        if (song is null)
        {
            return;
        }

        if (PlayerService.IsSameSong(song, PlayerService.Instance.CurrentSong))
        {
            PlayerService.Instance.TogglePlayPause();
            return;
        }
        PlayerService.Instance.AppendToQueue(new[] { song });
        await PlayerService.Instance.PlayQueueSongAsync(song);
    }

    [RelayCommand]
    private void AddSongToQueue(KugouSong song)
    {
        if (song is null) return;
        PlayerService.Instance.AppendToQueue(new[] { song });
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        if (Songs.Count > 0)
        {
            await PlayerService.Instance.PlayQueueAsync(Songs.ToList(), 0, "云盘/收藏", replaceQueue: true);
        }
    }

    private async Task LoadPageAsync(int page)
    {
        var result = await MusicService.Client.GetUserCloudTypedAsync(page: page, pageSize: PageSize);
        TotalCount = result.Total > 0 ? result.Total : Songs.Count + result.Items.Count;
        foreach (var song in result.Items)
        {
            Songs.Add(song);
        }

        _currentPage = page;
    }

    private void RebuildPageItems()
    {
        PageItems.Clear();
        PageItems.Add(CloudHeaderSection.Instance);
        foreach (var song in Songs)
        {
            PageItems.Add(song);
        }

        if (CanLoadMore)
        {
            PageItems.Add(CloudLoadMoreSection.Instance);
        }
    }

    private void UpdateSongCountText()
    {
        SongCountText = TotalCount > Songs.Count
            ? $"{Songs.Count}/{TotalCount} 首歌曲"
            : $"{Songs.Count} 首歌曲";
    }
}
