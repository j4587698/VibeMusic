using System.Net;
using System.Text;

namespace KuGou.Lite;

public enum KugouPayloadMode
{
    Query,
    Body,
    Both
}

public sealed record KugouApiDefinition(
    string Route,
    string Description,
    string Method,
    string Upstream,
    string? Router,
    KugouPayloadMode PayloadMode);

public static class KugouApiCatalog
{
    public static IReadOnlyList<KugouApiDefinition> All { get; } = BuildDefinitions();

    private static readonly Lazy<IReadOnlyDictionary<string, KugouApiDefinition>> ByRoute = new(
        () => All.ToDictionary(item => item.Route, StringComparer.OrdinalIgnoreCase));

    public static KugouApiDefinition Get(string route)
    {
        if (ByRoute.Value.TryGetValue(route, out var definition))
        {
            return definition;
        }

        throw new ArgumentOutOfRangeException(nameof(route), route, "Unknown KuGou API route.");
    }

    private static IReadOnlyList<KugouApiDefinition> BuildDefinitions()
    {
        return DefinitionTable
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .Select(line =>
            {
                var parts = line.Split('|');
                return new KugouApiDefinition(
                    parts[0],
                    parts[1],
                    parts[2],
                    parts[3],
                    parts[4] == "-" ? null : parts[4],
                    Enum.Parse<KugouPayloadMode>(parts[5], ignoreCase: true));
            })
            .ToArray();
    }

