using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace KuGou.Lite;

public sealed class KugouRouteRequestDto
{
    public Dictionary<string, string> Parameters { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Cookies { get; init; } = new(StringComparer.Ordinal);
    public string? BodyJson { get; init; }

    public static KugouRouteRequestDto Empty { get; } = new();

    public static KugouRouteRequestDto FromParameters(params (string Key, object? Value)[] parameters)
    {
        var dto = new KugouRouteRequestDto();
        foreach (var (key, value) in parameters)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            dto.Parameters[key] = KugouCrypto.FormatValueForQuery(value);
        }

        return dto;
    }
}

public class KugouRouteResponseDto
{
    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("error_code")]
    public int? ErrorCode { get; init; }

    [JsonPropertyName("err_code")]
    public int? ErrCode { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("msg")]
    public string? Msg { get; init; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }

    [JsonPropertyName("info")]
    public JsonElement? Info { get; init; }

    [JsonPropertyName("list")]
    public JsonElement? List { get; init; }

    [JsonPropertyName("lists")]
    public JsonElement? Lists { get; init; }

    [JsonPropertyName("total")]
    public int? Total { get; init; }

    [JsonPropertyName("count")]
    public int? Count { get; init; }

    [JsonPropertyName("counts")]
    public int? Counts { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record KugouRouteDtoDefinition(
    string Route,
    string Description,
    string Method,
    Type ResponseType,
    JsonTypeInfo ResponseTypeInfo);

public sealed record KugouRouteDtoResult(
    KugouRouteDtoDefinition Definition,
    KugouRouteResponseDto Response,
    int HttpStatusCode,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<string> Cookies);

public static class KugouRouteDtoCatalog
{
    public static IReadOnlyList<KugouRouteDtoDefinition> All { get; } = KugouApiCatalog.All
        .Select(item => new KugouRouteDtoDefinition(
            item.Route,
            item.Description,
            item.Method,
            GetResponseTypeInfo(item.Route).Type,
            GetResponseTypeInfo(item.Route)))
        .ToArray();

    private static readonly Lazy<IReadOnlyDictionary<string, KugouRouteDtoDefinition>> ByRoute = new(
        () => All.ToDictionary(item => item.Route, StringComparer.OrdinalIgnoreCase));

    public static KugouRouteDtoDefinition Get(string route)
    {
        if (ByRoute.Value.TryGetValue(route, out var definition))
        {
            return definition;
        }

        throw new ArgumentOutOfRangeException(nameof(route), route, "Unknown KuGou DTO route.");
    }

    public static bool TryGet(string route, out KugouRouteDtoDefinition? definition) =>
        ByRoute.Value.TryGetValue(route, out definition);

    public static JsonTypeInfo GetResponseTypeInfo(string route) => route.ToLowerInvariant() switch
    {
        "/ai/recommend" => KugouJsonSerializerContext.Default.KugouAiRecommendResponseDto,
        "/album" => KugouJsonSerializerContext.Default.KugouAlbumResponseDto,
        "/album/detail" => KugouJsonSerializerContext.Default.KugouAlbumDetailResponseDto,
        "/album/shop" => KugouJsonSerializerContext.Default.KugouAlbumShopResponseDto,
        "/album/songs" => KugouJsonSerializerContext.Default.KugouAlbumSongsResponseDto,
        "/artist/albums" => KugouJsonSerializerContext.Default.KugouArtistAlbumsResponseDto,
        "/artist/audios" => KugouJsonSerializerContext.Default.KugouArtistAudiosResponseDto,
        "/artist/detail" => KugouJsonSerializerContext.Default.KugouArtistDetailResponseDto,
        "/artist/follow" => KugouJsonSerializerContext.Default.KugouArtistFollowResponseDto,
        "/artist/follow/newsongs" => KugouJsonSerializerContext.Default.KugouArtistFollowNewsongsResponseDto,
        "/artist/honour" => KugouJsonSerializerContext.Default.KugouArtistHonourResponseDto,
        "/artist/lists" => KugouJsonSerializerContext.Default.KugouArtistListsResponseDto,
        "/artist/unfollow" => KugouJsonSerializerContext.Default.KugouArtistUnfollowResponseDto,
        "/artist/videos" => KugouJsonSerializerContext.Default.KugouArtistVideosResponseDto,
        "/audio" => KugouJsonSerializerContext.Default.KugouAudioResponseDto,
        "/audio/accompany/matching" => KugouJsonSerializerContext.Default.KugouAudioAccompanyMatchingResponseDto,
        "/audio/ktv/total" => KugouJsonSerializerContext.Default.KugouAudioKtvTotalResponseDto,
        "/audio/related" => KugouJsonSerializerContext.Default.KugouAudioRelatedResponseDto,
        "/brush" => KugouJsonSerializerContext.Default.KugouBrushResponseDto,
        "/captcha/sent" => KugouJsonSerializerContext.Default.KugouCaptchaSentResponseDto,
        "/comment/album" => KugouJsonSerializerContext.Default.KugouCommentAlbumResponseDto,
        "/comment/count" => KugouJsonSerializerContext.Default.KugouCommentCountResponseDto,
        "/comment/floor" => KugouJsonSerializerContext.Default.KugouCommentFloorResponseDto,
        "/comment/music" => KugouJsonSerializerContext.Default.KugouCommentMusicResponseDto,
        "/comment/music/classify" => KugouJsonSerializerContext.Default.KugouCommentMusicClassifyResponseDto,
        "/comment/music/hotword" => KugouJsonSerializerContext.Default.KugouCommentMusicHotwordResponseDto,
        "/comment/playlist" => KugouJsonSerializerContext.Default.KugouCommentPlaylistResponseDto,
        "/everyday/friend" => KugouJsonSerializerContext.Default.KugouEverydayFriendResponseDto,
        "/everyday/history" => KugouJsonSerializerContext.Default.KugouEverydayHistoryResponseDto,
        "/everyday/recommend" => KugouJsonSerializerContext.Default.KugouEverydayRecommendResponseDto,
        "/everyday/style/recommend" => KugouJsonSerializerContext.Default.KugouEverydayStyleRecommendResponseDto,
        "/favorite/count" => KugouJsonSerializerContext.Default.KugouFavoriteCountResponseDto,
        "/fm/class" => KugouJsonSerializerContext.Default.KugouFmClassResponseDto,
        "/fm/image" => KugouJsonSerializerContext.Default.KugouFmImageResponseDto,
        "/fm/recommend" => KugouJsonSerializerContext.Default.KugouFmRecommendResponseDto,
        "/fm/songs" => KugouJsonSerializerContext.Default.KugouFmSongsResponseDto,
        "/images" => KugouJsonSerializerContext.Default.KugouImagesResponseDto,
        "/images/audio" => KugouJsonSerializerContext.Default.KugouImagesAudioResponseDto,
        "/ip" => KugouJsonSerializerContext.Default.KugouIpResponseDto,
        "/ip/dateil" => KugouJsonSerializerContext.Default.KugouIpDateilResponseDto,
        "/ip/playlist" => KugouJsonSerializerContext.Default.KugouIpPlaylistResponseDto,
        "/ip/zone" => KugouJsonSerializerContext.Default.KugouIpZoneResponseDto,
        "/ip/zone/home" => KugouJsonSerializerContext.Default.KugouIpZoneHomeResponseDto,
        "/kmr/audio/mv" => KugouJsonSerializerContext.Default.KugouKmrAudioMvResponseDto,
        "/krm/audio" => KugouJsonSerializerContext.Default.KugouKrmAudioResponseDto,
        "/lastest/songs/listen" => KugouJsonSerializerContext.Default.KugouLastestSongsListenResponseDto,
        "/login" => KugouJsonSerializerContext.Default.KugouLoginResponseDto,
        "/login/cellphone" => KugouJsonSerializerContext.Default.KugouLoginCellphoneResponseDto,
        "/login/device" => KugouJsonSerializerContext.Default.KugouLoginDeviceResponseDto,
        "/login/device/kick" => KugouJsonSerializerContext.Default.KugouLoginDeviceKickResponseDto,
        "/login/openplat" => KugouJsonSerializerContext.Default.KugouLoginOpenplatResponseDto,
        "/login/qr/check" => KugouJsonSerializerContext.Default.KugouLoginQrCheckResponseDto,
        "/login/qr/create" => KugouJsonSerializerContext.Default.KugouLoginQrCreateResponseDto,
        "/login/qr/key" => KugouJsonSerializerContext.Default.KugouLoginQrKeyResponseDto,
        "/login/token" => KugouJsonSerializerContext.Default.KugouLoginTokenResponseDto,
        "/login/wx/check" => KugouJsonSerializerContext.Default.KugouLoginWxCheckResponseDto,
        "/login/wx/create" => KugouJsonSerializerContext.Default.KugouLoginWxCreateResponseDto,
        "/longaudio/album/audios" => KugouJsonSerializerContext.Default.KugouLongaudioAlbumAudiosResponseDto,
        "/longaudio/album/detail" => KugouJsonSerializerContext.Default.KugouLongaudioAlbumDetailResponseDto,
        "/longaudio/daily/recommend" => KugouJsonSerializerContext.Default.KugouLongaudioDailyRecommendResponseDto,
        "/longaudio/rank/recommend" => KugouJsonSerializerContext.Default.KugouLongaudioRankRecommendResponseDto,
        "/longaudio/vip/recommend" => KugouJsonSerializerContext.Default.KugouLongaudioVipRecommendResponseDto,
        "/longaudio/week/recommend" => KugouJsonSerializerContext.Default.KugouLongaudioWeekRecommendResponseDto,
        "/lyric" => KugouJsonSerializerContext.Default.KugouLyricResponseDto,
        "/pc/diantai" => KugouJsonSerializerContext.Default.KugouPcDiantaiResponseDto,
        "/personal/fm" => KugouJsonSerializerContext.Default.KugouPersonalFmResponseDto,
        "/playhistory/upload" => KugouJsonSerializerContext.Default.KugouPlayhistoryUploadResponseDto,
        "/playlist/add" => KugouJsonSerializerContext.Default.KugouPlaylistAddResponseDto,
        "/playlist/del" => KugouJsonSerializerContext.Default.KugouPlaylistDelResponseDto,
        "/playlist/detail" => KugouJsonSerializerContext.Default.KugouPlaylistDetailResponseDto,
        "/playlist/effect" => KugouJsonSerializerContext.Default.KugouPlaylistEffectResponseDto,
        "/playlist/similar" => KugouJsonSerializerContext.Default.KugouPlaylistSimilarResponseDto,
        "/playlist/tags" => KugouJsonSerializerContext.Default.KugouPlaylistTagsResponseDto,
        "/playlist/track/all" => KugouJsonSerializerContext.Default.KugouPlaylistTrackAllResponseDto,
        "/playlist/track/all/new" => KugouJsonSerializerContext.Default.KugouPlaylistTrackAllNewResponseDto,
        "/playlist/tracks/add" => KugouJsonSerializerContext.Default.KugouPlaylistTracksAddResponseDto,
        "/playlist/tracks/del" => KugouJsonSerializerContext.Default.KugouPlaylistTracksDelResponseDto,
        "/privilege/lite" => KugouJsonSerializerContext.Default.KugouPrivilegeLiteResponseDto,
        "/rank/audio" => KugouJsonSerializerContext.Default.KugouRankAudioResponseDto,
        "/rank/info" => KugouJsonSerializerContext.Default.KugouRankInfoResponseDto,
        "/rank/list" => KugouJsonSerializerContext.Default.KugouRankListResponseDto,
        "/rank/top" => KugouJsonSerializerContext.Default.KugouRankTopResponseDto,
        "/rank/vol" => KugouJsonSerializerContext.Default.KugouRankVolResponseDto,
        "/recommend/songs" => KugouJsonSerializerContext.Default.KugouRecommendSongsResponseDto,
        "/register/dev" => KugouJsonSerializerContext.Default.KugouRegisterDevResponseDto,
        "/scene/audio/list" => KugouJsonSerializerContext.Default.KugouSceneAudioListResponseDto,
        "/scene/collection/list" => KugouJsonSerializerContext.Default.KugouSceneCollectionListResponseDto,
        "/scene/lists" => KugouJsonSerializerContext.Default.KugouSceneListsResponseDto,
        "/scene/lists/v2" => KugouJsonSerializerContext.Default.KugouSceneListsV2ResponseDto,
        "/scene/module" => KugouJsonSerializerContext.Default.KugouSceneModuleResponseDto,
        "/scene/module/info" => KugouJsonSerializerContext.Default.KugouSceneModuleInfoResponseDto,
        "/scene/music" => KugouJsonSerializerContext.Default.KugouSceneMusicResponseDto,
        "/scene/video/list" => KugouJsonSerializerContext.Default.KugouSceneVideoListResponseDto,
        "/search" => KugouJsonSerializerContext.Default.KugouSearchResponseDto,
        "/search/complex" => KugouJsonSerializerContext.Default.KugouSearchComplexResponseDto,
        "/search/default" => KugouJsonSerializerContext.Default.KugouSearchDefaultResponseDto,
        "/search/hot" => KugouJsonSerializerContext.Default.KugouSearchHotResponseDto,
        "/search/lyric" => KugouJsonSerializerContext.Default.KugouSearchLyricResponseDto,
        "/search/mixed" => KugouJsonSerializerContext.Default.KugouSearchMixedResponseDto,
        "/search/suggest" => KugouJsonSerializerContext.Default.KugouSearchSuggestResponseDto,
        "/server/now" => KugouJsonSerializerContext.Default.KugouServerNowResponseDto,
        "/sheet/detail" => KugouJsonSerializerContext.Default.KugouSheetDetailResponseDto,
        "/sheet/explore" => KugouJsonSerializerContext.Default.KugouSheetExploreResponseDto,
        "/sheet/rank" => KugouJsonSerializerContext.Default.KugouSheetRankResponseDto,
        "/sheet/song" => KugouJsonSerializerContext.Default.KugouSheetSongResponseDto,
        "/sheet/tags" => KugouJsonSerializerContext.Default.KugouSheetTagsResponseDto,
        "/singer/list" => KugouJsonSerializerContext.Default.KugouSingerListResponseDto,
        "/song/climax" => KugouJsonSerializerContext.Default.KugouSongClimaxResponseDto,
        "/song/ranking" => KugouJsonSerializerContext.Default.KugouSongRankingResponseDto,
        "/song/ranking/filter" => KugouJsonSerializerContext.Default.KugouSongRankingFilterResponseDto,
        "/song/url" => KugouJsonSerializerContext.Default.KugouSongUrlResponseDto,
        "/song/url/new" => KugouJsonSerializerContext.Default.KugouSongUrlNewResponseDto,
        "/theme/music" => KugouJsonSerializerContext.Default.KugouThemeMusicResponseDto,
        "/theme/music/detail" => KugouJsonSerializerContext.Default.KugouThemeMusicDetailResponseDto,
        "/theme/playlist" => KugouJsonSerializerContext.Default.KugouThemePlaylistResponseDto,
        "/theme/playlist/track" => KugouJsonSerializerContext.Default.KugouThemePlaylistTrackResponseDto,
        "/top/album" => KugouJsonSerializerContext.Default.KugouTopAlbumResponseDto,
        "/top/card" => KugouJsonSerializerContext.Default.KugouTopCardResponseDto,
        "/top/card/youth" => KugouJsonSerializerContext.Default.KugouTopCardYouthResponseDto,
        "/top/ip" => KugouJsonSerializerContext.Default.KugouTopIpResponseDto,
        "/top/playlist" => KugouJsonSerializerContext.Default.KugouTopPlaylistResponseDto,
        "/top/song" => KugouJsonSerializerContext.Default.KugouTopSongResponseDto,
        "/user/cloud" => KugouJsonSerializerContext.Default.KugouUserCloudResponseDto,
        "/user/cloud/url" => KugouJsonSerializerContext.Default.KugouUserCloudUrlResponseDto,
        "/user/detail" => KugouJsonSerializerContext.Default.KugouUserDetailResponseDto,
        "/user/follow" => KugouJsonSerializerContext.Default.KugouUserFollowResponseDto,
        "/user/follow/message" => KugouJsonSerializerContext.Default.KugouUserFollowMessageResponseDto,
        "/user/history" => KugouJsonSerializerContext.Default.KugouUserHistoryResponseDto,
        "/user/listen" => KugouJsonSerializerContext.Default.KugouUserListenResponseDto,
        "/user/playlist" => KugouJsonSerializerContext.Default.KugouUserPlaylistResponseDto,
        "/user/video/collect" => KugouJsonSerializerContext.Default.KugouUserVideoCollectResponseDto,
        "/user/video/love" => KugouJsonSerializerContext.Default.KugouUserVideoLoveResponseDto,
        "/user/vip/detail" => KugouJsonSerializerContext.Default.KugouUserVipDetailResponseDto,
        "/video/detail" => KugouJsonSerializerContext.Default.KugouVideoDetailResponseDto,
        "/video/privilege" => KugouJsonSerializerContext.Default.KugouVideoPrivilegeResponseDto,
        "/video/url" => KugouJsonSerializerContext.Default.KugouVideoUrlResponseDto,
        "/youth/channel/all" => KugouJsonSerializerContext.Default.KugouYouthChannelAllResponseDto,
        "/youth/channel/amway" => KugouJsonSerializerContext.Default.KugouYouthChannelAmwayResponseDto,
        "/youth/channel/detail" => KugouJsonSerializerContext.Default.KugouYouthChannelDetailResponseDto,
        "/youth/channel/similar" => KugouJsonSerializerContext.Default.KugouYouthChannelSimilarResponseDto,
        "/youth/channel/song" => KugouJsonSerializerContext.Default.KugouYouthChannelSongResponseDto,
        "/youth/channel/song/detail" => KugouJsonSerializerContext.Default.KugouYouthChannelSongDetailResponseDto,
        "/youth/channel/sub" => KugouJsonSerializerContext.Default.KugouYouthChannelSubResponseDto,
        "/youth/day/vip" => KugouJsonSerializerContext.Default.KugouYouthDayVipResponseDto,
        "/youth/day/vip/upgrade" => KugouJsonSerializerContext.Default.KugouYouthDayVipUpgradeResponseDto,
        "/youth/dynamic" => KugouJsonSerializerContext.Default.KugouYouthDynamicResponseDto,
        "/youth/dynamic/recent" => KugouJsonSerializerContext.Default.KugouYouthDynamicRecentResponseDto,
        "/youth/listen/song" => KugouJsonSerializerContext.Default.KugouYouthListenSongResponseDto,
        "/youth/month/vip/record" => KugouJsonSerializerContext.Default.KugouYouthMonthVipRecordResponseDto,
        "/youth/union/vip" => KugouJsonSerializerContext.Default.KugouYouthUnionVipResponseDto,
        "/youth/user/song" => KugouJsonSerializerContext.Default.KugouYouthUserSongResponseDto,
        "/youth/vip" => KugouJsonSerializerContext.Default.KugouYouthVipResponseDto,
        "/yueku" => KugouJsonSerializerContext.Default.KugouYuekuResponseDto,
        "/yueku/banner" => KugouJsonSerializerContext.Default.KugouYuekuBannerResponseDto,
        "/yueku/fm" => KugouJsonSerializerContext.Default.KugouYuekuFmResponseDto,
        _ => KugouJsonSerializerContext.Default.KugouRouteResponseDto
    };
}

public sealed partial class KugouLiteClient
{
    public async Task<KugouRouteDtoResult> InvokeRouteDtoAsync(
        string route,
        KugouRouteRequestDto? requestDto = null,
        CancellationToken cancellationToken = default)
    {
        var definition = KugouRouteDtoCatalog.Get(route);
        requestDto ??= KugouRouteRequestDto.Empty;

        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in requestDto.Parameters)
        {
            if (item.Value is not null)
            {
                parameters[item.Key] = item.Value;
            }
        }

        foreach (var item in requestDto.Headers)
        {
            if (item.Value is not null)
            {
                parameters[$"header:{item.Key}"] = item.Value;
            }
        }

        foreach (var item in requestDto.Cookies)
        {
            if (item.Value is not null)
            {
                parameters[$"cookie:{item.Key}"] = item.Value;
            }
        }

        var catalogDefinition = KugouApiCatalog.Get(route);
        var bodyJson = requestDto.BodyJson;
        if (bodyJson is null && catalogDefinition.PayloadMode is KugouPayloadMode.Body or KugouPayloadMode.Both)
        {
            bodyJson = JsonSerializer.Serialize(requestDto.Parameters, KugouJsonSerializerContext.Default.DictionaryStringString);
        }

        var response = await InvokeRouteAsync(route, parameters, bodyJson, cancellationToken).ConfigureAwait(false);
        var dto = JsonSerializer.Deserialize(response.BodyText, definition.ResponseTypeInfo) as KugouRouteResponseDto
            ?? new KugouRouteResponseDto();

        return new KugouRouteDtoResult(definition, dto, response.StatusCodeNumber, response.Headers, response.Cookies);
    }

    public async Task<TResponse> InvokeRouteDtoAsync<TResponse>(
        string route,
        KugouRouteRequestDto? requestDto,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken = default)
        where TResponse : KugouRouteResponseDto, new()
    {
        requestDto ??= KugouRouteRequestDto.Empty;
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in requestDto.Parameters)
        {
            if (item.Value is not null)
            {
                parameters[item.Key] = item.Value;
            }
        }

        foreach (var item in requestDto.Headers)
        {
            if (item.Value is not null)
            {
                parameters[$"header:{item.Key}"] = item.Value;
            }
        }

        foreach (var item in requestDto.Cookies)
        {
            if (item.Value is not null)
            {
                parameters[$"cookie:{item.Key}"] = item.Value;
            }
        }

        var catalogDefinition = KugouApiCatalog.Get(route);
        var bodyJson = requestDto.BodyJson;
        if (bodyJson is null && catalogDefinition.PayloadMode is KugouPayloadMode.Body or KugouPayloadMode.Both)
        {
            bodyJson = JsonSerializer.Serialize(requestDto.Parameters, KugouJsonSerializerContext.Default.DictionaryStringString);
        }

        var response = await InvokeRouteAsync(route, parameters, bodyJson, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(response.BodyText, responseTypeInfo) ?? new TResponse();
    }
}

