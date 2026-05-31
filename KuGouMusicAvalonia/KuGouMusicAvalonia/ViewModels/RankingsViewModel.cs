using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public partial class RankingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<KugouRank> _featuredRanks = new();

    [ObservableProperty]
    private ObservableCollection<KugouRank> _ranks = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedRankName))]
    [NotifyPropertyChangedFor(nameof(SelectedRankPic))]
    private KugouRank? _selectedRank;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "正在同步酷狗排行榜";

    public string SelectedRankName => SelectedRank?.Name ?? "排行榜";

    public string SelectedRankPic => SelectedRank?.Pic ?? string.Empty;

    public RankingsViewModel()
    {
        _ = LoadRankingsAsync();
    }

    [RelayCommand]
    private async Task LoadRankingsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var topTask = MusicService.Client.GetRankTopTypedAsync();
            var allTask = MusicService.Client.GetRankListTypedAsync();
            await Task.WhenAll(topTask, allTask);

            FeaturedRanks.Clear();
            foreach (var rank in topTask.Result.Items.Where(item => item.Id > 0))
            {
                FeaturedRanks.Add(rank);
            }

            Ranks.Clear();
            var seen = new HashSet<int>();
            foreach (var rank in topTask.Result.Items.Concat(allTask.Result.Items).Where(item => item.Id > 0))
            {
                if (seen.Add(rank.Id))
                {
                    Ranks.Add(rank);
                }
            }

            StatusMessage = Ranks.Count > 0 ? $"已同步 {Ranks.Count} 个榜单" : "暂时没有拿到排行榜";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"排行榜加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectRank(KugouRank rank)
    {
        if (rank is null) return;
        SelectedRank = rank;
        ShellNavigationService.Instance.OpenRankingDetail(rank);
    }

}