    private const string DefinitionTable = """
/ai/recommend|AI 推荐|POST|/recommend|songlistairec.kugou.com|Body
/album|专辑信息|POST|http://kmr.service.kugou.com/v1/album|kmr.service.kugou.com|Body
/album/detail|专辑详情|POST|/kmr/v2/albums|openapi.kugou.com|Body
/album/shop|唱片店|GET|/zhuanjidata/v3/album_shop_v2/get_classify_data|-|Query
/album/songs|专辑音乐列表|POST|/v1/album_audio/lite|openapi.kugou.com|Body
/artist/albums|获取歌手专辑|POST|/kmr/v1/author/albums|openapi.kugou.com|Body
/artist/audios|获取歌手单曲|POST|https://openapi.kugou.com/kmr/v1/audio_group/author|openapi.kugou.com|Body
/artist/detail|歌手详情|POST|/kmr/v3/author|openapi.kugou.com|Body
/artist/follow|关注歌手|POST|/followservice/v3/follow_singer|-|Both
/artist/follow/newsongs|获取关注歌手新歌|POST|/feed/v1/follow/newsong_album_list|-|Both
/artist/honour|歌手荣誉详情|POST|http://h5activity.kugou.com/v1/query_singer_honour_detail|-|Query
/artist/lists|获取歌手列表|GET|/ocean/v6/singer/list|-|Query
/artist/unfollow|取消关注歌手|POST|/followservice/v3/unfollow_singer|-|Both
/artist/videos|获取歌手 MV|GET|https://openapicdn.kugou.com/kmr/v1/author/videos|-|Query
/audio|获取音乐相关信息|POST|http://kmr.service.kugou.com/v1/audio/audio|kmr.service.kugou.com|Body
/audio/accompany/matching|获取音乐伴奏信息|GET|https://nsongacsing.kugou.com/sing7/accompanywan/json/v2/cdn/optimal_matching_accompany_2_listen.do|-|Query
/audio/ktv/total|获取音乐 K 歌数量|GET|https://acsing.service.kugou.com/sing7/listenguide/json/v2/cdn/listenguide/get_total_opus_num_v02.do|-|Query
/audio/related|获取更多音乐版本|GET|https://listkmrp3cdnretry.kugou.com|-|Query
/brush|刷刷|POST|/genesisapi/v1/newepoch_song_rec/feed|-|Both
/captcha/sent|发送验证码|POST|http://login.user.kugou.com/v7/send_mobile_code|-|Body
/comment/album|专辑评论|POST|/m.comment.service/v1/cmtlist|-|Query
/comment/count|歌曲评论数|GET|/index.php|sum.comment.service.kugou.com|Query
/comment/floor|楼层评论|POST|/mcomment/v1/hot_replylist|-|Query
/comment/music|歌曲评论|POST|/mcomment/v1/cmtlist|-|Query
/comment/music/classify|歌曲评论-根据分类返回|POST|/mcomment/v1/cmt_classify_list|-|Query
/comment/music/hotword|歌曲评论-根据热词返回|POST|/mcomment/v1/get_hot_word|-|Query
/comment/playlist|歌单评论|POST|/m.comment.service/v1/cmtlist|-|Query
/everyday/friend|好友听歌推荐|POST|https://acsing.service.kugou.com/sing7/relation/json/v3/friend_rec_by_using_song_list|-|Both
/everyday/history|历史推荐|POST|/everyday/api/v1/get_history|everydayrec.service.kugou.com|Query
/everyday/recommend|每日推荐|POST|/everyday_song_recommend|everydayrec.service.kugou.com|Query
/everyday/style/recommend|风格推荐|POST|/everydayrec.service/everyday_style_recommend|-|Both
/favorite/count|歌曲收藏数|GET|/count/v1/audio/mget_collect|-|Query
/fm/class|电台|POST|/v1/class_fm_song|fm.service.kugou.com|Body
/fm/image|电台图片|POST|/v1/fm_info|fm.service.kugou.com|Body
/fm/recommend|电台推荐|POST|/v1/rcmd_list|fm.service.kugou.com|Body
/fm/songs|电台音乐列表|POST|/v1/app_song_list_offset|fm.service.kugou.com|Body
/images|歌手和专辑图片|GET|https://expendablekmr.kugou.com/container/v2/image|-|Query
/images/audio|歌手图片|GET|https://expendablekmr.kugou.com/v2/author_image/audio|-|Query
/ip|编辑精选数据|POST|/openapi/v1/ip/{type}|-|Body
/ip/dateil|IP 详情|POST|/openapi/v1/ip|-|Body
/ip/playlist|编辑精选歌单|POST|/ocean/v6/pubsongs/list_info_for_ip|-|Query
/ip/zone|编辑精选专区|GET|/v1/zone/index|yuekucategory.kugou.com|Query
/ip/zone/home|编辑精选专区详情|GET|/v1/zone/home|yuekucategory.kugou.com|Query
/kmr/audio/mv|获取歌曲 MV|POST|/kmr/v1/audio/mv|openapi.kugou.com|Body
/krm/audio|获取音乐专辑/歌手信息|POST|/kmr/v2/audio|openapi.kugou.com|Body
/lastest/songs/listen|继续播放信息|POST|/playque/devque/v1/get_latest_songs|-|Body
/login|用户名登录|POST|/v9/login_by_pwd|login.user.kugou.com|Both
/login/cellphone|手机登录|POST|https://loginserviceretry.kugou.com/v7/login_by_verifycode|-|Both
/login/device|获取登录设备|POST|https://userinfoservice.kugou.com/v2/get_dev|-|Both
/login/device/kick|踢出登录设备|GET|/loginservice/v1/dev_logout|-|Body
/login/openplat|开放平台登录|POST|https://api.weixin.qq.com/sns/oauth2/access_token|login.user.kugou.com|Both
/login/qr/check|二维码登录状态|GET|https://login-user.kugou.com/v2/get_userinfo_qrcode|-|Query
/login/qr/create|生成酷狗二维码|GET|local:qrcode|-|Body
/login/qr/key|二维码 key|GET|https://login-user.kugou.com/v2/qrcode|-|Query
/login/token|刷新登录|POST|http://login.user.kugou.com/v5/login_by_token|-|Both
/login/wx/check|微信登录状态|GET|https://long.open.weixin.qq.com/connect/l/qrconnect?f=json&uuid={uuid}|-|Query
/login/wx/create|微信二维码登录|GET|https://api.weixin.qq.com/cgi-bin/token|-|Query
/longaudio/album/audios|听书专辑音乐列表|POST|/longaudio/v2/album_audios|openapi.kugou.com|Body
/longaudio/album/detail|听书专辑详情|POST|/openapi/v2/broadcast|-|Body
/longaudio/daily/recommend|听书每日推荐|POST|/longaudio/v1/home_new/daily_recommend|-|Query
/longaudio/rank/recommend|听书排行榜推荐|GET|/longaudio/v1/home_new/rank_card_recommend|-|Query
/longaudio/vip/recommend|听书 VIP 推荐|POST|/longaudio/v1/home_new/vip_select_recommend|-|Both
/longaudio/week/recommend|听书每周推荐|POST|/longaudio/v1/home_new/week_new_albums_recommend|-|Both
/lyric|获取歌词|GET|https://lyrics.kugou.com/download|-|Query
/pc/diantai|banner|POST|https://adservice.kugou.com/v3/pc_diantai|-|Body
/personal/fm|私人 FM|POST|/v2/personal_recommend|persnfm.service.kugou.com|Body
/playhistory/upload|提交听歌历史|POST|/playhistory/v1/upload_songs|-|Both
/playlist/add|收藏歌单/新建歌单|POST|/cloudlist.service/v5/add_list|-|Both
/playlist/del|取消收藏歌单/删除歌单|POST|/v2/delete_list|cloudlist.service.kugou.com|Both
/playlist/detail|获取歌单详情|POST|/v3/get_list_info|pubsongs.kugou.com|Body
/playlist/effect|音效歌单|POST|/pubsongs/v1/get_sound_effect_list|-|Body
/playlist/similar|相似歌单|POST|/pubsongs/v1/kmr_get_similar_lists|-|Body
/playlist/tags|歌单分类|POST|/pubsongs/v1/get_tags_by_type|-|Body
/playlist/track/all|获取歌单所有歌曲|GET|/pubsongs/v2/get_other_list_file_nofilt|pubsongscdn.kugou.com|Query
/playlist/track/all/new|获取歌单所有歌曲新版|POST|/v4/get_list_all_file|cloudlist.service.kugou.com|Body
/playlist/tracks/add|对歌单添加歌曲|POST|/cloudlist.service/v6/add_song|-|Both
/playlist/tracks/del|对歌单删除歌曲|POST|/v4/delete_songs|cloudlist.service.kugou.com|Body
/privilege/lite|获取音乐详情|POST|/v2/get_res_privilege/lite|media.store.kugou.com|Body
/rank/audio|排行榜歌曲列表|POST|/openapi/kmr/v2/rank/audio|-|Body
/rank/info|排行榜信息|GET|/ocean/v6/rank/info|-|Query
/rank/list|排行列表|GET|/ocean/v6/rank/list|-|Query
/rank/top|排行榜推荐列表|GET|/mobileservice/api/v5/rank/rec_rank_list|-|Query
/rank/vol|排行榜往期列表|GET|/ocean/v6/rank/vol|-|Query
/recommend/songs|每日推荐歌曲|POST|/everyday_song_recommend|everydayrec.service.kugou.com|Body
/register/dev|dfid 获取|POST|https://userservice.kugou.com/risk/v2/r_register_dev|-|Both
/scene/audio/list|场景音乐音乐列表|POST|/scene/v1/scene/audio_list|-|Both
/scene/collection/list|场景音乐歌单列表|POST|/scene/v1/distribution/collection_list|-|Body
/scene/lists|场景音乐列表|GET|/scene/v1/scene/list|-|Query
/scene/lists/v2|场景音乐讨论区|POST|/scene/v1/scene/list_v2|-|Both
/scene/module|场景音乐详情|POST|/scene/v1/scene/module|-|Query
/scene/module/info|场景音乐模块 Tag|GET|/scene/v1/scene/module_info|-|Query
/scene/music|场景音乐推荐|POST|/genesisapi/v1/scene_music/rec_music|-|Both
/scene/video/list|场景音乐视频列表|POST|/scene/v1/distribution/video_list|-|Body
/search|搜索|GET|/v3/search/song|complexsearch.kugou.com|Query
/search/complex|综合搜索|GET|https://complexsearch.kugou.com/v6/search/complex|-|Query
/search/default|默认搜索关键词|POST|/searchnofocus/v1/search_no_focus_word|-|Both
/search/hot|热搜列表|GET|/api/v3/search/hot_tab|msearch.kugou.com|Query
/search/lyric|歌词搜索|GET|https://lyrics.kugou.com/v1/search|-|Query
/search/mixed|混合搜索|GET|/v3/search/mixed|complexsearch.kugou.com|Query
/search/suggest|搜索建议|GET|/v2/getSearchTip|searchtip.kugou.com|Query
/server/now|获取服务器时间|POST|/v1/server_now|usercenter.kugou.com|Both
/sheet/collection|曲谱合集|GET|/miniyueku/v1/opern_square/get_home_module_config|-|Query
/sheet/collection/detail|曲谱合集详情|GET|/miniyueku/v1/opern_square/collection_detail|-|Query
/sheet/detail|曲谱详情|GET|https://miniyueku.kugou.com/v1/opern/detail|-|Query
/sheet/hot|推荐曲谱|GET|/miniyueku/v1/opern_square/get_home_hot_opern|-|Query
/sheet/list|歌曲曲谱|GET|/miniyueku/v1/opern/list|-|Query
/singer/list|歌手列表|GET|/ocean/v6/singer/list|-|Query
/song/climax|获取歌曲高潮部分|GET|https://expendablekmrcdn.kugou.com/v1/audio_climax/audio|-|Both
/song/ranking|歌曲成绩单|GET|/grow/v1/song_ranking/play_page/ranking_info|-|Query
/song/ranking/filter|歌曲成绩单详情|GET|/grow/v1/song_ranking/unlock/v2/ranking_filter|-|Query
/song/url|获取音乐 URL|GET|/v5/url|trackercdn.kugou.com|Query
/song/url/new|获取音乐 URL 新版|POST|http://tracker.kugou.com/v6/priv_url|-|Body
/theme/music|获取主题音乐|POST|/everydayrec.service/v1/mul_theme_category_recommend|-|Body
/theme/music/detail|获取主题音乐详情|POST|/everydayrec.service/v1/theme_category_recommend|-|Body
/theme/playlist|主题歌单|POST|/v2/getthemelist|everydayrec.service.kugou.com|Body
/theme/playlist/track|获取主题歌单所有歌曲|POST|/v2/gettheme_songidlist|everydayrec.service.kugou.com|Body
/top/album|新碟上架|POST|/musicadservice/v1/mobile_newalbum_sp|-|Body
/top/card|歌曲推荐|POST|/singlecardrec.service/v1/single_card_recommend|-|Both
/top/card/youth|歌曲推荐概念版|POST|youth/v1/song/single_card_recommend|-|Both
/top/ip|编辑精选|POST|http://musicadservice.kugou.com/v1/daily_recommend|-|Both
/top/playlist|歌单|POST|/v2/special_recommend|specialrec.service.kugou.com|Body
/top/song|新歌速递|POST|/musicadservice/container/v1/newsong_publish|-|Body
/user/cloud|用户云盘|POST|https://mcloudservice.kugou.com/v1/get_list|-|Both
/user/cloud/url|用户云盘音乐 URL|GET|/bsstrackercdngz/v2/query_musicclound_url|-|Query
/user/detail|用户额外信息|POST|/v3/get_my_info|usercenter.kugou.com|Both
/user/follow|用户关注歌手|POST|/v4/follow_list|relationuser.kugou.com|Both
/user/follow/message|关注歌手消息|GET|/msg.mobile/v3/msgtag/history|-|Query
/user/history|用户最近听歌历史|POST|/playhistory/v1/get_songs|-|Body
/user/listen|用户听歌历史排行|POST|https://listenservice.kugou.com/v2/get_list|-|Both
/user/playlist|用户歌单|POST|/v7/get_all_list|cloudlist.service.kugou.com|Both
/user/video/collect|用户收藏视频|POST|/collectservice/v2/collect_list_mixvideo|-|Both
/user/video/love|用户喜欢视频|GET|/m.comment.service/v1/get_user_like_video|-|Query
/user/vip/detail|用户 VIP 信息|GET|https://kugouvip.kugou.com/v1/get_union_vip|-|Query
/video/detail|视频详情|POST|/v1/video|kmr.service.kugou.com|Body
/video/privilege|视频相关信息|POST|/v1/get_video_privilege|media.store.kugou.com|Body
/video/url|视频 URL|GET|/v2/interface/index|trackermv.kugou.com|Query
/youth/channel/all|频道-获取用户所有频道|GET|/youth/v2/channel/channel_all_list|-|Query
/youth/channel/amway|频道安利|GET|/youth/api/amway/v2/index|-|Query
/youth/channel/detail|频道详情|POST|/youth/api/channel/v1/channel_list_by_id|-|Body
/youth/channel/similar|相似频道|POST|/youth/v1/channel/get_friendly_channel|-|Both
/youth/channel/song|频道音乐故事|GET|/youth/api/channel/v1/channel_get_song_audit_passed|-|Query
/youth/channel/song/detail|频道音乐故事详情|GET|/youth/v2/post/get_song_detail|-|Query
/youth/channel/sub|频道订阅|POST|/youth/v1/channel_subscribe|-|Query
/youth/day/vip|领取一天 VIP|POST|/youth/v1/recharge/receive_vip_listen_song|-|Query
/youth/day/vip/upgrade|升级概念版 VIP|POST|/youth/v1/listen_song/upgrade_vip_reward|-|Query
/youth/dynamic|动态|GET|/youth/v3/user/get_dynamic|-|Query
/youth/dynamic/recent|动态-最常访问|GET|/youth/v3/user/recent_dynamic|-|Query
/youth/listen/song|听歌领取 VIP|POST|/youth/v2/report/listen_song|-|Both
/youth/month/vip/record|当月已领取 VIP 天数|GET|/youth/v1/activity/get_month_vip_record|-|Query
/youth/union/vip|已领取 VIP 状态|GET|https://kugouvip.kugou.com/v1/get_union_vip|-|Query
/youth/user/song|用户公开音乐|GET|/youth/v1/get_user_song_public|-|Query
/youth/vip|领取 VIP|POST|/youth/v1/ad/play_report|-|Body
/yueku|乐库|GET|/v1/yueku/recommend_v2|service.mobile.kugou.com|Query
/yueku/banner|乐库 banner|POST|/ads.gateway/v3/listen_banner|-|Body
/yueku/fm|乐库电台|GET|/v1/time_fm_info|fm.service.kugou.com|Query
""";
}