public static class KugouResponseDtoExtensions
{
    public static TResponse ToDto<TResponse>(this KugouResponse response, JsonTypeInfo<TResponse> responseTypeInfo)
        where TResponse : KugouRouteResponseDto, new()
    {
        return JsonSerializer.Deserialize(response.BodyText, responseTypeInfo) ?? new TResponse();
    }
}

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(KugouRouteRequestDto))]
[JsonSerializable(typeof(KugouRouteResponseDto))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(KugouParameterDefinition))]
[JsonSerializable(typeof(KugouApiParameterInfo))]
[JsonSerializable(typeof(KugouApiParameterInfo[]))]
[JsonSerializable(typeof(KugouSongRelateGood))]
[JsonSerializable(typeof(KugouSongArtist))]
[JsonSerializable(typeof(KugouSong))]
[JsonSerializable(typeof(KugouPlaylist))]
[JsonSerializable(typeof(KugouAlbum))]
[JsonSerializable(typeof(KugouArtist))]
[JsonSerializable(typeof(KugouRank))]
[JsonSerializable(typeof(KugouCommentLike))]
[JsonSerializable(typeof(KugouComment))]
[JsonSerializable(typeof(KugouUser))]
[JsonSerializable(typeof(KugouVideoAuthor))]
[JsonSerializable(typeof(KugouVideoTag))]
[JsonSerializable(typeof(KugouVideoSource))]
[JsonSerializable(typeof(KugouVideo))]
[JsonSerializable(typeof(KugouTrackLoudness))]
[JsonSerializable(typeof(KugouAudioUrl))]
[JsonSerializable(typeof(KugouAiRecommendResponseDto))]
[JsonSerializable(typeof(KugouAlbumResponseDto))]
[JsonSerializable(typeof(KugouAlbumDetailResponseDto))]
[JsonSerializable(typeof(KugouAlbumShopResponseDto))]
[JsonSerializable(typeof(KugouAlbumSongsResponseDto))]
[JsonSerializable(typeof(KugouArtistAlbumsResponseDto))]
[JsonSerializable(typeof(KugouArtistAudiosResponseDto))]
[JsonSerializable(typeof(KugouArtistDetailResponseDto))]
[JsonSerializable(typeof(KugouArtistFollowResponseDto))]
[JsonSerializable(typeof(KugouArtistFollowNewsongsResponseDto))]
[JsonSerializable(typeof(KugouArtistHonourResponseDto))]
[JsonSerializable(typeof(KugouArtistListsResponseDto))]
[JsonSerializable(typeof(KugouArtistUnfollowResponseDto))]
[JsonSerializable(typeof(KugouArtistVideosResponseDto))]
[JsonSerializable(typeof(KugouAudioResponseDto))]
[JsonSerializable(typeof(KugouAudioAccompanyMatchingResponseDto))]
[JsonSerializable(typeof(KugouAudioKtvTotalResponseDto))]
[JsonSerializable(typeof(KugouAudioRelatedResponseDto))]
[JsonSerializable(typeof(KugouBrushResponseDto))]
[JsonSerializable(typeof(KugouCaptchaSentResponseDto))]
[JsonSerializable(typeof(KugouCommentAlbumResponseDto))]
[JsonSerializable(typeof(KugouCommentCountResponseDto))]
[JsonSerializable(typeof(KugouCommentFloorResponseDto))]
[JsonSerializable(typeof(KugouCommentMusicResponseDto))]
[JsonSerializable(typeof(KugouCommentMusicClassifyResponseDto))]
[JsonSerializable(typeof(KugouCommentMusicHotwordResponseDto))]
[JsonSerializable(typeof(KugouCommentPlaylistResponseDto))]
[JsonSerializable(typeof(KugouEverydayFriendResponseDto))]
[JsonSerializable(typeof(KugouEverydayHistoryResponseDto))]
[JsonSerializable(typeof(KugouEverydayRecommendResponseDto))]
[JsonSerializable(typeof(KugouEverydayStyleRecommendResponseDto))]
[JsonSerializable(typeof(KugouFavoriteCountResponseDto))]
[JsonSerializable(typeof(KugouFmClassResponseDto))]
[JsonSerializable(typeof(KugouFmImageResponseDto))]
[JsonSerializable(typeof(KugouFmRecommendResponseDto))]
[JsonSerializable(typeof(KugouFmSongsResponseDto))]
[JsonSerializable(typeof(KugouImagesResponseDto))]
[JsonSerializable(typeof(KugouImagesAudioResponseDto))]
[JsonSerializable(typeof(KugouIpResponseDto))]
[JsonSerializable(typeof(KugouIpDateilResponseDto))]
[JsonSerializable(typeof(KugouIpPlaylistResponseDto))]
[JsonSerializable(typeof(KugouIpZoneResponseDto))]
[JsonSerializable(typeof(KugouIpZoneHomeResponseDto))]
[JsonSerializable(typeof(KugouKmrAudioMvResponseDto))]
[JsonSerializable(typeof(KugouKrmAudioResponseDto))]
[JsonSerializable(typeof(KugouLastestSongsListenResponseDto))]
[JsonSerializable(typeof(KugouLoginResponseDto))]
[JsonSerializable(typeof(KugouLoginCellphoneResponseDto))]
[JsonSerializable(typeof(KugouLoginDeviceResponseDto))]
[JsonSerializable(typeof(KugouLoginDeviceKickResponseDto))]
[JsonSerializable(typeof(KugouLoginOpenplatResponseDto))]
[JsonSerializable(typeof(KugouLoginQrCheckResponseDto))]
[JsonSerializable(typeof(KugouLoginQrCreateResponseDto))]
[JsonSerializable(typeof(KugouLoginQrKeyResponseDto))]
[JsonSerializable(typeof(KugouLoginTokenResponseDto))]
[JsonSerializable(typeof(KugouLoginWxCheckResponseDto))]
[JsonSerializable(typeof(KugouLoginWxCreateResponseDto))]
[JsonSerializable(typeof(KugouLongaudioAlbumAudiosResponseDto))]
[JsonSerializable(typeof(KugouLongaudioAlbumDetailResponseDto))]
[JsonSerializable(typeof(KugouLongaudioDailyRecommendResponseDto))]
[JsonSerializable(typeof(KugouLongaudioRankRecommendResponseDto))]
[JsonSerializable(typeof(KugouLongaudioVipRecommendResponseDto))]
[JsonSerializable(typeof(KugouLongaudioWeekRecommendResponseDto))]
[JsonSerializable(typeof(KugouLyricResponseDto))]
[JsonSerializable(typeof(KugouPcDiantaiResponseDto))]
[JsonSerializable(typeof(KugouPersonalFmResponseDto))]
[JsonSerializable(typeof(KugouPlayhistoryUploadResponseDto))]
[JsonSerializable(typeof(KugouPlaylistAddResponseDto))]
[JsonSerializable(typeof(KugouPlaylistDelResponseDto))]
[JsonSerializable(typeof(KugouPlaylistDetailResponseDto))]
[JsonSerializable(typeof(KugouPlaylistEffectResponseDto))]
[JsonSerializable(typeof(KugouPlaylistSimilarResponseDto))]
[JsonSerializable(typeof(KugouPlaylistTagsResponseDto))]
[JsonSerializable(typeof(KugouPlaylistTrackAllResponseDto))]
[JsonSerializable(typeof(KugouPlaylistTrackAllNewResponseDto))]
[JsonSerializable(typeof(KugouPlaylistTracksAddResponseDto))]
[JsonSerializable(typeof(KugouPlaylistTracksDelResponseDto))]
[JsonSerializable(typeof(KugouPrivilegeLiteResponseDto))]
[JsonSerializable(typeof(KugouRankAudioResponseDto))]
[JsonSerializable(typeof(KugouRankInfoResponseDto))]
[JsonSerializable(typeof(KugouRankListResponseDto))]
[JsonSerializable(typeof(KugouRankTopResponseDto))]
[JsonSerializable(typeof(KugouRankVolResponseDto))]
[JsonSerializable(typeof(KugouRecommendSongsResponseDto))]
[JsonSerializable(typeof(KugouRegisterDevResponseDto))]
[JsonSerializable(typeof(KugouSceneAudioListResponseDto))]
[JsonSerializable(typeof(KugouSceneCollectionListResponseDto))]
[JsonSerializable(typeof(KugouSceneListsResponseDto))]
[JsonSerializable(typeof(KugouSceneListsV2ResponseDto))]
[JsonSerializable(typeof(KugouSceneModuleResponseDto))]
[JsonSerializable(typeof(KugouSceneModuleInfoResponseDto))]
[JsonSerializable(typeof(KugouSceneMusicResponseDto))]
[JsonSerializable(typeof(KugouSceneVideoListResponseDto))]
[JsonSerializable(typeof(KugouSearchResponseDto))]
[JsonSerializable(typeof(KugouSearchComplexResponseDto))]
[JsonSerializable(typeof(KugouSearchDefaultResponseDto))]
[JsonSerializable(typeof(KugouSearchHotResponseDto))]
[JsonSerializable(typeof(KugouSearchLyricResponseDto))]
[JsonSerializable(typeof(KugouSearchMixedResponseDto))]
[JsonSerializable(typeof(KugouSearchSuggestResponseDto))]
[JsonSerializable(typeof(KugouServerNowResponseDto))]
[JsonSerializable(typeof(KugouSheetDetailResponseDto))]
[JsonSerializable(typeof(KugouSheetExploreResponseDto))]
[JsonSerializable(typeof(KugouSheetRankResponseDto))]
[JsonSerializable(typeof(KugouSheetSongResponseDto))]
[JsonSerializable(typeof(KugouSheetTagsResponseDto))]
[JsonSerializable(typeof(KugouSingerListResponseDto))]
[JsonSerializable(typeof(KugouSongClimaxResponseDto))]
[JsonSerializable(typeof(KugouSongRankingResponseDto))]
[JsonSerializable(typeof(KugouSongRankingFilterResponseDto))]
[JsonSerializable(typeof(KugouSongUrlResponseDto))]
[JsonSerializable(typeof(KugouSongUrlNewResponseDto))]
[JsonSerializable(typeof(KugouThemeMusicResponseDto))]
[JsonSerializable(typeof(KugouThemeMusicDetailResponseDto))]
[JsonSerializable(typeof(KugouThemePlaylistResponseDto))]
[JsonSerializable(typeof(KugouThemePlaylistTrackResponseDto))]
[JsonSerializable(typeof(KugouTopAlbumResponseDto))]
[JsonSerializable(typeof(KugouTopCardResponseDto))]
[JsonSerializable(typeof(KugouTopCardYouthResponseDto))]
[JsonSerializable(typeof(KugouTopIpResponseDto))]
[JsonSerializable(typeof(KugouTopPlaylistResponseDto))]
[JsonSerializable(typeof(KugouTopSongResponseDto))]
[JsonSerializable(typeof(KugouUserCloudResponseDto))]
[JsonSerializable(typeof(KugouUserCloudUrlResponseDto))]
[JsonSerializable(typeof(KugouUserDetailResponseDto))]
[JsonSerializable(typeof(KugouUserFollowResponseDto))]
[JsonSerializable(typeof(KugouUserFollowMessageResponseDto))]
[JsonSerializable(typeof(KugouUserHistoryResponseDto))]
[JsonSerializable(typeof(KugouUserListenResponseDto))]
[JsonSerializable(typeof(KugouUserPlaylistResponseDto))]
[JsonSerializable(typeof(KugouUserVideoCollectResponseDto))]
[JsonSerializable(typeof(KugouUserVideoLoveResponseDto))]
[JsonSerializable(typeof(KugouUserVipDetailResponseDto))]
[JsonSerializable(typeof(KugouVideoDetailResponseDto))]
[JsonSerializable(typeof(KugouVideoPrivilegeResponseDto))]
[JsonSerializable(typeof(KugouVideoUrlResponseDto))]
[JsonSerializable(typeof(KugouYouthChannelAllResponseDto))]
[JsonSerializable(typeof(KugouYouthChannelAmwayResponseDto))]
[JsonSerializable(typeof(KugouYouthChannelDetailResponseDto))]
[JsonSerializable(typeof(KugouYouthChannelSimilarResponseDto))]
[JsonSerializable(typeof(KugouYouthChannelSongResponseDto))]
[JsonSerializable(typeof(KugouYouthChannelSongDetailResponseDto))]
[JsonSerializable(typeof(KugouYouthChannelSubResponseDto))]
[JsonSerializable(typeof(KugouYouthDayVipResponseDto))]
[JsonSerializable(typeof(KugouYouthDayVipUpgradeResponseDto))]
[JsonSerializable(typeof(KugouYouthDynamicResponseDto))]
[JsonSerializable(typeof(KugouYouthDynamicRecentResponseDto))]
[JsonSerializable(typeof(KugouYouthListenSongResponseDto))]
[JsonSerializable(typeof(KugouYouthMonthVipRecordResponseDto))]
[JsonSerializable(typeof(KugouYouthUnionVipResponseDto))]
[JsonSerializable(typeof(KugouYouthUserSongResponseDto))]
[JsonSerializable(typeof(KugouYouthVipResponseDto))]
[JsonSerializable(typeof(KugouYuekuResponseDto))]
[JsonSerializable(typeof(KugouYuekuBannerResponseDto))]
[JsonSerializable(typeof(KugouYuekuFmResponseDto))]
internal sealed partial class KugouJsonSerializerContext : JsonSerializerContext
{
}
