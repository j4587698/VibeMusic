using KuGou.Lite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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

    public static bool TryGetResponseError(KugouResponse response, out string errorMessage, out int errorCode, out string eventId)
    {
        errorCode = 0;
        eventId = string.Empty;

        using var doc = response.TryParseJson();
        var root = doc?.RootElement;
        var message = root is { ValueKind: JsonValueKind.Object }
            ? ReadResponseMessage(root.Value)
            : null;

        if (root is { ValueKind: JsonValueKind.Object })
        {
            if (root.Value.TryGetProperty("data", out var dataObj) && dataObj.ValueKind == JsonValueKind.Object)
            {
                if (dataObj.TryGetProperty("eventid", out var ev) && ev.ValueKind == JsonValueKind.String)
                {
                    eventId = ev.GetString() ?? string.Empty;
                }
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            errorMessage = string.IsNullOrWhiteSpace(message)
                ? $"HTTP {(int)response.StatusCode}"
                : $"HTTP {(int)response.StatusCode}：{message}";
            return true;
        }

        if (root is not { ValueKind: JsonValueKind.Object })
        {
            errorMessage = "响应格式无效";
            return true;
        }

        errorCode = ReadResponseInt(root.Value, "error_code") ?? ReadResponseInt(root.Value, "errcode") ?? 0;
        var status = ReadResponseInt(root.Value, "status");
        if (errorCode == 0 && status is not 0)
        {
            errorMessage = string.Empty;
            return false;
        }

        errorMessage = !string.IsNullOrWhiteSpace(message)
            ? message
            : errorCode == 20002
                ? "请先登录或重新登录"
                : $"API 错误 {errorCode}";
        return true;
    }

    private static string? ReadResponseMessage(JsonElement root)
    {
        foreach (var name in new[] { "error_msg", "msg", "message", "error" })
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static int? ReadResponseInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

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

    public static bool MinimizeToTrayOnClose
    {
        get => LocalMusicStore.Instance.GetBoolSetting("MinimizeToTrayOnClose", true);
        set => LocalMusicStore.Instance.SetSetting("MinimizeToTrayOnClose", value);
    }

    public static bool HasPromptedMinimizeToTray
    {
        get => LocalMusicStore.Instance.GetBoolSetting("HasPromptedMinimizeToTray", false);
        set => LocalMusicStore.Instance.SetSetting("HasPromptedMinimizeToTray", value);
    }

    public static string DownloadDirectory
    {
        get => PlatformStoragePaths.NormalizeDownloadDirectory(LocalMusicStore.Instance.GetStringSetting(LocalSettingKeys.DownloadDirectory, PlatformStoragePaths.DefaultDownloadDirectory));
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.DownloadDirectory, PlatformStoragePaths.NormalizeDownloadDirectory(value));
    }

    public static string DefaultPlaybackQuality
    {
        get => LocalMusicStore.Instance.GetStringSetting(LocalSettingKeys.DefaultPlaybackQuality, "无损 FLAC");
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.DefaultPlaybackQuality, string.IsNullOrWhiteSpace(value) ? "无损 FLAC" : value);
    }

    public static bool PreferKrc
    {
        get => LocalMusicStore.Instance.GetBoolSetting(LocalSettingKeys.PreferKrc, true);
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.PreferKrc, value);
    }

    public static bool FloatingLyricsOpen
    {
        get => LocalMusicStore.Instance.GetBoolSetting(LocalSettingKeys.FloatingLyricsOpen, false);
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.FloatingLyricsOpen, value);
    }

    public static bool FloatingLyricsLocked
    {
        get => LocalMusicStore.Instance.GetBoolSetting(LocalSettingKeys.FloatingLyricsLocked, false);
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.FloatingLyricsLocked, value);
    }

    public static bool FloatingLyricsCompactMode
    {
        get => LocalMusicStore.Instance.GetBoolSetting(LocalSettingKeys.FloatingLyricsCompactMode, false);
        set => LocalMusicStore.Instance.SetSetting(LocalSettingKeys.FloatingLyricsCompactMode, value);
    }

    public static double FloatingLyricsFontSize
    {
        get
        {
            var value = LocalMusicStore.Instance.GetStringSetting(
                LocalSettingKeys.FloatingLyricsFontSize,
                FloatingLyricsService.DefaultFontSize.ToString(CultureInfo.InvariantCulture));

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Clamp(parsed, FloatingLyricsService.MinFontSize, FloatingLyricsService.MaxFontSize)
                : FloatingLyricsService.DefaultFontSize;
        }
        set
        {
            var normalized = Math.Clamp(value, FloatingLyricsService.MinFontSize, FloatingLyricsService.MaxFontSize);
            LocalMusicStore.Instance.SetSetting(LocalSettingKeys.FloatingLyricsFontSize, normalized.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>桌面歌词窗口上次的屏幕坐标（物理像素），未记录时为 null。</summary>
    public static int? FloatingLyricsWindowX
    {
        get => GetNullableIntSetting(LocalSettingKeys.FloatingLyricsWindowX);
        set => SetNullableIntSetting(LocalSettingKeys.FloatingLyricsWindowX, value);
    }

    public static int? FloatingLyricsWindowY
    {
        get => GetNullableIntSetting(LocalSettingKeys.FloatingLyricsWindowY);
        set => SetNullableIntSetting(LocalSettingKeys.FloatingLyricsWindowY, value);
    }

    /// <summary>桌面歌词窗口上次的宽度（逻辑像素），未记录时为 null。</summary>
    public static double? FloatingLyricsWindowWidth
    {
        get
        {
            var raw = LocalMusicStore.Instance.GetStringSetting(LocalSettingKeys.FloatingLyricsWindowWidth, string.Empty);
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : null;
        }
        set => LocalMusicStore.Instance.SetSetting(
            LocalSettingKeys.FloatingLyricsWindowWidth,
            value is { } width && width > 0 ? width.ToString(CultureInfo.InvariantCulture) : string.Empty);
    }

    /// <summary>Android 悬浮歌词上次的位置（物理像素，相对屏幕左上角），未记录时为 null。</summary>
    public static int? FloatingLyricsOverlayX
    {
        get => GetNullableIntSetting(LocalSettingKeys.FloatingLyricsOverlayX);
        set => SetNullableIntSetting(LocalSettingKeys.FloatingLyricsOverlayX, value);
    }

    public static int? FloatingLyricsOverlayY
    {
        get => GetNullableIntSetting(LocalSettingKeys.FloatingLyricsOverlayY);
        set => SetNullableIntSetting(LocalSettingKeys.FloatingLyricsOverlayY, value);
    }

    /// <summary>是否已同意免责声明。</summary>
    public static bool DisclaimerAccepted
    {
        get => LocalMusicStore.Instance.DisclaimerAccepted;
        set => LocalMusicStore.Instance.SetDisclaimerAccepted(value);
    }

    private static int? GetNullableIntSetting(string key)
    {
        var raw = LocalMusicStore.Instance.GetStringSetting(key, string.Empty);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static void SetNullableIntSetting(string key, int? value)
    {
        LocalMusicStore.Instance.SetSetting(
            key,
            value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }

    public static string DefaultPlaybackQualityValue => DefaultPlaybackQuality switch
    {
        "标准 128k" => "128",
        "高品 320k" => "320",
        "无损 FLAC" => "flac",
        "高解析 High" => "high",
        _ => "flac"
    };

    public static void ClearSession()
    {
        LocalMusicStore.Instance.ClearCookies();
        LocalMusicStore.Instance.ClearUserProfileCache();
        _client?.Dispose();
        _client = CreateClient(loadSavedCookies: false);
    }

    public static void ClearAllData()
    {
        LocalMusicStore.Instance.ClearAllData();
        _client?.Dispose();
        _client = CreateClient(loadSavedCookies: false);
        PlayerService.Instance.ClearQueue();
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