public sealed partial class KugouLiteClient
{
    public Task<KugouResponse> InvokeRouteAsync(
        string route,
        IDictionary<string, object?>? parameters = null,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        return InvokeApiAsync(KugouApiCatalog.Get(route), parameters, body, cancellationToken);
    }

    public Task<KugouResponse> InvokeApiAsync(
        KugouApiDefinition definition,
        IDictionary<string, object?>? parameters = null,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        var values = parameters is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(parameters, StringComparer.Ordinal);

        if (definition.Upstream == "local:qrcode")
        {
            return Task.FromResult(CreateLocalQrCodeResponse(values));
        }

        var explicitUpstream = TakeControlValue(values, "__upstreamPath");
        var upstream = explicitUpstream ?? definition.Upstream;
        var methodName = TakeControlValue(values, "__method") ?? definition.Method;
        var explicitBody = TakeControlObject(values, "__body");
        if (explicitBody.found)
        {
            body = explicitBody.value;
        }

        if (explicitUpstream is null && definition.Route.Equals("/search", StringComparison.OrdinalIgnoreCase))
        {
            upstream = ApplySearchRouteDefaults(values);
        }

        var request = new KugouRequest
        {
            Method = ToHttpMethod(methodName, definition.PayloadMode),
            Path = ResolveUpstreamPath(upstream, values),
            Body = body,
            EncryptType = KugouEncryptType.Android
        };

        if (definition.Router is not null)
        {
            request.Headers["x-router"] = definition.Router;
        }

        if (bool.TryParse(TakeControlValue(values, "__clearDefaultParams"), out var clearDefaultParams))
        {
            request.ClearDefaultParams = clearDefaultParams;
        }

        if (bool.TryParse(TakeControlValue(values, "__notSignature"), out var notSignature))
        {
            request.NotSignature = notSignature;
        }

        var encryptType = TakeControlValue(values, "__encryptType");
        if (!string.IsNullOrWhiteSpace(encryptType) && Enum.TryParse<KugouEncryptType>(encryptType, ignoreCase: true, out var parsedEncryptType))
        {
            request.EncryptType = parsedEncryptType;
        }

        foreach (var header in ExtractPrefixed(values, "header:"))
        {
            request.Headers[header.Key] = KugouCrypto.FormatValueForQuery(header.Value);
        }

        foreach (var cookie in ExtractPrefixed(values, "cookie:"))
        {
            request.Cookies[cookie.Key] = KugouCrypto.FormatValueForQuery(cookie.Value);
        }

        ApplyPayload(definition.PayloadMode, request, values, body is not null);
        return SendAsync(request, cancellationToken);
    }

