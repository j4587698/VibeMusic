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



    [ObservableProperty]
    private ObservableCollection<KugouYouthChannel> _youthChannels = new();

    [ObservableProperty]
    private bool _isChannelsLoading;

    [ObservableProperty]
    private string _channelsStatus = string.Empty;

    [RelayCommand]
    private async Task LoadChannelsAsync()
    {
        if (IsChannelsLoading) return;
        if (!MusicService.IsLoggedIn)
        {
            ChannelsStatus = "登录后可查看已订阅的 Youth 频道";
            return;
        }

        IsChannelsLoading = true;
        ChannelsStatus = "正在加载...";
        YouthChannels.Clear();

        try
        {
            var response = await MusicService.Client.YouthChannelAllAsync(page: 1, pageSize: 50);
            if (MusicService.TryGetResponseError(response, out var errorMessage))
            {
                ChannelsStatus = $"加载失败：{errorMessage}";
            }
            else
            {
                using var doc = response.TryParseJson();
                if (doc is null)
                {
                    ChannelsStatus = "响应解析失败";
                }
                else
                {
                    var items = ExtractYouthChannels(doc.RootElement);
                    foreach (var channel in items)
                    {
                        YouthChannels.Add(channel);
                    }
                    ChannelsStatus = YouthChannels.Count > 0
                        ? $"已订阅 {YouthChannels.Count} 个频道"
                        : "暂无订阅，可在官方酷狗概念版中订阅频道";
                }
            }
        }
        catch (System.Exception ex)
        {
            ChannelsStatus = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsChannelsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SubscribeChannelAsync(KugouYouthChannel channel)
    {
        if (channel is null || string.IsNullOrWhiteSpace(channel.Id)) return;
        try
        {
            await MusicService.Client.YouthChannelSubscribeAsync(channel.Id);
            channel.IsSubscribed = true;
            ChannelsStatus = $"已订阅「{channel.Name}」";
        }
        catch (System.Exception ex)
        {
            ChannelsStatus = $"订阅失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UnsubscribeChannelAsync(KugouYouthChannel channel)
    {
        if (channel is null || string.IsNullOrWhiteSpace(channel.Id)) return;
        try
        {
            await MusicService.Client.YouthChannelUnsubscribeAsync(channel.Id);
            channel.IsSubscribed = false;
            YouthChannels.Remove(channel);
            ChannelsStatus = $"已取消订阅「{channel.Name}」";
        }
        catch (System.Exception ex)
        {
            ChannelsStatus = $"取消订阅失败：{ex.Message}";
        }
    }

    private static List<KugouYouthChannel> ExtractYouthChannels(System.Text.Json.JsonElement root)
    {
        var result = new List<KugouYouthChannel>();
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return result;

        var data = root.TryGetProperty("data", out var value) ? value : root;
        var items = data.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Array => data,
            System.Text.Json.JsonValueKind.Object => TryGetArray(data, "list", "items", "channels"),
            _ => default
        };

        if (items.ValueKind != System.Text.Json.JsonValueKind.Array) return result;

        foreach (var item in items.EnumerateArray())
        {
            var channel = KugouYouthChannel.FromJson(item);
            if (channel is not null) result.Add(channel);
        }
        return result;
    }

    private static System.Text.Json.JsonElement TryGetArray(System.Text.Json.JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                return arr;
        }
        return default;
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

public sealed class KugouYouthChannel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CoverUrl { get; init; } = string.Empty;
    public int SongCount { get; init; }
    public int SubCount { get; init; }
    public bool IsSubscribed { get; set; } = true;

    public static KugouYouthChannel? FromJson(System.Text.Json.JsonElement item)
    {
        if (item.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
        var id = ReadString(item, "global_collection_id", "collection_id", "id", "channel_id");
        if (string.IsNullOrWhiteSpace(id)) return null;

        var cover = ReadString(item, "cover", "img", "pic", "collection_cover", "cover_url");
        if (!string.IsNullOrWhiteSpace(cover) && !cover.StartsWith("http"))
        {
            cover = null;
        }

        return new KugouYouthChannel
        {
            Id = id,
            Name = ReadString(item, "collection_name", "name", "title", "channel_name") ?? "未命名频道",
            CoverUrl = cover ?? string.Empty,
            SongCount = ReadInt(item, "song_count", "count", "music_count", "audio_count"),
            SubCount = ReadInt(item, "sub_count", "subscribe_count", "fans_count"),
            IsSubscribed = true
        };
    }

    private static string? ReadString(System.Text.Json.JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var val = prop.GetString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        return null;
    }

    private static int ReadInt(System.Text.Json.JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var val))
                return val;
        }
        return 0;
    }
}
