using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class RankingsHeaderSection
{
    public static RankingsHeaderSection Instance { get; } = new();

    private RankingsHeaderSection()
    {
    }
}

public sealed class RankingsAllSection
{
    public static RankingsAllSection Instance { get; } = new();

    private RankingsAllSection()
    {
    }
}

public sealed class RankingsFooterSection
{
    public static RankingsFooterSection Instance { get; } = new();

    private RankingsFooterSection()
    {
    }
}

public sealed class FeaturedRankRow
{
    public FeaturedRankRow(IReadOnlyList<KugouRank> ranks)
    {
        Ranks = ranks;
    }

    public IReadOnlyList<KugouRank> Ranks { get; }
}

public sealed class RankCardRow
{
    public RankCardRow(IReadOnlyList<KugouRank> ranks)
    {
        Ranks = ranks;
    }

    public IReadOnlyList<KugouRank> Ranks { get; }
}

public partial class RankingsViewModel : ViewModelBase
{
    private int _cardsPerRow = 4;

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

    public ObservableCollection<object> PageItems { get; } = new();

    public string SelectedRankName => SelectedRank?.Name ?? "排行榜";

    public string SelectedRankPic => SelectedRank?.Pic ?? string.Empty;

    public RankingsViewModel()
    {
        RebuildPageItems();
        _ = LoadRankingsAsync();
    }

    public void SetCardsPerRow(int cardsPerRow)
    {
        var normalizedCount = System.Math.Max(1, cardsPerRow);
        if (_cardsPerRow == normalizedCount)
        {
            return;
        }

        _cardsPerRow = normalizedCount;
        RebuildPageItems();
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

            FeaturedRanks = new ObservableCollection<KugouRank>(topTask.Result.Items.Where(item => item.Id > 0));

            var seen = new HashSet<int>();
            var combined = topTask.Result.Items.Concat(allTask.Result.Items)
                                                 .Where(item => item.Id > 0 && seen.Add(item.Id));
            Ranks = new ObservableCollection<KugouRank>(combined);
            RebuildPageItems();

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

    private void RebuildPageItems()
    {
        PageItems.Clear();
        PageItems.Add(RankingsHeaderSection.Instance);

        foreach (var row in FeaturedRanks.Chunk(_cardsPerRow))
        {
            PageItems.Add(new FeaturedRankRow(row));
        }

        PageItems.Add(RankingsAllSection.Instance);

        foreach (var row in Ranks.Chunk(_cardsPerRow))
        {
            PageItems.Add(new RankCardRow(row));
        }

        PageItems.Add(RankingsFooterSection.Instance);
    }
}