    private static string ApplySearchRouteDefaults(Dictionary<string, object?> values)
    {
        var rawType = (TakeControlValue(values, "type") ?? "song").ToLowerInvariant();
        var type = rawType switch
        {
            "special" or "lyric" or "song" or "album" or "author" or "mv" => rawType,
            _ => "song"
        };

        var keywords = TakeControlValue(values, "keywords");
        if (!string.IsNullOrWhiteSpace(keywords) && !values.ContainsKey("keyword"))
        {
            values["keyword"] = keywords;
        }

        if (!values.ContainsKey("albumhide"))
        {
            values["albumhide"] = 0;
        }

        if (!values.ContainsKey("iscorrection"))
        {
            values["iscorrection"] = 1;
        }

        if (!values.ContainsKey("nocollect"))
        {
            values["nocollect"] = 0;
        }

        if (!values.ContainsKey("platform"))
        {
            values["platform"] = "AndroidFilter";
        }

        var version = type == "song" ? "v3" : "v1";
        return $"/{version}/search/{type}";
    }

    private static void ApplyPayload(KugouPayloadMode mode, KugouRequest request, Dictionary<string, object?> values, bool hasExplicitBody)
    {
        if (request.Method == HttpMethod.Get || request.Method.Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            Copy(values, request.Params);
            if (hasExplicitBody)
            {
                return;
            }

            return;
        }

        switch (mode)
        {
            case KugouPayloadMode.Query:
                Copy(values, request.Params);
                break;
            case KugouPayloadMode.Both:
                Copy(values, request.Params);
                if (!hasExplicitBody)
                {
                    request.Body = new Dictionary<string, object?>(values, StringComparer.Ordinal);
                }

                break;
            case KugouPayloadMode.Body:
            default:
                if (!hasExplicitBody)
                {
                    request.Body = new Dictionary<string, object?>(values, StringComparer.Ordinal);
                }

                break;
        }
    }

