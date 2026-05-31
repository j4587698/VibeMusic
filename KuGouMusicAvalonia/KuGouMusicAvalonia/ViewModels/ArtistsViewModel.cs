using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class ArtistCategoryOption
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Type { get; init; }
    public int SexType { get; init; }
    public int Musician { get; init; }
}

public sealed class ArtistsHeaderSection
{
    public static ArtistsHeaderSection Instance { get; } = new();

    private ArtistsHeaderSection()
    {
    }
}

public sealed class ArtistsFooterSection
{
    public static ArtistsFooterSection Instance { get; } = new();

    private ArtistsFooterSection()
    {
    }
}

public sealed class ArtistCardRow
{
    public ArtistCardRow(IReadOnlyList<KugouArtist> artists)
    {
        Artists = artists;
    }

    public IReadOnlyList<KugouArtist> Artists { get; }
}

public partial class ArtistsViewModel : ViewModelBase
{
    private const int ArtistFetchSize = 200;
    private const int ArtistBatchSize = 60;
    private readonly List<KugouArtist> _allArtists = new();
    private int _cardsPerRow = 4;

    [ObservableProperty]
    private ObservableCollection<KugouArtist> _artists = new();

    public ObservableCollection<object> PageItems { get; } = new();

    public ObservableCollection<ArtistCategoryOption> ArtistCategories { get; } = new();

    [ObservableProperty]
    private ArtistCategoryOption? _selectedArtistCategory;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private string _statusMessage = "正在加载歌手分类";

    public string SelectedCategoryName => SelectedArtistCategory?.Name ?? "全部";

    public bool CanLoadMore => !IsLoading && !IsLoadingMore && Artists.Count < _allArtists.Count;

    public string VisibleArtistCountText => _allArtists.Count > 0 ? $"{Artists.Count}/{_allArtists.Count}" : "0";

    public ArtistsViewModel()
    {
        ArtistCategories.Add(new ArtistCategoryOption { Name = "全部", Description = "热门歌手", Type = 0, SexType = 0 });
        ArtistCategories.Add(new ArtistCategoryOption { Name = "华语", Description = "华语流行", Type = 1, SexType = 0 });
        ArtistCategories.Add(new ArtistCategoryOption { Name = "欧美", Description = "欧美热歌", Type = 2, SexType = 0 });
        ArtistCategories.Add(new ArtistCategoryOption { Name = "日韩", Description = "日韩歌手", Type = 3, SexType = 0 });
        ArtistCategories.Add(new ArtistCategoryOption { Name = "男歌手", Description = "热门男声", Type = 0, SexType = 1 });
        ArtistCategories.Add(new ArtistCategoryOption { Name = "女歌手", Description = "热门女声", Type = 0, SexType = 2 });
        ArtistCategories.Add(new ArtistCategoryOption { Name = "组合", Description = "乐队组合", Type = 0, SexType = 3 });
        ArtistCategories.Add(new ArtistCategoryOption { Name = "音乐人", Description = "入驻音乐人", Type = 0, SexType = 0, Musician = 3 });
        SelectedArtistCategory = ArtistCategories.FirstOrDefault();
        RebuildPageItems();
        _ = LoadArtistsAsync();
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

    partial void OnSelectedArtistCategoryChanged(ArtistCategoryOption? value)
    {
        OnPropertyChanged(nameof(SelectedCategoryName));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        NotifyLoadMoreState();
    }

    partial void OnIsLoadingMoreChanged(bool value)
    {
        NotifyLoadMoreState();
    }

    [RelayCommand]
    private async Task LoadArtistsAsync()
    {
        if (IsLoading || SelectedArtistCategory is null) return;
        IsLoading = true;
        Artists.Clear();
        _allArtists.Clear();
        RebuildPageItems();
        NotifyLoadMoreState();
        StatusMessage = $"正在加载 {SelectedArtistCategory.Name} 歌手";

        try
        {
            var category = SelectedArtistCategory;
            var result = await MusicService.Client.GetArtistListTypedAsync(category.Type, category.SexType, category.Musician, hotSize: ArtistFetchSize);
            _allArtists.AddRange(result.Items
                .Where(artist => artist.Id > 0)
                .GroupBy(artist => artist.Id)
                .Select(group => group.First()));

            AddNextArtistBatch();

            StatusMessage = BuildStatusMessage();
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"歌手分类加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyLoadMoreState();
        }
    }

    [RelayCommand]
    private void LoadMore()
    {
        if (!CanLoadMore) return;
        IsLoadingMore = true;

        try
        {
            AddNextArtistBatch();
            StatusMessage = BuildStatusMessage();
        }
        finally
        {
            IsLoadingMore = false;
            NotifyLoadMoreState();
        }
    }

    [RelayCommand]
    private async Task SelectArtistCategoryAsync(ArtistCategoryOption category)
    {
        if (category is null) return;
        SelectedArtistCategory = category;
        await LoadArtistsAsync();
    }

    [RelayCommand]
    private void OpenArtist(KugouArtist artist)
    {
        if (artist is null) return;
        ShellNavigationService.Instance.OpenArtistDetail(artist);
    }

    private void AddNextArtistBatch()
    {
        var nextArtists = _allArtists.Skip(Artists.Count).Take(ArtistBatchSize).ToArray();
        foreach (var artist in nextArtists)
        {
            Artists.Add(artist);
        }

        RebuildPageItems();
        NotifyLoadMoreState();
    }

    private void RebuildPageItems()
    {
        PageItems.Clear();
        PageItems.Add(ArtistsHeaderSection.Instance);

        foreach (var row in Artists.Chunk(_cardsPerRow))
        {
            PageItems.Add(new ArtistCardRow(row));
        }

        PageItems.Add(ArtistsFooterSection.Instance);
    }

    private string BuildStatusMessage()
    {
        if (SelectedArtistCategory is null)
        {
            return "请选择歌手分类";
        }

        return _allArtists.Count > 0
            ? $"{SelectedArtistCategory.Name} · 已显示 {Artists.Count}/{_allArtists.Count} 位歌手"
            : "这个分类暂时没有歌手";
    }

    private void NotifyLoadMoreState()
    {
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(VisibleArtistCountText));
    }
}