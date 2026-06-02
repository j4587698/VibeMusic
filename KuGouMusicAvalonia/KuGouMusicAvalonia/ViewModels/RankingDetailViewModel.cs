using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class RankingDetailHeaderSection
{
    public static RankingDetailHeaderSection Instance { get; } = new();

    private RankingDetailHeaderSection()
    {
    }
}

public partial class RankingDetailViewModel : ViewModelBase
{
    public KugouRank Rank { get; }

    [ObservableProperty]
    private ObservableCollection<KugouSong> _songs = new();

    public ObservableCollection<object> PageItems { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "正在加载榜单歌曲";

    public string Title => Rank.Name;
    public string CoverUrl => Rank.Pic;
    public string Subtitle => string.IsNullOrWhiteSpace(Rank.UpdateFrequency) ? Rank.RankTypeName ?? "排行榜" : Rank.UpdateFrequency!;
    public string MetaText => string.IsNullOrWhiteSpace(Rank.Group) ? "实时榜单" : Rank.Group!;

    public RankingDetailViewModel(KugouRank rank)
    {
        Rank = rank;
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
        StatusMessage = "正在加载榜单歌曲";

        try
        {
            var result = await MusicService.Client.GetRankSongsTypedAsync(Rank.Id, page: 1, pageSize: 120);
            foreach (var song in result.Items)
            {
                Songs.Add(song);
            }

            StatusMessage = Songs.Count > 0 ? $"已加载 {Songs.Count} 首歌曲" : "该榜单暂时没有歌曲";
            RebuildPageItems();
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"榜单歌曲加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        ShellNavigationService.Instance.Navigate("NavRankings");
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
        if (PlayerService.IsSameSong(song, PlayerService.Instance.CurrentSong))
        {
            PlayerService.Instance.TogglePlayPause();
            return;
        }
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
        PageItems.Add(RankingDetailHeaderSection.Instance);
        foreach (var song in Songs)
        {
            PageItems.Add(song);
        }
    }
}