    private static string ResolveUpstreamPath(string upstream, IDictionary<string, object?> values)
    {
        if (upstream.Contains("{type}", StringComparison.Ordinal) && !values.ContainsKey("type"))
        {
            values["type"] = "audios";
        }

        foreach (var item in values)
        {
            upstream = upstream.Replace("{" + item.Key + "}", Uri.EscapeDataString(KugouCrypto.FormatValueForQuery(item.Value)), StringComparison.Ordinal);
        }

        return upstream;
    }

    private static HttpMethod ToHttpMethod(string method, KugouPayloadMode mode)
    {
        if (string.IsNullOrWhiteSpace(method) || method == "-")
        {
            return mode == KugouPayloadMode.Body ? HttpMethod.Post : HttpMethod.Get;
        }

        return method.ToUpperInvariant() switch
        {
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            _ => HttpMethod.Get
        };
    }

    private static string? TakeControlValue(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return null;
        }

        values.Remove(key);
        return value is null ? null : KugouCrypto.FormatValueForQuery(value);
    }

    private static (bool found, object? value) TakeControlObject(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return (false, null);
        }

        values.Remove(key);
        return (true, value);
    }

    private static Dictionary<string, object?> ExtractPrefixed(Dictionary<string, object?> values, string prefix)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in values.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            result[key[prefix.Length..]] = values[key];
            values.Remove(key);
        }

        return result;
    }

    private static KugouResponse CreateLocalQrCodeResponse(IDictionary<string, object?> values)
    {
        var key = values.TryGetValue("key", out var keyValue) ? KugouCrypto.FormatValueForQuery(keyValue) : string.Empty;
        var url = $"https://h5.kugou.com/apps/loginQRCode/html/index.html?qrcode={Uri.EscapeDataString(key)}";
        var body = KugouCrypto.ToJson(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = 200,
            ["status"] = 200,
            ["data"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = url,
                ["base64"] = string.Empty
            }
        });

        return new KugouResponse(HttpStatusCode.OK, Encoding.UTF8.GetBytes(body), Array.Empty<string>(), new Dictionary<string, string>());
    }
}