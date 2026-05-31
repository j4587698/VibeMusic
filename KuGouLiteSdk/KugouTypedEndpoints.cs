using System.Text.Json;
using System.Net.Http;

namespace KuGou.Lite;

public sealed partial class KugouLiteClient
{
    public async Task<KugouListResult<KugouSong>> SearchSongsTypedAsync(string keywords, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await SearchAsync(keywords, KugouSearchType.Song, page, pageSize, cancellationToken).ConfigureAwait(false);
        var result = KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Search));
        if (result.Items.Count > 0 || !IsSearchParameterError(response))
        {
            return result;
        }

        var fallbackResponse = await SearchPublicSongsAsync(keywords, page, pageSize, cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(fallbackResponse, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Search));
    }

    public async Task<KugouListResult<KugouPlaylist>> SearchPlaylistsTypedAsync(string keywords, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await SearchAsync(keywords, KugouSearchType.Special, page, pageSize, cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapPlaylist);
    }

    public async Task<KugouListResult<KugouAlbum>> SearchAlbumsTypedAsync(string keywords, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await SearchAsync(keywords, KugouSearchType.Album, page, pageSize, cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapAlbum);
    }

    public async Task<KugouListResult<KugouArtist>> SearchArtistsTypedAsync(string keywords, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await SearchAsync(keywords, KugouSearchType.Author, page, pageSize, cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapArtist);
    }

    public async Task<KugouListResult<KugouVideo>> SearchMvsTypedAsync(string keywords, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await SearchAsync(keywords, KugouSearchType.Mv, page, pageSize, cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapVideo(item) ?? new KugouVideo());
    }

    public async Task<KugouListResult<KugouSong>> GetNewSongsTypedAsync(CancellationToken cancellationToken = default)
    {
        var body = D(
            ("rank_id", 21608),
            ("userid", ReadUserIdOrZero()),
            ("page", 1),
            ("pagesize", 30),
            ("tags", Array.Empty<object>()));
        var response = await InvokeRouteAsync("/top/song", D(("__body", body)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Top));
    }

    public async Task<KugouListResult<KugouSong>> GetEverydayRecommendTypedAsync(CancellationToken cancellationToken = default)
    {
        var body = D(
            ("platform", "android"),
            ("userid", ReadUserIdOrZero()));
        var response = await InvokeRouteAsync("/recommend/songs", D(("__body", body)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Top));
    }

    public async Task<KugouListResult<KugouSong>> GetPersonalFmTypedAsync(IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var values = parameters is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(parameters, StringComparer.Ordinal);
        var clientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var userId = ReadLongValue(values, "userid", ReadUserIdOrZero());
        var token = ReadTextValue(values, "token") ?? CookieStore.Get("token") ?? string.Empty;
        var vipType = ReadTextValue(values, "vip_type") ?? ReadTextValue(values, "vipType") ?? CookieStore.Get("vip_type") ?? string.Empty;
        var body = D(
            ("appid", KugouConstants.LiteAppId),
            ("clienttime", clientTime),
            ("mid", CookieStore.Get("KUGOU_API_MID") ?? string.Empty),
            ("action", ReadTextValue(values, "action") ?? "play"),
            ("recommend_source_locked", 0),
            ("song_pool_id", ReadIntValue(values, "song_pool_id", 0)),
            ("callerid", 0),
            ("m_type", 1),
            ("platform", ReadTextValue(values, "platform") ?? "ios"),
            ("area_code", 1),
            ("remain_songcnt", ReadIntValue(values, "remain_songcnt", 0)),
            ("clientver", KugouConstants.LiteClientVersion),
            ("is_overplay", ReadBoolValue(values, "is_overplay") ? 1 : 0),
            ("mode", ReadTextValue(values, "mode") ?? "normal"),
            ("fakem", "ca981cfc583a4c37f28d2d49000013c16a0a"),
            ("key", KugouCrypto.SignParamsKey(clientTime)));

        if (userId > 0)
        {
            body["userid"] = userId;
            body["kguid"] = userId;
        }

        if (!string.IsNullOrWhiteSpace(token) && token != "0")
        {
            body["token"] = token;
        }

        if (!string.IsNullOrWhiteSpace(vipType) && vipType != "0")
        {
            body["vip_type"] = vipType;
        }

        CopyIfPresent(values, body, "hash");
        CopyIfPresent(values, body, "songid");
        CopyIfPresent(values, body, "playtime");

        var response = await InvokeRouteAsync("/personal/fm", D(("__body", body)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Top));
    }

    public async Task<KugouTypedResult<KugouPlaylist>> GetPlaylistDetailTypedAsync(string ids, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/playlist/detail", D(("ids", ids)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToFirstResult(response, KugouJsonMapper.MapPlaylist);
    }

    public async Task<KugouListResult<KugouSong>> GetPlaylistTracksTypedAsync(string id, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await GetPlaylistTracksAsync(id, page, pageSize, cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Playlist));
    }

    public async Task<KugouListResult<KugouSong>> GetPlaylistTracksNewTypedAsync(string listid, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var body = D(
            ("listid", listid),
            ("userid", CookieStore.Get("userid") ?? "0"),
            ("area_code", 1),
            ("show_relate_goods", 0),
            ("pagesize", pageSize),
            ("allplatform", 1),
            ("show_cover", 1),
            ("type", 0),
            ("token", CookieStore.Get("token") ?? "0"),
            ("page", page));
        var response = await InvokeRouteAsync("/playlist/track/all/new", D(("__body", body)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Playlist));
    }

    public async Task<KugouListResult<KugouPlaylist>> GetUserPlaylistsTypedAsync(int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var userId = ReadUserIdOrZero();
        var token = CookieStore.Get("token") ?? string.Empty;
        var body = D(
            ("userid", userId),
            ("token", token),
            ("total_ver", 979),
            ("type", 2),
            ("page", page),
            ("pagesize", pageSize));
        var response = await InvokeRouteAsync(
            "/user/playlist",
            D(("plat", 1), ("userid", userId), ("token", token), ("__body", body)),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapPlaylist);
    }

    public async Task<KugouListResult<KugouPlaylist>> GetTopPlaylistsTypedAsync(int categoryId = 0, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var clientTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var body = D(
            ("appid", KugouConstants.LiteAppId),
            ("mid", CookieStore.Get("KUGOU_API_MID") ?? string.Empty),
            ("clientver", KugouConstants.LiteClientVersion),
            ("platform", "android"),
            ("clienttime", clientTime),
            ("userid", ReadUserIdOrZero()),
            ("module_id", 1),
            ("page", page),
            ("pagesize", pageSize),
            ("key", KugouCrypto.SignParamsKey(clientTime)),
            ("special_recommend", D(
                ("withtag", 1),
                ("withsong", 1),
                ("sort", 1),
                ("ugc", 1),
                ("is_selected", 0),
                ("withrecommend", 1),
                ("area_code", 1),
                ("categoryid", categoryId))),
            ("req_multi", 1),
            ("retrun_min", 5),
            ("return_special_falg", 1));
        var response = await RawGatewayAsync(
            "/v2/special_recommend",
            HttpMethod.Post,
            D(("clienttime", clientTime)),
            body,
            new Dictionary<string, string> { ["x-router"] = "specialrec.service.kugou.com" },
            cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapPlaylist);
    }

    public async Task<KugouListResult<KugouRank>> GetRankListTypedAsync(CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/rank/list", cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapRank);
    }

    public async Task<KugouListResult<KugouRank>> GetRankTopTypedAsync(CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/rank/top", cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapRank);
    }

    public async Task<KugouListResult<KugouSong>> GetRankSongsTypedAsync(long rankId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var response = await GetRankAudioAsync(rankId, page: page, pageSize: pageSize, cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Rank));
    }

    public async Task<KugouTypedResult<KugouAlbum>> GetAlbumDetailTypedAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAlbumDetailAsync(id, cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToFirstResult(response, KugouJsonMapper.MapAlbum);
    }

    public async Task<KugouListResult<KugouSong>> GetAlbumSongsTypedAsync(long id, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await GetAlbumSongsAsync(id, page, pageSize, cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Album));
    }

    public async Task<KugouTypedResult<KugouArtist>> GetArtistDetailTypedAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/artist/detail", D(("id", id)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToFirstResult(response, KugouJsonMapper.MapArtist);
    }

    public async Task<KugouListResult<KugouArtist>> GetArtistListTypedAsync(int type = 0, int sexType = 0, int musician = 0, int hotSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/artist/lists", D(("type", type), ("sextype", sexType), ("showtype", 2), ("musician", musician), ("hotsize", hotSize)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapArtist);
    }

    public async Task<KugouListResult<KugouSong>> GetArtistSongsTypedAsync(long id, int page = 1, int pageSize = 200, string sort = "hot", CancellationToken cancellationToken = default)
    {
        var clientTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var body = D(
            ("appid", KugouConstants.LiteAppId),
            ("clientver", KugouConstants.LiteClientVersion),
            ("mid", CookieStore.Get("KUGOU_API_MID") ?? string.Empty),
            ("clienttime", clientTime),
            ("key", KugouCrypto.SignParamsKey(clientTime)),
            ("author_id", id),
            ("pagesize", pageSize),
            ("page", page),
            ("sort", string.Equals(sort, "hot", StringComparison.OrdinalIgnoreCase) ? 1 : 2),
            ("area_code", "all"));
        var response = await RawGatewayAsync(
            "https://openapi.kugou.com/kmr/v1/audio_group/author",
            HttpMethod.Post,
            body: body,
            headers: new Dictionary<string, string>
            {
                ["x-router"] = "openapi.kugou.com",
                ["kg-tid"] = "220"
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Artist, id.ToString()));
    }

    public async Task<KugouListResult<KugouAlbum>> GetArtistAlbumsTypedAsync(long id, int page = 1, int pageSize = 30, string sort = "hot", CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/artist/albums", D(("id", id), ("page", page), ("pagesize", pageSize), ("sort", sort)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, KugouJsonMapper.MapAlbum);
    }

    public async Task<KugouListResult<KugouVideo>> GetArtistVideosTypedAsync(long id, int page = 1, int pageSize = 30, string tag = "all", CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/artist/videos", D(("id", id), ("page", page), ("pagesize", pageSize), ("tag", tag)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapVideo(item) ?? new KugouVideo());
    }

    public async Task<KugouListResult<KugouComment>> GetMusicCommentsTypedAsync(string mixSongId, int page = 1, int pageSize = 30, bool showClassify = false, bool showHotwordList = false, int sort = 2, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/comment/music", D(("mixsongid", mixSongId), ("page", page), ("pagesize", pageSize), ("show_classify", showClassify ? 1 : 0), ("show_hotword_list", showHotwordList ? 1 : 0), ("sort", sort)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToCommentListResult(response);
    }

    public async Task<KugouListResult<KugouComment>> GetPlaylistCommentsTypedAsync(string id, int page = 1, int pageSize = 30, bool showClassify = false, bool showHotwordList = false, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/comment/playlist", D(("id", id), ("page", page), ("pagesize", pageSize), ("show_classify", showClassify ? 1 : 0), ("show_hotword_list", showHotwordList ? 1 : 0)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToCommentListResult(response);
    }

    public async Task<KugouListResult<KugouComment>> GetAlbumCommentsTypedAsync(string id, int page = 1, int pageSize = 30, bool showClassify = false, bool showHotwordList = false, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/comment/album", D(("id", id), ("page", page), ("pagesize", pageSize), ("show_classify", showClassify ? 1 : 0), ("show_hotword_list", showHotwordList ? 1 : 0)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToCommentListResult(response);
    }

    public async Task<KugouListResult<KugouComment>> GetFloorCommentsTypedAsync(string specialId, string tid, string? mixSongId = null, string? code = null, string? resourceType = null, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/comment/floor", D(("special_id", specialId), ("tid", tid), ("mixsongid", mixSongId), ("code", code), ("resource_type", resourceType), ("page", page), ("pagesize", pageSize)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToCommentListResult(response);
    }

    public async Task<KugouTypedResult<KugouUser>> GetUserDetailTypedAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetUserDetailAsync(cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToFirstResult(response, KugouJsonMapper.MapUser);
    }

    public async Task<KugouListResult<KugouSong>> GetUserHistoryTypedAsync(string? bp = null, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/user/history", D(("bp", bp)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.History));
    }

    public async Task<KugouListResult<KugouSong>> GetUserCloudTypedAsync(int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/user/cloud", D(("page", page), ("pagesize", pageSize)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return KugouJsonMapper.ToListResult(response, item => KugouJsonMapper.MapSong(item, KugouSongMapKind.Cloud));
    }

    public async Task<KugouTypedResult<KugouAudioUrl>> GetSongUrlTypedAsync(string hash, long albumId = 0, long albumAudioId = 0, string quality = "128", bool freePart = false, string? ppageId = null, CancellationToken cancellationToken = default)
    {
        var response = await GetSongUrlAsync(hash, albumId, albumAudioId, quality, freePart, ppageId, cancellationToken).ConfigureAwait(false);
        var audioUrl = KugouJsonMapper.MapAudioUrl(response);
        if (!string.IsNullOrWhiteSpace(audioUrl.Url))
        {
            return new KugouTypedResult<KugouAudioUrl>(audioUrl, response);
        }

        if (!string.Equals(ppageId, "356753938", StringComparison.Ordinal))
        {
            var ppageResponse = await GetSongUrlAsync(hash, albumId, albumAudioId, quality, freePart, "356753938", cancellationToken).ConfigureAwait(false);
            var ppageAudioUrl = KugouJsonMapper.MapAudioUrl(ppageResponse);
            if (!string.IsNullOrWhiteSpace(ppageAudioUrl.Url))
            {
                return new KugouTypedResult<KugouAudioUrl>(ppageAudioUrl, ppageResponse);
            }
        }

        if (albumAudioId > 0)
        {
            var newResponse = await GetSongUrlNewAsync(hash, albumAudioId, freePart, cancellationToken).ConfigureAwait(false);
            var newAudioUrl = KugouJsonMapper.MapAudioUrl(newResponse);
            if (!string.IsNullOrWhiteSpace(newAudioUrl.Url))
            {
                return new KugouTypedResult<KugouAudioUrl>(newAudioUrl, newResponse);
            }
        }

        var publicResponse = await GetPublicSongInfoAsync(hash, albumId, cancellationToken).ConfigureAwait(false);
        var publicAudioUrl = KugouJsonMapper.MapAudioUrl(publicResponse);
        return new KugouTypedResult<KugouAudioUrl>(publicAudioUrl, publicResponse);
    }

    public async Task<KugouResolvedAudioSource> ResolveSongAudioUrlTypedAsync(
        KugouSong song,
        string preferredQuality = "128",
        bool compatibilityMode = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(song.Hash))
        {
            throw new ArgumentException("Song hash is required to resolve audio url.", nameof(song));
        }

        var albumId = long.TryParse(song.AlbumId, out var parsedAlbumId) ? parsedAlbumId : 0;
        var relateGoods = song.RelateGoods;
        try
        {
            var privilege = await GetSongPrivilegeLiteTypedAsync(song.Hash, albumId > 0 ? albumId : null, cancellationToken).ConfigureAwait(false);
            if (privilege.Items.Count > 0)
            {
                relateGoods = privilege.Items;
            }
        }
        catch
        {
            // Keep search/list metadata if privilege lite is unavailable.
        }

        async Task<KugouTypedResult<KugouAudioUrl>> TrySongUrlAsync(string hash, string quality, string? ppageId = null)
        {
            var response = await GetSongUrlAsync(hash, albumId, song.MixSongId, quality, ppageId: ppageId, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new KugouTypedResult<KugouAudioUrl>(KugouJsonMapper.MapAudioUrl(response), response);
        }

        foreach (var quality in GetAudioQualityCandidates(preferredQuality, compatibilityMode))
        {
            var matched = relateGoods.FirstOrDefault(item => DoesRelateGoodMatchQuality(item, quality) && !string.IsNullOrWhiteSpace(item.Hash));
            if (matched?.Hash is null)
            {
                continue;
            }

            var qualityResult = await TrySongUrlAsync(matched.Hash, quality).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(qualityResult.Data?.Url))
            {
                return new KugouResolvedAudioSource(qualityResult.Data!.Url, quality, "none", qualityResult.Raw);
            }
        }

        KugouTypedResult<KugouAudioUrl>? lastResult = null;
        if (compatibilityMode)
        {
            lastResult = await TrySongUrlAsync(song.Hash, string.Empty).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(lastResult.Data?.Url))
            {
                return new KugouResolvedAudioSource(lastResult.Data!.Url, ResolveEffectiveAudioQuality(relateGoods, preferredQuality, compatibilityMode), "none", lastResult.Raw);
            }
        }

        lastResult = await TrySongUrlAsync(song.Hash, string.Empty, "356753938").ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(lastResult.Data?.Url))
        {
            return new KugouResolvedAudioSource(lastResult.Data!.Url, ResolveEffectiveAudioQuality(relateGoods, preferredQuality, compatibilityMode), "none", lastResult.Raw);
        }

        if (song.MixSongId > 0)
        {
            var newResponse = await GetSongUrlNewAsync(song.Hash, song.MixSongId, cancellationToken: cancellationToken).ConfigureAwait(false);
            lastResult = new KugouTypedResult<KugouAudioUrl>(KugouJsonMapper.MapAudioUrl(newResponse), newResponse);
            if (!string.IsNullOrWhiteSpace(lastResult.Data?.Url))
            {
                return new KugouResolvedAudioSource(lastResult.Data!.Url, ResolveEffectiveAudioQuality(relateGoods, preferredQuality, compatibilityMode), "none", lastResult.Raw);
            }
        }

        var publicResponse = await GetPublicSongInfoAsync(song.Hash, cancellationToken: cancellationToken).ConfigureAwait(false);
        lastResult = new KugouTypedResult<KugouAudioUrl>(KugouJsonMapper.MapAudioUrl(publicResponse), publicResponse);
        return new KugouResolvedAudioSource(lastResult.Data?.Url ?? string.Empty, ResolveEffectiveAudioQuality(relateGoods, preferredQuality, compatibilityMode), "none", lastResult.Raw);
    }

    public async Task<KugouListResult<KugouSongRelateGood>> GetSongPrivilegeLiteTypedAsync(string hash, long? albumId = null, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/privilege/lite", D(("hash", hash), ("album_id", albumId)), cancellationToken: cancellationToken).ConfigureAwait(false);
        var items = KugouJsonMapper.MapRelateGoodsFromPrivilege(response);
        return new KugouListResult<KugouSongRelateGood>(items, items.Count, response);
    }

    public async Task<KugouTypedResult<KugouAudioUrl>> GetCloudSongUrlTypedAsync(string hash, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/user/cloud/url", D(("hash", hash)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return new KugouTypedResult<KugouAudioUrl>(KugouJsonMapper.MapAudioUrl(response), response);
    }

    public async Task<KugouTypedResult<KugouVideo>> GetVideoDetailTypedAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/video/detail", D(("id", id)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return new KugouTypedResult<KugouVideo>(KugouJsonMapper.MapVideo(response), response);
    }

    public async Task<KugouTypedResult<string>> GetVideoUrlTypedAsync(string hash, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/video/url", D(("hash", hash)), cancellationToken: cancellationToken).ConfigureAwait(false);
        return new KugouTypedResult<string>(KugouJsonMapper.ExtractVideoUrl(response, hash), response);
    }

    public async Task<KugouListResult<KugouVideoSource>> GetVideoPrivilegeTypedAsync(string hash, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRouteAsync("/video/privilege", D(("hash", hash)), cancellationToken: cancellationToken).ConfigureAwait(false);
        var sources = KugouJsonMapper.MapVideoSourcesFromPrivilege(response);
        return new KugouListResult<KugouVideoSource>(sources, sources.Count, response);
    }

    private static bool IsSearchParameterError(KugouResponse response)
    {
        using var doc = response.TryParseJson();
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (doc.RootElement.TryGetProperty("error_code", out var errorCode) &&
            errorCode.ValueKind == JsonValueKind.Number &&
            errorCode.TryGetInt32(out var code) &&
            code == 152)
        {
            return true;
        }

        if (doc.RootElement.TryGetProperty("error_msg", out var errorMessage) &&
            errorMessage.ValueKind == JsonValueKind.String &&
            string.Equals(errorMessage.GetString(), "Parameter Error", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static void CopyIfPresent(IDictionary<string, object?> from, IDictionary<string, object?> to, string key)
    {
        if (from.TryGetValue(key, out var value) && value is not null)
        {
            to[key] = value;
        }
    }

    private static string? ReadTextValue(IDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out var value) && value is not null
            ? KugouCrypto.FormatValueForQuery(value)
            : null;
    }

    private static int ReadIntValue(IDictionary<string, object?> values, string key, int fallback)
    {
        return int.TryParse(ReadTextValue(values, key), out var value) ? value : fallback;
    }

    private static long ReadLongValue(IDictionary<string, object?> values, string key, long fallback)
    {
        return long.TryParse(ReadTextValue(values, key), out var value) ? value : fallback;
    }

    private static bool ReadBoolValue(IDictionary<string, object?> values, string key)
    {
        var text = ReadTextValue(values, key);
        return text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
    }

    private long ReadUserIdOrZero()
    {
        return long.TryParse(CookieStore.Get("userid"), out var userId) ? userId : 0;
    }

    private static IReadOnlyList<string> GetAudioQualityCandidates(string preferredQuality, bool compatibilityMode)
    {
        var order = new[] { "128", "320", "flac", "high" };
        var normalized = order.Contains(preferredQuality, StringComparer.OrdinalIgnoreCase) ? preferredQuality.ToLowerInvariant() : "128";
        if (!compatibilityMode)
        {
            return new[] { normalized };
        }

        var index = Array.FindIndex(order, item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return order.Take(index + 1).Reverse().ToArray();
    }

    private static string ResolveEffectiveAudioQuality(IReadOnlyList<KugouSongRelateGood> relateGoods, string preferredQuality, bool compatibilityMode)
    {
        foreach (var quality in GetAudioQualityCandidates(preferredQuality, compatibilityMode))
        {
            if (quality == "128" || relateGoods.Any(item => DoesRelateGoodMatchQuality(item, quality)))
            {
                return quality;
            }
        }

        return relateGoods.LastOrDefault(item => !string.IsNullOrWhiteSpace(item.Quality))?.Quality ?? "128";
    }

    private static bool DoesRelateGoodMatchQuality(KugouSongRelateGood item, string quality)
    {
        if (quality == "128")
        {
            return true;
        }

        var normalizedQuality = (item.Quality ?? string.Empty).Trim().ToLowerInvariant();
        return quality switch
        {
            "320" => normalizedQuality is "320" or "hq" || item.Level == 4,
            "flac" => normalizedQuality is "flac" or "sq" || item.Level == 5,
            "high" => normalizedQuality is "high" or "hires" or "hi-res" or "res" || item.Level == 6,
            _ => string.Equals(normalizedQuality, quality, StringComparison.OrdinalIgnoreCase)
        };
    }
}