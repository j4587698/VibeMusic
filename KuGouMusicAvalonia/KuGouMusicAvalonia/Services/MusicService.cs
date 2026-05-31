using KuGou.Lite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services;

public static class MusicService
{
    private static KugouLiteClient? _client;

    public static KugouLiteClient Client
    {
        get
        {
            if (_client == null)
            {
                _client = CreateClient();
            }
            return _client;
        }
    }

    public static void SaveSession()
    {
        LocalMusicStore.Instance.SaveCookies(new Dictionary<string, string>(Client.CookieStore.Snapshot(), StringComparer.Ordinal));
    }

    public static bool IsLoggedIn =>
        !string.IsNullOrWhiteSpace(Client.CookieStore.Get("token")) &&
        !string.IsNullOrWhiteSpace(Client.CookieStore.Get("userid")) &&
        !string.Equals(Client.CookieStore.Get("userid"), "0", StringComparison.Ordinal);

    public static Task<KugouResponse> CreatePlaylistAsync(string name, bool isPrivate = false, CancellationToken cancellationToken = default)
    {
        var userId = Client.CookieStore.Get("userid") ?? "0";
        var token = Client.CookieStore.Get("token") ?? string.Empty;
        var clientTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["userid"] = userId,
            ["token"] = token,
            ["total_ver"] = 0,
            ["name"] = name,
            ["type"] = 0,
            ["source"] = 1,
            ["is_pri"] = isPrivate ? 1 : 0,
            ["list_create_userid"] = 0,
            ["list_create_listid"] = 0,
            ["list_create_gid"] = string.Empty,
            ["from_shupinmv"] = 0
        };
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["last_time"] = clientTime,
            ["last_area"] = "gztx",
            ["userid"] = userId,
            ["token"] = token
        };

        return Client.RawGatewayAsync("/cloudlist.service/v5/add_list", HttpMethod.Post, parameters, body, cancellationToken: cancellationToken);
    }

    public static Task<KugouResponse> AddSongsToPlaylistAsync(int listId, IEnumerable<KugouSong> songs, CancellationToken cancellationToken = default)
    {
        var userId = Client.CookieStore.Get("userid") ?? "0";
        var token = Client.CookieStore.Get("token") ?? string.Empty;
        var clientTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var resources = songs
            .Where(song => song is not null && !string.IsNullOrWhiteSpace(song.Hash))
            .Select(song => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["number"] = 1,
                ["name"] = song.Title,
                ["hash"] = song.Hash,
                ["size"] = 0,
                ["sort"] = 0,
                ["timelen"] = 0,
                ["bitrate"] = 0,
                ["album_id"] = ParseLongOrZero(song.AlbumId),
                ["mixsongid"] = song.MixSongId
            })
            .ToArray();

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["userid"] = userId,
            ["token"] = token,
            ["listid"] = listId,
            ["list_ver"] = 0,
            ["type"] = 0,
            ["slow_upload"] = 1,
            ["scene"] = "false;null",
            ["data"] = resources
        };
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["last_time"] = clientTime,
            ["last_area"] = "gztx",
            ["userid"] = userId,
            ["token"] = token
        };

        return Client.RawGatewayAsync("/cloudlist.service/v6/add_song", HttpMethod.Post, parameters, body, cancellationToken: cancellationToken);
    }

    public static Task<KugouResponse> DeleteSongsFromPlaylistAsync(int listId, IEnumerable<long> fileIds, CancellationToken cancellationToken = default)
    {
        var userId = Client.CookieStore.Get("userid") ?? "0";
        var token = Client.CookieStore.Get("token") ?? string.Empty;
        var resources = fileIds
            .Where(fileId => fileId > 0)
            .Distinct()
            .Select(fileId => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["fileid"] = fileId
            })
            .ToArray();

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["listid"] = listId,
            ["userid"] = userId,
            ["data"] = resources,
            ["type"] = 0,
            ["token"] = token,
            ["list_ver"] = 0
        };

        return Client.RawGatewayAsync(
            "/v4/delete_songs",
            HttpMethod.Post,
            body: body,
            headers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["x-router"] = "cloudlist.service.kugou.com"
            },
            cancellationToken: cancellationToken);
    }

    public static async Task<KugouPlaylist?> GetFavoritePlaylistAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLoggedIn)
        {
            return null;
        }

        var result = await Client.GetUserPlaylistsTypedAsync(page: 1, pageSize: 80, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Items.FirstOrDefault(IsFavoritePlaylist) ?? result.Items.FirstOrDefault(playlist => playlist.IsDefault == true);
    }

    public static async Task<IReadOnlyList<KugouSong>> GetFavoriteSongsAsync(CancellationToken cancellationToken = default)
    {
        var playlist = await GetFavoritePlaylistAsync(cancellationToken).ConfigureAwait(false);
        var listId = ResolvePlaylistListId(playlist);
        if (listId <= 0)
        {
            return Array.Empty<KugouSong>();
        }

        var songs = new List<KugouSong>();
        const int pageSize = 100;
        for (var page = 1; page <= 20; page++)
        {
            var result = await Client.GetPlaylistTracksNewTypedAsync(listId.ToString(), page, pageSize, cancellationToken).ConfigureAwait(false);
            if (result.Items.Count == 0)
            {
                break;
            }

            songs.AddRange(result.Items);
            if (result.Total > 0 && page * pageSize >= result.Total)
            {
                break;
            }
        }

        return songs;
    }

    public static async Task AddSongToFavoritePlaylistAsync(KugouSong song, CancellationToken cancellationToken = default)
    {
        var playlist = await GetFavoritePlaylistAsync(cancellationToken).ConfigureAwait(false);
        var listId = ResolvePlaylistListId(playlist);
        if (listId <= 0)
        {
            throw new InvalidOperationException("没有找到酷狗账号的我喜欢歌单");
        }

        await AddSongsToPlaylistAsync(listId, new[] { song }, cancellationToken).ConfigureAwait(false);
    }

    public static async Task RemoveSongFromFavoritePlaylistAsync(KugouSong song, CancellationToken cancellationToken = default)
    {
        var playlist = await GetFavoritePlaylistAsync(cancellationToken).ConfigureAwait(false);
        var listId = ResolvePlaylistListId(playlist);
        if (listId <= 0)
        {
            throw new InvalidOperationException("没有找到酷狗账号的我喜欢歌单");
        }

        var favoriteSong = await FindSongInPlaylistAsync(listId, song, cancellationToken).ConfigureAwait(false);
        if (favoriteSong?.FileId is not long fileId || fileId <= 0)
        {
            return;
        }

        await DeleteSongsFromPlaylistAsync(listId, new[] { fileId }, cancellationToken).ConfigureAwait(false);
    }

    public static bool AutoReceiveVipBeforePlayback
    {
        get => LocalMusicStore.Instance.GetBoolSetting(LocalSettingKeys.AutoReceiveVipBeforePlayback, true);
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.AutoReceiveVipBeforePlayback, value);
    }

    public static string ThemeMode
    {
        get => LocalMusicStore.Instance.GetStringSetting(LocalSettingKeys.ThemeMode, "深色");
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.ThemeMode, string.IsNullOrWhiteSpace(value) ? "深色" : value);
    }

    public static bool StreamWhileDownloading
    {
        get => LocalMusicStore.Instance.GetBoolSetting(LocalSettingKeys.StreamWhileDownloading, true);
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.StreamWhileDownloading, value);
    }

    public static string DownloadDirectory
    {
        get => LocalMusicStore.Instance.GetStringSetting(LocalSettingKeys.DownloadDirectory, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.DownloadDirectory, string.IsNullOrWhiteSpace(value) ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) : value);
    }

    public static string DefaultPlaybackQuality
    {
        get => LocalMusicStore.Instance.GetStringSetting(LocalSettingKeys.DefaultPlaybackQuality, "标准 128k");
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.DefaultPlaybackQuality, string.IsNullOrWhiteSpace(value) ? "标准 128k" : value);
    }

    public static string DefaultPlaybackQualityValue => DefaultPlaybackQuality switch
    {
        "高品 320k" => "320",
        "无损 FLAC" => "flac",
        "高解析 High" => "high",
        _ => "128"
    };

    public static void ClearSession()
    {
        LocalMusicStore.Instance.ClearCookies();
        _client?.Dispose();
        _client = CreateClient(loadSavedCookies: false);
    }

    private static long ParseLongOrZero(string? value)
    {
        return long.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static async Task<KugouSong?> FindSongInPlaylistAsync(int listId, KugouSong target, CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        for (var page = 1; page <= 20; page++)
        {
            var result = await Client.GetPlaylistTracksNewTypedAsync(listId.ToString(), page, pageSize, cancellationToken).ConfigureAwait(false);
            var match = result.Items.FirstOrDefault(song => IsSameSong(song, target));
            if (match is not null)
            {
                return match;
            }

            if (result.Items.Count == 0 || result.Total > 0 && page * pageSize >= result.Total)
            {
                break;
            }
        }

        return null;
    }

    private static bool IsFavoritePlaylist(KugouPlaylist playlist)
    {
        return playlist.IsDefault == true ||
            playlist.Name.Contains("我喜欢", StringComparison.OrdinalIgnoreCase) ||
            playlist.Name.Contains("喜欢", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolvePlaylistListId(KugouPlaylist? playlist)
    {
        return playlist?.Listid ?? playlist?.OriginalId ?? playlist?.Id ?? 0;
    }

    private static bool IsSameSong(KugouSong left, KugouSong right)
    {
        if (!string.IsNullOrWhiteSpace(left.Hash) && !string.IsNullOrWhiteSpace(right.Hash) && string.Equals(left.Hash, right.Hash, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (left.MixSongId > 0 && right.MixSongId > 0 && left.MixSongId == right.MixSongId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(left.Id) && string.Equals(left.Id, right.Id, StringComparison.Ordinal);
    }

    private static KugouLiteClient CreateClient(bool loadSavedCookies = true)
    {
        var client = new KugouLiteClient();
        if (!loadSavedCookies)
        {
            return client;
        }

        foreach (var cookie in LocalMusicStore.Instance.LoadCookies())
        {
            client.CookieStore.Set(cookie.Key, cookie.Value);
        }

        return client;
    }
}
