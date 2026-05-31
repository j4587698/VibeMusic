using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public partial class DiscoverViewModel : ViewModelBase
{
    private readonly Queue<KugouSong> _personalFmBuffer = new();
    private readonly HashSet<string> _personalFmSeenKeys = new(StringComparer.OrdinalIgnoreCase);
    private bool _lastPersonalFmUsedFallback;

    [ObservableProperty]
    private ObservableCollection<KugouSong> _newSongs = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PersonalFmTitle))]
    [NotifyPropertyChangedFor(nameof(PersonalFmArtist))]
    [NotifyPropertyChangedFor(nameof(PersonalFmCoverUrl))]
    [NotifyPropertyChangedFor(nameof(PersonalFmAlbumText))]
    [NotifyPropertyChangedFor(nameof(HasPersonalFmSong))]
    private KugouSong? _personalFmSong;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isPersonalFmLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeroTitle))]
    [NotifyPropertyChangedFor(nameof(HeroArtist))]
    [NotifyPropertyChangedFor(nameof(HeroCoverUrl))]
    [NotifyPropertyChangedFor(nameof(HasFeaturedSong))]
    private KugouSong? _featuredSong;

    [ObservableProperty]
    private string _statusMessage = "正在连接酷狗音乐";

    [ObservableProperty]
    private string _personalFmStatusMessage = "正在加载猜你喜欢";

    public string HeroTitle => FeaturedSong?.Title ?? "还没有歌曲";

    public string HeroArtist => FeaturedSong?.Artist ?? "从新歌速递、猜你喜欢或歌手分类开始";

    public string HeroCoverUrl => FeaturedSong?.CoverUrl ?? string.Empty;

    public bool HasFeaturedSong => FeaturedSong is not null;

    public PlayerService Player => PlayerService.Instance;

    public string PersonalFmTitle => PersonalFmSong?.Title ?? "猜你喜欢 FM";

    public string PersonalFmArtist => PersonalFmSong?.Artist ?? "点击播放开始调频";

    public string PersonalFmCoverUrl => PersonalFmSong?.CoverUrl ?? string.Empty;

    public string PersonalFmAlbumText => string.IsNullOrWhiteSpace(PersonalFmSong?.AlbumName) ? "私人电台" : PersonalFmSong!.AlbumName!;

    public bool HasPersonalFmSong => PersonalFmSong is not null;

    public DiscoverViewModel()
    {
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            await LoadNewSongsAsync();
            await LoadPersonalFmAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadNewSongsAsync()
    {
        try
        {
            var result = await MusicService.Client.GetNewSongsTypedAsync();
            NewSongs.Clear();
            if (result?.Items != null)
            {
                foreach (var song in result.Items)
                {
                    NewSongs.Add(song);
                }
            }

            FeaturedSong = NewSongs.FirstOrDefault();
            StatusMessage = NewSongs.Count > 0 ? "已加载新歌速递" : "新歌速递暂时没有内容";
        }
        catch (System.Exception ex)
        {
            NewSongs.Clear();
            FeaturedSong = null;
            StatusMessage = $"新歌速递加载失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadPersonalFmAsync()
    {
        await LoadNextPersonalFmSongAsync(PersonalFmSong);
    }

    [RelayCommand]
    private async Task PlaySongAsync(KugouSong song)
    {
        if (song == null) return;
        var index = NewSongs.IndexOf(song);
        await PlayerService.Instance.PlayQueueAsync(NewSongs.ToList(), index < 0 ? 0 : index, "新歌速递", replaceQueue: true);
    }

    [RelayCommand]
    private async Task PlayFeaturedSongAsync()
    {
        if (FeaturedSong is not null)
        {
            var index = NewSongs.IndexOf(FeaturedSong);
            await PlayerService.Instance.PlayQueueAsync(NewSongs.ToList(), index < 0 ? 0 : index, "新歌速递", replaceQueue: true);
        }
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        if (NewSongs.Count == 0) return;
        await PlayerService.Instance.PlayQueueAsync(NewSongs.ToList(), 0, "新歌速递", replaceQueue: true);
    }

    [RelayCommand]
    private void QueueAll()
    {
        if (NewSongs.Count == 0) return;
        var added = PlayerService.Instance.AppendToQueue(NewSongs.ToList(), "新歌速递");
        StatusMessage = added > 0 ? $"已加入 {added} 首到播放队列" : "这些歌曲已在播放队列中";
    }

    [RelayCommand]
    private async Task StartPersonalFmAsync()
    {
        if (Player.IsRadioMode && Player.HasSong)
        {
            Player.TogglePlayPause();
            return;
        }

        var song = PersonalFmSong ?? await LoadNextPersonalFmSongAsync(null);
        if (song is null)
        {
            PersonalFmStatusMessage = "FM 暂时没有可播放内容";
            return;
        }

        await Player.StartRadioAsync(song, LoadNextPersonalFmSongAsync, "猜你喜欢 FM");
        PersonalFmStatusMessage = "FM 正在播放";
    }

    [RelayCommand]
    private async Task NextPersonalFmAsync()
    {
        if (Player.IsRadioMode)
        {
            await Player.SkipNextAsync();
            return;
        }

        var song = await LoadNextPersonalFmSongAsync(PersonalFmSong);
        if (song is not null)
        {
            await Player.StartRadioAsync(song, LoadNextPersonalFmSongAsync, "猜你喜欢 FM");
        }
    }

    private async Task<KugouSong?> LoadNextPersonalFmSongAsync(KugouSong? previousSong)
    {
        if (IsPersonalFmLoading)
        {
            PersonalFmStatusMessage = "FM 正在调频，请稍候";
            return null;
        }

        IsPersonalFmLoading = true;
        PersonalFmStatusMessage = "正在调频";

        try
        {
            var song = TakeBufferedPersonalFmSong(previousSong);
            if (song is null)
            {
                var songs = await FetchPersonalFmSongsAsync(previousSong);
                song = SelectPersonalFmSong(songs, previousSong);
            }

            if (song is null)
            {
                PersonalFmStatusMessage = "FM 暂时没有下一首";
                return null;
            }

            PersonalFmSong = song;
            PersonalFmStatusMessage = _lastPersonalFmUsedFallback ? "私人 FM 暂无新内容，已切到每日推荐电台" : "FM 已就绪";
            return song;
        }
        catch (Exception ex)
        {
            PersonalFmStatusMessage = $"FM 加载失败：{ex.Message}";
            return null;
        }
        finally
        {
            IsPersonalFmLoading = false;
        }
    }

    private async Task<IReadOnlyList<KugouSong>> FetchPersonalFmSongsAsync(KugouSong? previousSong)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["mode"] = "normal",
            ["song_pool_id"] = 0,
            ["remain_songcnt"] = 0
        };

        if (previousSong is not null)
        {
            if (!string.IsNullOrWhiteSpace(previousSong.Hash))
            {
                parameters["hash"] = previousSong.Hash;
            }

            var songId = !string.IsNullOrWhiteSpace(previousSong.SongId) ? previousSong.SongId : previousSong.MixSongId > 0 ? previousSong.MixSongId.ToString() : null;
            if (!string.IsNullOrWhiteSpace(songId))
            {
                parameters["songid"] = songId;
            }

            parameters["playtime"] = Math.Max(0, (int)Math.Round(Player.Progress));
            parameters["is_overplay"] = 1;
        }

        try
        {
            var result = await MusicService.Client.GetPersonalFmTypedAsync(parameters);
            var items = result.Items.Where(IsUsablePersonalFmSong).ToArray();
            if (items.Length > 0)
            {
                _lastPersonalFmUsedFallback = false;
                return items;
            }
        }
        catch
        {
            // Fall back to daily recommendations when personal FM is unavailable.
        }

        var everyday = await MusicService.Client.GetEverydayRecommendTypedAsync();
        _lastPersonalFmUsedFallback = true;
        return everyday.Items.Where(IsUsablePersonalFmSong).ToArray();
    }

    private KugouSong? SelectPersonalFmSong(IReadOnlyList<KugouSong> songs, KugouSong? previousSong)
    {
        _personalFmBuffer.Clear();

        var candidates = songs
            .Where(song => !IsSamePersonalFmSong(song, previousSong))
            .GroupBy(GetPersonalFmSongKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var selected = candidates.FirstOrDefault(song => !_personalFmSeenKeys.Contains(GetPersonalFmSongKey(song)));
        if (selected is null && candidates.Length > 0)
        {
            _personalFmSeenKeys.Clear();
            selected = candidates[0];
        }

        if (selected is null)
        {
            return null;
        }

        MarkPersonalFmSongSeen(selected);
        foreach (var song in candidates.Where(song => !IsSamePersonalFmSong(song, selected)))
        {
            _personalFmBuffer.Enqueue(song);
        }

        return selected;
    }

    private KugouSong? TakeBufferedPersonalFmSong(KugouSong? previousSong)
    {
        while (_personalFmBuffer.Count > 0)
        {
            var song = _personalFmBuffer.Dequeue();
            if (IsSamePersonalFmSong(song, previousSong))
            {
                continue;
            }

            var key = GetPersonalFmSongKey(song);
            if (_personalFmSeenKeys.Contains(key))
            {
                continue;
            }

            MarkPersonalFmSongSeen(song);
            return song;
        }

        return null;
    }

    private static bool IsUsablePersonalFmSong(KugouSong song)
    {
        return song is not null && (!string.IsNullOrWhiteSpace(song.Hash) || song.MixSongId > 0 || !string.IsNullOrWhiteSpace(song.Id));
    }

    private static bool IsSamePersonalFmSong(KugouSong? left, KugouSong? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(GetPersonalFmSongKey(left), GetPersonalFmSongKey(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPersonalFmSongKey(KugouSong song)
    {
        if (!string.IsNullOrWhiteSpace(song.Hash))
        {
            return song.Hash;
        }

        if (song.MixSongId > 0)
        {
            return song.MixSongId.ToString();
        }

        return song.Id ?? song.Title;
    }

    private void MarkPersonalFmSongSeen(KugouSong song)
    {
        _personalFmSeenKeys.Add(GetPersonalFmSongKey(song));
    }
}