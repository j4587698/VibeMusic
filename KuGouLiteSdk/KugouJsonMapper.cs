using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KuGou.Lite;

internal enum KugouSongMapKind
{
    Generic,
    Top,
    Playlist,
    Artist,
    Album,
    Rank,
    Search,
    History,
    Cloud
}

internal static partial class KugouJsonMapper
{
    private const string DefaultCoverUrl = "https://imge.kugou.com/soft/collection/default.jpg";

    private readonly record struct AudioUrlCandidate(string Url, JsonObject? Record, JsonObject? Info, bool IsEncrypted);

    public static KugouListResult<T> ToListResult<T>(KugouResponse response, Func<JsonNode?, T> mapper)
    {
        var root = Parse(response);
        var items = ExtractList(root).Select(mapper).ToArray();
        return new KugouListResult<T>(items, ExtractTotal(root, items.Length), response);
    }

    public static KugouTypedResult<T> ToFirstResult<T>(KugouResponse response, Func<JsonNode?, T> mapper)
    {
        var root = Parse(response);
        var first = ExtractFirstObject(root);
        return new KugouTypedResult<T>(first is null ? default : mapper(first), response);
    }

    public static KugouListResult<KugouComment> ToCommentListResult(KugouResponse response)
    {
        var root = Parse(response);
        var source = Object(GetObject(root, "data")) ?? Object(GetObject(root, "info")) ?? Object(root);
        var list = ArrayFrom(Pick(Get(source, "list"), Get(source, "comments")));
        var hotCandidate = Pick(Get(source, "weight_list"), Get(source, "hot_list"), Get(source, "star_cmts"), Get(source, "star_comment"));
        var hotSource = ArrayFrom(Get(Object(hotCandidate), "list"));
        if (hotSource.Length == 0)
        {
            hotSource = ArrayFrom(hotCandidate);
        }

        var starSource = ArrayFrom(Get(Object(Get(source, "star_cmts")), "list"));
        if (starSource.Length == 0)
        {
            starSource = ArrayFrom(Get(Object(Get(source, "star_comment")), "list"));
        }

        var hot = starSource.Select(item => MapComment(item, isStar: true))
            .Concat(hotSource.Select(item => MapComment(item, isHot: true)))
            .Where(item => item.Content.Length > 0)
            .ToArray();
        var items = list.Select(item => MapComment(item)).Where(item => item.Content.Length > 0).ToArray();
        var total = ParseInt(Pick(Get(source, "count"), Get(source, "total"), Get(Object(root), "count"), Get(Object(root), "total")));
        if (total == 0)
        {
            total = items.Length;
        }

        return new KugouListResult<KugouComment>(items, total, response, hot);
    }

    public static KugouAudioUrl MapAudioUrl(KugouResponse response)
    {
        var root = Parse(response);
        var record = Object(root);
        var data = Object(Get(record, "data"));
        var info = Object(Get(record, "info"));
        var candidate = ResolveAudioUrlCandidate(root);
        var candidateRecord = candidate.Record;
        var candidateInfo = candidate.Info ?? Object(Get(candidateRecord, "info"));
        var fileSize = candidate.IsEncrypted
            ? Pick(
                Get(candidateRecord, "en_filesize"),
                Get(candidateRecord, "en_fileSize"),
                Get(candidateInfo, "en_filesize"),
                Get(candidateInfo, "en_fileSize"),
                Get(candidateRecord, "filesize"),
                Get(candidateRecord, "fileSize"),
                Get(candidateInfo, "filesize"),
                Get(candidateInfo, "fileSize"),
                Get(record, "en_filesize"),
                Get(data, "en_filesize"),
                Get(info, "en_filesize"))
            : Pick(
                Get(candidateRecord, "filesize"),
                Get(candidateRecord, "fileSize"),
                Get(candidateInfo, "filesize"),
                Get(candidateInfo, "fileSize"),
                Get(record, "filesize"),
                Get(record, "fileSize"),
                Get(data, "filesize"),
                Get(data, "fileSize"),
                Get(info, "filesize"),
                Get(info, "fileSize"));

        return new KugouAudioUrl
        {
            Url = candidate.Url ?? string.Empty,
            Bitrate = ParseOptionalInt(Pick(Get(candidateRecord, "bitRate"), Get(candidateRecord, "bitrate"), Get(candidateInfo, "bitRate"), Get(candidateInfo, "bitrate"), Get(record, "bitRate"), Get(record, "bitrate"), Get(data, "bitRate"), Get(data, "bitrate"), Get(info, "bitRate"), Get(info, "bitrate"))),
            Loudness = ResolveTrackLoudness(root) ?? ResolveTrackLoudness(candidateRecord) ?? ResolveTrackLoudness(candidateInfo),
            EnEkey = ReadString(Pick(Get(candidateRecord, "en_ekey"), Get(candidateInfo, "en_ekey"), Get(record, "en_ekey"), Get(data, "en_ekey"), Get(info, "en_ekey"))),
            FileSize = ParseOptionalLong(fileSize) ?? 0
        };
    }

    public static IReadOnlyList<KugouSongRelateGood> MapRelateGoodsFromPrivilege(KugouResponse response)
    {
        var root = Parse(response);
        var source = Get(Object(root), "data");
        var array = ArrayFrom(source);

        var goods = new System.Collections.Generic.List<KugouSongRelateGood>();
        foreach (var item in array)
        {
            var record = Object(item);
            var hash = ReadString(Get(record, "hash"));
            var quality = ReadString(Get(record, "quality"));
            if (!string.IsNullOrWhiteSpace(hash) || !string.IsNullOrWhiteSpace(quality))
            {
                goods.Add(new KugouSongRelateGood { Hash = EmptyToNull(hash), Quality = EmptyToNull(quality) });
            }
        }

        var first = Object(array.FirstOrDefault()) ?? Object(source);
        if (first is not null)
        {
            goods.AddRange(BuildRelateGoods(first, null));
        }

        return goods.ToArray();
    }

    public static KugouSong MapSong(JsonNode? json, KugouSongMapKind kind = KugouSongMapKind.Generic, string? artistId = null)
    {
        var record = Object(json);
        var info = Object(Get(record, "info"));
        if (kind == KugouSongMapKind.History && info is not null)
        {
            var baseSong = MapSong(info, KugouSongMapKind.Playlist, artistId);
            var playedAt = ParseLong(Pick(
                Get(record, "ot"),
                Get(record, "play_time"),
                Get(record, "played_time"),
                Get(record, "last_play_time"),
                Get(record, "listen_time"),
                Get(record, "time")));
            var playCount = ParseOptionalInt(Pick(Get(record, "pc"), Get(record, "play_count"), Get(record, "playcount"), Get(record, "count")));
            var mxid = ParseLong(Pick(Get(record, "mxid"), Get(record, "mixsongid"), Get(info, "mixsongid"), Get(info, "MixSongID")));
            var historyIdentity = ReadString(Pick(Get(record, "history_id"), Get(record, "id"), Get(record, "play_id"), Get(record, "pid"), Get(record, "bp")));
            var historyKey = !string.IsNullOrWhiteSpace(historyIdentity)
                ? historyIdentity
                : playedAt > 0
                    ? $"{(mxid > 0 ? mxid : baseSong.MixSongId)}:{playedAt}"
                    : $"{(mxid > 0 ? mxid : baseSong.MixSongId)}:{baseSong.Hash}";
            return CopySong(baseSong, mxid, playedAt, playCount, historyKey);
        }

        var baseObj = Object(Get(record, "base"));
        var audioInfo = Object(Get(record, "audio_info"));
        var albumInfo = Object(Pick(Get(record, "albuminfo"), Get(record, "album_info")));
        var transParam = Object(Get(record, "trans_param"));
        var copyright = Object(Get(record, "copyright"));
        var deprecated = Object(Get(record, "deprecated"));
        var privilegeDownload = Object(Pick(Get(record, "privilege_download"), Get(record, "privilegeDownload")));
        var recSongInfo = Object(Get(record, "rec_song_info"));

        var rawName = ReadString(Pick(
            Get(record, "songname"),
            Get(record, "SongName"),
            Get(record, "filename"),
            Get(record, "FileName"),
            Get(record, "name"),
            Get(record, "audio_name"),
            Get(record, "ori_audio_name"),
            Get(audioInfo, "songname"),
            Get(audioInfo, "filename"),
            Get(audioInfo, "name"),
            Get(baseObj, "audio_name")), "未知歌曲");

        var singerName = NormalizeText(ReadString(Pick(
            Get(record, "author_name"),
            Get(record, "AuthorName"),
            Get(record, "singername"),
            Get(record, "SingerName"),
            Get(record, "singer"),
            Get(audioInfo, "author_name"),
            Get(audioInfo, "AuthorName"),
            Get(audioInfo, "singername"),
            Get(baseObj, "author_name"),
            Get(baseObj, "AuthorName"))));
        if (string.IsNullOrWhiteSpace(singerName) && rawName.Contains(" - ", StringComparison.Ordinal))
        {
            singerName = rawName.Split(" - ", 2, StringSplitOptions.None)[0];
        }

        var title = CleanupAudioExtension(ProcessSongTitle(rawName));
        singerName = CleanupAudioExtension(singerName);
        var artists = BuildArtists(record, audioInfo).ToArray();
        var fallbackArtist = !string.IsNullOrWhiteSpace(singerName) ? singerName : "未知歌手";
        var singers = artists.Length > 0
            ? artists
            : new[] { new KugouSongArtist { Id = artistId, Name = fallbackArtist } };
        var albumName = NormalizeText(ReadString(Pick(
            Get(record, "AlbumName"),
            Get(record, "albumname"),
            Get(record, "album_name"),
            Get(albumInfo, "name"),
            Get(albumInfo, "album_name"))));
        var cover = FormatPic(Pick(
            Get(record, "album_sizable_cover"),
            Get(record, "sizable_cover"),
            Get(record, "Image"),
            Get(record, "cover"),
            Get(record, "cover_url"),
            Get(record, "pic"),
            Get(record, "img"),
            Get(audioInfo, "img"),
            Get(albumInfo, "cover"),
            Get(albumInfo, "sizable_cover"),
            Get(transParam, "union_cover")));
        var duration = NormalizeDuration(ParseInt(Pick(
            Get(record, "Duration"),
            Get(record, "time_length"),
            Get(record, "timelength"),
            Get(record, "timelen"),
            Get(record, "duration"),
            Get(audioInfo, "duration_128"),
            Get(audioInfo, "timelength_128"),
            Get(audioInfo, "timelength"),
            Get(audioInfo, "duration"))));
        var hash = ReadString(Pick(
            Get(record, "FileHash"),
            Get(record, "hash"),
            Get(record, "hash_128"),
            Get(record, "hash_320"),
            Get(record, "hash_flac"),
            Get(record, "hash_high"),
            Get(audioInfo, "hash_128"),
            Get(audioInfo, "hash")));
        var id = ReadString(Pick(
            Get(record, "MixSongID"),
            Get(record, "mixsongid"),
            Get(record, "audio_id"),
            Get(record, "Audioid"),
            Get(record, "album_audio_id"),
            Get(baseObj, "mixsongid"),
            Get(baseObj, "album_audio_id"),
            Get(baseObj, "audio_id"),
            Get(audioInfo, "audio_id"),
            Node(hash)));
        var songId = ReadString(Pick(Get(record, "songid"), Get(record, "song_id"), Get(record, "audio_id"), Get(baseObj, "audio_id")));
        var mixSongId = ParseLong(Pick(
            Get(record, "MixSongID"),
            Get(record, "mixsongid"),
            Get(record, "album_audio_id"),
            Get(record, "audio_id"),
            Get(baseObj, "mixsongid"),
            Get(baseObj, "album_audio_id"),
            Get(baseObj, "audio_id"),
            Get(audioInfo, "audio_id")));
        var fileId = ParseOptionalLong(Pick(Get(record, "fileid"), Get(record, "file_id"), Get(record, "Audioid"), Get(record, "audio_id"), Get(audioInfo, "audio_id")));
        var privilege = ParseOptionalInt(Pick(Get(record, "AlbumPrivilege"), Get(record, "privilege"), Get(audioInfo, "privilege"), Get(copyright, "privilege"), Get(privilegeDownload, "privilege")));
        var payType = ParseOptionalInt(Pick(Get(record, "PayType"), Get(record, "pay_type"), Get(record, "payType"), Get(deprecated, "pay_type")));
        var oldCpy = ParseOptionalInt(Pick(Get(record, "OldCpy"), Get(record, "old_cpy"), Get(record, "media_old_cpy"), Get(record, "mediaOldCpy"), Get(deprecated, "old_cpy")));

        return new KugouSong
        {
            Id = id,
            SongId = EmptyToNull(songId),
            Title = string.IsNullOrWhiteSpace(title) ? "未知歌曲" : title,
            Name = string.IsNullOrWhiteSpace(title) ? "未知歌曲" : title,
            Artist = NormalizeText(singers.Length > 0 ? string.Join(", ", singers.Select(item => item.Name)) : fallbackArtist),
            Language = EmptyToNull(NormalizeText(ReadString(Pick(Get(record, "language"), Get(transParam, "language"))))),
            Artists = singers,
            Singers = singers,
            Album = albumName,
            AlbumName = albumName,
            AlbumId = EmptyToNull(ReadString(Pick(Get(record, "AlbumID"), Get(record, "AlbumId"), Get(record, "album_id"), Get(record, "albumid"), Get(albumInfo, "id"), Get(albumInfo, "album_id"), Get(baseObj, "album_id"), Get(baseObj, "albumid")))),
            Duration = duration,
            CoverUrl = GetCoverUrl(cover, 400),
            Cover = EmptyToNull(cover),
            AudioUrl = string.Empty,
            Hash = hash,
            MvHash = EmptyToNull(ReadString(Pick(Get(record, "video_hash"), Get(record, "mv_hash"), Get(record, "mvhash"), Get(record, "MVHash")))),
            MixSongId = mixSongId,
            FileId = fileId,
            Source = kind == KugouSongMapKind.Cloud ? "cloud" : EmptyToNull(ReadString(Get(record, "source"))),
            LyricSnippet = EmptyToNull(ReadString(Get(record, "Lyric"))),
            Privilege = privilege,
            PayType = payType,
            OldCpy = oldCpy,
            RelateGoods = BuildRelateGoods(record, audioInfo).ToArray(),
            IsOriginal = Has(record, "IsOriginal") ? ParseInt(Get(record, "IsOriginal")) == 1 : null,
            RecDesc = EmptyToNull(NormalizeText(ReadString(Pick(Get(recSongInfo, "rec_desc"), Get(recSongInfo, "recDesc"))))),
            SimilarDesc = EmptyToNull(NormalizeText(ReadString(Pick(Get(recSongInfo, "similar_desc"), Get(recSongInfo, "similarDesc")))))
        };
    }

    public static KugouPlaylist MapPlaylist(JsonNode? json)
    {
        var record = Object(json);
        var extra = Object(Get(record, "extra"));
        var id = ParseInt(Pick(Get(record, "specialid"), Get(record, "listid"), Get(record, "global_collection_id"), Get(extra, "specialid"), Get(record, "id")));
        var listid = ParseOptionalInt(Pick(Get(record, "listid"), Get(record, "specialid"), Get(extra, "specialid")));
        var listCreateListid = ParseOptionalInt(Pick(Get(record, "list_create_listid"), Get(extra, "specialid"), Get(record, "specialid")));
        var globalCollectionId = ReadString(Pick(Get(record, "global_collection_id"), Get(record, "gid"), Get(record, "specialid"), Get(extra, "global_collection_id")));
        var originalId = listCreateListid is > 0 ? listCreateListid : id != 0 ? id : ParseOptionalInt(Node(globalCollectionId));

        return new KugouPlaylist
        {
            Id = id,
            Listid = listid,
            GlobalCollectionId = EmptyToNull(globalCollectionId),
            ListCreateGid = EmptyToNull(ReadString(Pick(Get(record, "list_create_gid"), Get(record, "global_collection_id"), Get(record, "gid"), Get(record, "specialid"), Get(extra, "global_collection_id"), Get(extra, "global_special_id"), Get(extra, "specialid")))),
            ListCreateUserid = ParseOptionalInt(Pick(Get(record, "list_create_userid"), Get(extra, "list_create_userid"), Get(record, "userid"), Get(record, "suid"))),
            ListCreateListid = listCreateListid,
            MusiclibId = ParseOptionalInt(Pick(Get(record, "musiclib_id"), Get(extra, "musiclib_id"))),
            IpId = ParseOptionalInt(Pick(Get(record, "ip_id"), Get(extra, "ip_id"), Get(record, "id"))),
            Name = ReadString(Pick(Get(record, "specialname"), Get(record, "name"), Get(record, "title"))),
            Pic = FormatPic(Pick(Get(record, "flexible_cover"), Get(record, "pic"), Get(record, "imgurl"), Get(record, "cover"), Get(record, "img"), Get(record, "image_url"))),
            Intro = ReadString(Pick(Get(record, "intro"), Get(record, "description"), Get(record, "desc"), Get(record, "sub_title"))),
            Nickname = ReadString(Pick(Get(record, "nickname"), Get(record, "username"), Get(record, "author"), Get(record, "list_create_username"), Get(extra, "list_create_username"))),
            UserPic = FormatPic(Pick(Get(record, "user_pic"), Get(record, "avatar"), Get(record, "create_user_pic"), Get(record, "author_pic"))),
            Tags = ReadString(Get(record, "tags")),
            PlayCount = ParseInt(Pick(Get(record, "playcount"), Get(record, "play_count"), Get(record, "count"), Get(record, "play_total"), Get(extra, "play_count"))),
            Count = ParseInt(Pick(Get(record, "song_count"), Get(record, "songcount"), Get(record, "count"), Get(extra, "song_count"))),
            IsPrivate = ParseInt(Pick(Get(record, "is_pri"), Get(record, "is_private"))) == 1,
            Heat = ParseOptionalInt(Pick(Get(record, "collectcount"), Get(record, "collect_count"), Get(record, "collect_total"))),
            PublishDate = EmptyToNull(ReadString(Pick(Get(record, "publishtime"), Get(record, "publish_time"))).Split(' ')[0]),
            CreateTime = ParseOptionalInt(Pick(Get(record, "create_time"), Get(record, "addtime"))),
            UpdateTime = ParseOptionalInt(Get(record, "update_time")),
            Source = ParseInt(Pick(Get(record, "source"), Node(1))),
            Type = ParseOptionalInt(Get(record, "type")),
            IsDefault = ParseInt(Pick(Get(record, "is_def"), Get(record, "is_default"))) is 1 or 2,
            OriginalId = originalId
        };
    }

    public static KugouAlbum MapAlbum(JsonNode? json)
    {
        var record = Object(json);
        var extra = Object(Get(record, "extra"));
        var firstAuthor = ArrayFrom(Get(record, "authors"))?.Select(Object).FirstOrDefault(item => item is not null);
        return new KugouAlbum
        {
            Id = ParseInt(Pick(Get(record, "AlbumId"), Get(record, "albumid"), Get(record, "album_id"), Get(record, "id"), Get(extra, "album_id"))),
            Name = ReadString(Pick(Get(record, "AlbumName"), Get(record, "albumname"), Get(record, "album_name"), Get(record, "name"), Get(extra, "album_name"))),
            Pic = FormatPic(Pick(Get(record, "img"), Get(record, "Image"), Get(record, "imgurl"), Get(record, "sizable_cover"), Get(record, "pic"), Get(record, "cover"), Get(extra, "sizable_cover"), Get(extra, "cover"))),
            Intro = ReadString(Pick(Get(record, "intro"), Get(record, "album_intro"), Get(extra, "intro"))),
            SingerName = ReadString(Pick(Get(record, "SingerName"), Get(record, "singername"), Get(record, "singer_name"), Get(record, "author_name"), Get(record, "singer"), Get(extra, "singer_name"), Get(extra, "author_name"), Get(firstAuthor, "author_name"))),
            SingerId = ParseInt(Pick(Get(record, "SingerId"), Get(record, "SingerID"), Get(record, "singerid"), Get(record, "author_id"), Get(record, "singer_id"), Get(extra, "author_id"), Get(extra, "singer_id"), Get(firstAuthor, "author_id"))),
            PublishTime = ReadString(Pick(Get(record, "PublishTime"), Get(record, "publishtime"), Get(record, "publish_time"), Get(record, "publish_date"), Get(extra, "publish_time"))).Split(' ')[0],
            SongCount = ParseInt(Pick(Get(record, "SongCount"), Get(record, "song_count"), Get(record, "count"), Get(record, "songcount"), Get(record, "total_count"), Get(extra, "song_count"), Get(extra, "count"))),
            PlayCount = ParseInt(Pick(Get(record, "play_count"), Get(record, "play_times"), Get(record, "playcount"), Get(extra, "play_count"))),
            Heat = ParseInt(Pick(Get(record, "heat"), Get(record, "collect_count"), Get(record, "collectcount"))),
            Language = ReadString(Get(record, "language")),
            Type = ReadString(Get(record, "type")),
            Company = ReadString(Get(record, "company"))
        };
    }

    public static KugouArtist MapArtist(JsonNode? json)
    {
        var record = Object(json);
        var statistics = Object(Pick(Get(record, "statistics"), Get(record, "stat"), Get(record, "stats"), Get(record, "count_info"), Get(record, "base")));
        var longIntro = Get(record, "long_intro");
        var intro = string.Empty;
        var introArray = ArrayFrom(longIntro);
        if (introArray.Length > 0)
        {
            intro = string.Join("\n\n", introArray.Select(item => ReadString(Get(Object(item), "content"))).Where(item => item.Length > 0));
        }
        else
        {
            intro = ReadString(Pick(longIntro, Get(record, "intro"), Get(record, "profile"), Get(record, "singer_intro")));
        }

        var followedRaw = Pick(Get(record, "is_followed"), Get(record, "is_follow"), Get(record, "followed"), Get(record, "follow"));
        bool? followed = followedRaw is null ? null : ParseBool(followedRaw);
        return new KugouArtist
        {
            Id = ParseInt(Pick(Get(record, "AuthorId"), Get(record, "author_id"), Get(record, "singerid"), Get(record, "singer_id"), Get(record, "id"))),
            Name = ReadString(Pick(Get(record, "AuthorName"), Get(record, "author_name"), Get(record, "singername"), Get(record, "name"), Get(record, "singer_name"))),
            Pic = FormatPic(Pick(Get(record, "sizable_avatar"), Get(record, "Avatar"), Get(record, "imgurl"), Get(record, "avatar"), Get(record, "pic"), Get(record, "image"), Get(record, "singer_img"))),
            Intro = intro,
            SongCount = ParseInt(Pick(Get(record, "AudioCount"), Get(record, "audio_count"), Get(record, "audiocount"), Get(record, "audio_num"), Get(record, "audionum"), Get(record, "audionums"), Get(record, "song_count"), Get(record, "songcount"), Get(record, "song_num"), Get(record, "songnum"), Get(record, "songnums"), Get(record, "songs_num"), Get(record, "songsnum"), Get(record, "music_count"), Get(record, "musiccount"), Get(record, "count"), Get(record, "total"), Get(record, "total_count"), Get(statistics, "audio_count"), Get(statistics, "song_count"), Get(statistics, "songnum"), Get(statistics, "songnums"), Get(statistics, "count"))),
            AlbumCount = ParseInt(Pick(Get(record, "AlbumCount"), Get(record, "album_count"), Get(record, "albumcount"), Get(record, "album_num"), Get(record, "albumnum"), Get(record, "albumnums"), Get(statistics, "album_count"), Get(statistics, "albumcount"), Get(statistics, "albumnum"))),
            MvCount = ParseInt(Pick(Get(record, "VideoCount"), Get(record, "video_count"), Get(record, "videocount"), Get(record, "mv_count"), Get(record, "mvcount"), Get(record, "mv_num"), Get(record, "mvnum"), Get(record, "mvnums"), Get(statistics, "video_count"), Get(statistics, "mv_count"), Get(statistics, "mvnum"))),
            FansCount = ParseInt(Pick(Get(record, "FansNum"), Get(record, "fansnums"), Get(record, "fans_count"), Get(record, "fans"), Get(record, "fans_num"), Get(record, "fansnum"), Get(record, "fanscount"), Get(record, "fans_count_fmt"), Get(statistics, "fans_count"), Get(statistics, "fansnum"))),
            Heat = ParseInt(Pick(Get(record, "heat"), Get(record, "hot"), Get(record, "heatoffset"), Get(statistics, "heat"))),
            Birthday = EmptyToNull(ReadString(Get(record, "birthday"))),
            IsFollowed = followed
        };
    }

    public static KugouRank MapRank(JsonNode? json)
    {
        var record = Object(json);
        var typeInfo = Object(Get(record, "type_info"));
        var group = ReadString(Pick(Get(record, "group"), Get(record, "rank_type_name"), Get(record, "type_name"), Get(typeInfo, "name"), Get(typeInfo, "title")));
        var rankTypeName = ReadString(Pick(Get(record, "rank_type_name"), Get(record, "type_name"), Node(group), Get(typeInfo, "name"), Get(typeInfo, "title")));
        return new KugouRank
        {
            Id = ParseInt(Pick(Get(record, "rankid"), Get(record, "id"))),
            Name = ReadString(Pick(Get(record, "rankname"), Get(record, "name"), Get(record, "title"))),
            Pic = FormatPic(Pick(Get(record, "imgurl"), Get(record, "pic"), Get(record, "cover"), Get(record, "image"))),
            RankType = ParseOptionalInt(Pick(Get(record, "ranktype"), Get(record, "rank_type"), Get(record, "type"))),
            Type = EmptyToNull(ReadString(Pick(Get(record, "type"), Get(record, "rank_type")))),
            Group = EmptyToNull(group),
            RankTypeName = EmptyToNull(rankTypeName),
            UpdateFrequency = EmptyToNull(ReadString(Pick(Get(record, "updatefrequency"), Get(record, "updateFrequency"), Get(record, "update"))))
        };
    }

    public static KugouComment MapComment(JsonNode? json, bool isHot = false, bool isStar = false)
    {
        var record = Object(json);
        var likeRecord = Object(Get(record, "like"));
        var userRecord = Object(Get(record, "user"));
        var id = ReadString(Pick(Get(record, "comment_id"), Get(record, "id")));
        var userName = ReadString(Pick(Get(record, "user_name"), Get(record, "nickname"), Get(userRecord, "name"), Get(userRecord, "nickname"), Node("匿名用户")), "匿名用户");
        var avatar = ReadString(Pick(Get(record, "user_pic"), Get(record, "user_img"), Get(record, "avatar"), Get(userRecord, "avatar"), Get(userRecord, "pic")));
        var addTime = ReadString(Pick(Get(record, "addtime"), Get(record, "add_time"), Get(record, "time")));
        var likeCount = ParseInt(Pick(Get(likeRecord, "count"), Get(record, "like_count"), Get(record, "likeCount"), Get(record, "like_num"), Get(record, "reply_like_count"), Get(record, "count")));
        var replyCount = ParseInt(Pick(Get(record, "reply_num"), Get(record, "reply_count"), Get(record, "replyCount")));
        var specialId = ReadString(Pick(Get(record, "special_child_id"), Get(record, "special_id"), Get(record, "specialId"), Get(record, "specialid"), Get(record, "childrenid")));
        var tid = ReadString(Pick(Get(record, "tid"), Get(record, "id"), Get(record, "comment_id"), Get(record, "commentId")));
        var mixSongId = ReadString(Pick(Get(record, "mixsongid"), Get(record, "audio_id"), Get(record, "album_audio_id"), Get(record, "mixSongId")));
        return new KugouComment
        {
            Id = id,
            UserName = userName,
            UserPic = EmptyToNull(avatar),
            Avatar = avatar,
            Content = ReadString(Get(record, "content")),
            Time = addTime,
            AddTime = EmptyToNull(addTime),
            LikeCount = likeCount,
            Like = new KugouCommentLike { Count = likeCount },
            ReplyCount = replyCount,
            ReplyNum = replyCount,
            IsHot = isHot || ParseBool(Pick(Get(record, "isHot"), Get(record, "is_hot"), Get(record, "hot"))),
            IsStar = isStar || ParseBool(Pick(Get(record, "isStar"), Get(record, "is_star"), Get(record, "star"))),
            SpecialId = EmptyToNull(specialId),
            SpecialChildId = EmptyToNull(specialId),
            Tid = EmptyToNull(tid),
            Code = EmptyToNull(ReadString(Get(record, "code"))),
            MixSongId = EmptyToNull(mixSongId),
            AudioId = EmptyToNull(ReadString(Get(record, "audio_id"))),
            AlbumAudioId = EmptyToNull(ReadString(Get(record, "album_audio_id")))
        };
    }

    public static KugouUser MapUser(JsonNode? json)
    {
        var record = Object(json);
        var data = Object(Get(record, "data"));
        var info = Object(Get(record, "info"));
        var userInfo = Object(Pick(Get(record, "userinfo"), Get(record, "user_info")));
        var profile = Object(Get(record, "profile"));
        var account = Object(Get(record, "account"));
        var primary = FirstObject(userInfo, profile, info, data, record);
        var extendsInfo = FirstObject(Object(Get(record, "extends")), Object(Get(record, "extendsInfo")), Object(Get(primary, "extends")), Object(Get(primary, "extendsInfo")));
        var detail = FirstObject(Object(Get(record, "detail")), Object(Get(primary, "detail")), Object(Get(extendsInfo, "detail")));
        var vip = FirstObject(Object(Get(record, "vip")), Object(Get(primary, "vip")), Object(Get(extendsInfo, "vip")));

        return new KugouUser
        {
            UserId = ParseInt(Pick(Get(record, "userid"), Get(record, "userId"), Get(record, "user_id"), Get(record, "uid"), Get(record, "id"), Get(primary, "userid"), Get(primary, "userId"), Get(primary, "user_id"), Get(primary, "uid"), Get(primary, "id"), Get(account, "userid"), Get(account, "userId"), Get(account, "user_id"), Get(account, "uid"), Get(account, "id"), Get(detail, "userid"), Get(detail, "userId"), Get(detail, "user_id"), Get(detail, "uid"), Get(detail, "id"))),
            Token = ReadString(Pick(Get(record, "token"), Get(primary, "token"), Get(account, "token"))),
            Username = EmptyToNull(ReadString(Pick(Get(primary, "username"), Get(primary, "userName"), Get(record, "username"), Get(record, "userName"), Get(account, "username")))),
            Nickname = EmptyToNull(ReadString(Pick(Get(primary, "nickname"), Get(primary, "userName"), Get(primary, "username"), Get(record, "nickname"), Get(record, "userName")))),
            Mobile = EmptyToNull(ReadString(Pick(Get(primary, "mobile"), Get(record, "mobile"), Get(account, "mobile")))),
            Pic = EmptyToNull(ReadString(Pick(Get(primary, "pic"), Get(primary, "userPic"), Get(primary, "avatar"), Get(profile, "avatarUrl"), Get(profile, "avatar"), Get(record, "pic"), Get(record, "userPic"), Get(record, "avatar")))),
            T1 = EmptyToNull(ReadString(Pick(Get(record, "t1"), Get(primary, "t1"), Get(account, "t1")))),
            Expires = ParseOptionalInt(Pick(Get(primary, "expires"), Get(record, "expires"), Get(account, "expires"))),
            VipType = ParseOptionalInt(Pick(Get(primary, "vip_type"), Get(record, "vip_type"), Get(vip, "vip_type"))),
            PGrade = ParseOptionalInt(Pick(Get(primary, "p_grade"), Get(record, "p_grade"), Get(detail, "p_grade"))),
            Detail = detail,
            Vip = vip
        };
    }

    public static KugouVideo? MapVideo(JsonNode? payload, string targetHash = "")
    {
        var mvRecord = ResolveMvRecords(payload).FirstOrDefault();
        var detailRecord = ResolveDetailRecord(payload);
        var privilegeRecord = ResolvePrivilegeRecord(payload, targetHash);
        var rootObject = Object(payload);
        var isEnvelope = Has(rootObject, "data") || Has(rootObject, "status") || Has(rootObject, "error_code") || Has(rootObject, "code");
        var record = mvRecord ?? detailRecord ?? privilegeRecord ?? (isEnvelope ? null : rootObject);
        if (record is null)
        {
            return null;
        }

        var hash = ReadString(Pick(Get(record, "hash"), Node(targetHash)), targetHash);
        var cover = ReadString(Pick(Get(record, "hdpic"), Get(record, "thumb"), Get(record, "img"), Get(record, "image")));
        return new KugouVideo
        {
            Id = ReadString(Pick(Get(record, "video_id"), Get(record, "id"), Node(hash)), hash),
            Hash = hash,
            Title = ReadString(Pick(Get(record, "mv_name"), Get(record, "name"), Get(record, "video_name"), Node("MV播放")), "MV播放"),
            Description = EmptyToNull(ReadString(Pick(Get(record, "desc"), Get(record, "remark")))),
            Remark = EmptyToNull(ReadString(Get(record, "remark"))),
            Topic = EmptyToNull(ReadString(Get(record, "topic"))),
            CoverUrl = GetCoverUrl(cover, 720),
            Duration = NormalizeDuration(ParseInt(Get(record, "duration"))),
            PlayCount = ParseOptionalInt(Pick(Get(record, "play_times"), Get(record, "hit"), Get(record, "play_count"), Get(record, "hot"))),
            PublishTime = ParsePublishTime(Get(record, "publish_time")),
            AlbumAudioId = EmptyToNull(ReadString(Pick(Get(record, "album_audio_id"), Get(record, "audio_id")))),
            SongName = EmptyToNull(ReadString(Pick(Get(record, "mv_name"), Get(record, "name")))),
            ArtistName = EmptyToNull(ReadString(Pick(Get(record, "singer"), Get(record, "singer_name")))),
            AlbumName = EmptyToNull(ReadString(Get(record, "other_desc"))),
            Authors = ParseVideoAuthors(Get(record, "authors")).ToArray(),
            Tags = ParseVideoTags(Get(record, "tags")).ToArray(),
            Sources = CollectVideoSources(record).ToArray(),
            CollectionCount = ParseOptionalInt(Get(record, "collection_total")),
            DownloadCount = ParseOptionalInt(Get(record, "download_total")),
            HotScore = ParseOptionalInt(Get(record, "hot")),
            Recommend = Has(record, "is_recommend") ? ParseInt(Get(record, "is_recommend")) == 1 : null
        };
    }

    public static KugouVideo? MapVideo(KugouResponse response, string targetHash = "") => MapVideo(Parse(response), targetHash);

    public static string ExtractVideoUrl(KugouResponse response, string targetHash = "")
    {
        var root = Object(Parse(response));
        var data = Object(Get(root, "data"));
        var lowerHash = targetHash.Trim().ToLowerInvariant();
        var entry = !string.IsNullOrWhiteSpace(lowerHash) ? Object(Get(data, lowerHash)) : null;
        entry ??= data?.Select(item => Object(item.Value)).FirstOrDefault(item => item is not null);
        var backup = ArrayFrom(Get(entry, "backupdownurl")).FirstOrDefault();
        return ReadString(Pick(Get(entry, "downurl"), Get(entry, "url"), Get(entry, "play_url"), backup));
    }

    public static IReadOnlyList<KugouVideoSource> MapVideoSourcesFromPrivilege(KugouResponse response)
    {
        var root = Parse(response);
        return ArrayFrom(Get(Object(root), "data"))
            .Select(Object)
            .Where(item => item is not null)
            .Select(item =>
            {
                var hash = ReadString(Get(item, "hash"));
                if (string.IsNullOrWhiteSpace(hash))
                {
                    return null;
                }

                var info = Object(Get(item, "info"));
                var level = ParseInt(Get(item, "level"));
                return new KugouVideoSource
                {
                    Hash = hash,
                    Url = string.Empty,
                    Thumb = GetCoverUrl(ReadString(Pick(Get(item, "hdpic"), Get(item, "thumb"), Get(item, "img"), Get(item, "image"))), 360),
                    Label = level switch
                    {
                        5 => "1080P",
                        4 => "720P",
                        3 => "540P",
                        2 => "432P",
                        1 => "270P",
                        _ => $"等级 {level}"
                    },
                    Codec = "MP4",
                    Bitrate = ParseOptionalInt(Get(info, "bitrate")),
                    Size = ParseOptionalLong(Pick(Get(info, "filesize"), Get(item, "filesize")))
                };
            })
            .Where(item => item is not null)
            .Cast<KugouVideoSource>()
            .ToArray();
    }

    private static JsonNode? Parse(KugouResponse response)
    {
        try
        {
            return JsonNode.Parse(response.BodyText);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<JsonNode?> ExtractList(JsonNode? root)
    {
        var direct = ArrayFrom(root);
        if (direct.Length > 0)
        {
            return direct;
        }

        var singerGroups = ArrayFrom(Path(root, "data.info"));
        var singers = singerGroups.SelectMany(group => ArrayFrom(Get(Object(group), "singer"))).ToArray();
        if (singers.Length > 0)
        {
            return singers;
        }

        var paths = new[]
        {
            "data.info.singer",
            "data.special_list",
            "data.lists",
            "data.list",
            "data.info",
            "data.song_list",
            "data.songlist",
            "data.songs",
            "data.items",
            "songs.list",
            "info.list",
            "payload.list",
            "payload.data",
            "lists",
            "list",
            "songs",
            "songlist",
            "items",
            "info",
            "data"
        };

        foreach (var path in paths)
        {
            var array = ArrayFrom(Path(root, path));
            if (array.Length > 0)
            {
                return array;
            }
        }

        return System.Array.Empty<JsonNode?>();
    }

    private static JsonObject? ExtractFirstObject(JsonNode? root)
    {
        var first = ExtractList(root).Select(Object).FirstOrDefault(item => item is not null);
        if (first is not null)
        {
            return first;
        }

        return FirstObject(Object(Get(Object(root), "data")), Object(Get(Object(root), "info")), Object(root));
    }

    private static int ExtractTotal(JsonNode? root, int fallback)
    {
        var record = Object(root);
        var data = Object(Get(record, "data"));
        var total = ParseInt(Pick(
            Get(data, "total"),
            Get(data, "totalCount"),
            Get(data, "count"),
            Get(data, "counts"),
            Get(data, "song_count"),
            Get(data, "list_count"),
            Get(record, "total"),
            Get(record, "totalCount"),
            Get(record, "count"),
            Get(record, "counts"),
            Get(record, "song_count"),
            Get(record, "list_count")));
        return total > 0 ? total : fallback;
    }

    private static JsonObject? Object(JsonNode? node) => node as JsonObject;

    private static JsonObject? FirstObject(params JsonObject?[] objects) => objects.FirstOrDefault(item => item is not null && item.Count > 0);

    private static bool Has(JsonObject? record, string key)
    {
        if (record is null)
        {
            return false;
        }

        return record.ContainsKey(key) || record.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonNode? Get(JsonObject? record, string key)
    {
        if (record is null)
        {
            return null;
        }

        if (record.TryGetPropertyValue(key, out var exact))
        {
            return exact;
        }

        foreach (var item in record)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return item.Value;
            }
        }

        return null;
    }

    private static JsonNode? GetObject(JsonNode? node, string key) => Get(Object(node), key);

    private static JsonNode? Path(JsonNode? node, string path)
    {
        var current = node;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Get(Object(current), part);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static JsonNode? Pick(params JsonNode?[] values)
    {
        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) && text.Length == 0)
            {
                continue;
            }

            if (value is JsonArray jsonArray && jsonArray.Count == 0)
            {
                continue;
            }

            return value;
        }

        return null;
    }

    private static JsonArray? Array(JsonNode? value) => value as JsonArray;

    private static JsonNode?[] ArrayFrom(JsonNode? value) => Array(value)?.Select(item => item).ToArray() ?? System.Array.Empty<JsonNode?>();

    private static JsonNode Node(string value) => JsonValue.Create(value)!;

    private static JsonNode Node(int value) => JsonValue.Create(value)!;

    private static string ReadString(JsonNode? value, string fallback = "")
    {
        if (value is null)
        {
            return fallback;
        }

        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var text))
            {
                return text;
            }

            if (jsonValue.TryGetValue<long>(out var integer))
            {
                return integer.ToString(CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<double>(out var number))
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<bool>(out var boolean))
            {
                return boolean ? "true" : "false";
            }
        }

        return value.ToJsonString();
    }

    private static int ParseInt(JsonNode? value) => (int)Math.Clamp(ParseLong(value), int.MinValue, int.MaxValue);

    private static int? ParseOptionalInt(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        return ParseInt(value);
    }

    private static long ParseLong(JsonNode? value)
    {
        if (value is null)
        {
            return 0;
        }

        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<long>(out var integer))
            {
                return integer;
            }

            if (jsonValue.TryGetValue<double>(out var number))
            {
                return (long)number;
            }
        }

        var text = ReadString(value);
        var match = LeadingIntegerRegex().Match(text);
        if (match.Success && long.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
    }

    private static long? ParseOptionalLong(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        return ParseLong(value);
    }

    private static bool ParseBool(JsonNode? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        var text = ReadString(value).Trim();
        return text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static int NormalizeDuration(int value) => value > 1000 ? value / 1000 : value;

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string FormatPic(JsonNode? value)
    {
        var pic = ReadString(value);
        if (string.IsNullOrWhiteSpace(pic))
        {
            return string.Empty;
        }

        pic = pic.Replace("{size}", "400", StringComparison.Ordinal).Replace("http://", "https://", StringComparison.OrdinalIgnoreCase);
        if (pic.StartsWith("//", StringComparison.Ordinal))
        {
            pic = $"https:{pic}";
        }

        return pic;
    }

    private static string GetCoverUrl(string? url, int size = 400)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return DefaultCoverUrl;
        }

        return url.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase)
            .Replace("{size}", size.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("c1.kgimg.com", "imge.kugou.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeText(string raw) => TextWhitespaceRegex().Replace(raw.Replace('_', ' '), " ").Trim();

    private static string ProcessSongTitle(string rawName)
    {
        if (!rawName.Contains(" - ", StringComparison.Ordinal))
        {
            return rawName;
        }

        var parts = rawName.Split(" - ", StringSplitOptions.None);
        return parts.Length > 1 ? string.Join(" - ", parts.Skip(1)) : rawName;
    }

    private static string CleanupAudioExtension(string value)
    {
        foreach (var extension in new[] { ".mp3", ".flac", ".wav", ".aac", ".m4a", ".ape" })
        {
            if (value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return value[..^extension.Length];
            }
        }

        return value;
    }

    private static IEnumerable<KugouSongArtist> BuildArtists(JsonObject? record, JsonObject? audioInfo)
    {
        var singerInfo = ArrayFrom(Pick(Get(record, "singerinfo"), Get(audioInfo, "singerinfo"), Get(record, "authors"), Get(record, "Singers")));
        foreach (var raw in singerInfo)
        {
            var item = Object(raw);
            var name = ReadString(Pick(Get(item, "name"), Get(item, "AuthorName"), Get(item, "author_name"), Get(item, "singername"), Get(item, "singer"))).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            yield return new KugouSongArtist
            {
                Id = EmptyToNull(ReadString(Pick(Get(item, "id"), Get(item, "AuthorId"), Get(item, "author_id"), Get(item, "singerid"), Get(item, "singer_id")))),
                Name = name
            };
        }
    }

    private static IEnumerable<KugouSongRelateGood> BuildRelateGoods(JsonObject? record, JsonObject? audioInfo)
    {
        var rawGoods = ArrayFrom(Pick(Get(record, "relate_goods"), Get(record, "relateGoods"), Get(audioInfo, "relate_goods"), Get(audioInfo, "relateGoods")));
        foreach (var raw in rawGoods)
        {
            var item = Object(raw);
            var hash = ReadString(Pick(Get(item, "hash"), Get(item, "Hash"), Get(item, "file_hash"), Get(item, "fileHash")));
            var quality = ReadString(Pick(Get(item, "quality"), Get(item, "Quality"), Get(item, "q")));
            var level = ParseOptionalInt(Pick(Get(item, "level"), Get(item, "quality_level"), Get(item, "qualityLevel"), Get(item, "quality")));
            if (!string.IsNullOrWhiteSpace(hash) || !string.IsNullOrWhiteSpace(quality) || level is not null)
            {
                yield return new KugouSongRelateGood { Hash = EmptyToNull(hash), Quality = EmptyToNull(quality), Level = level };
            }
        }

        foreach (var (hashNode, quality) in new[]
                 {
                     (Pick(Get(record, "128hash"), Get(record, "hash_128"), Get(audioInfo, "hash_128")), "128"),
                     (Pick(Get(record, "320hash"), Get(record, "hash_320"), Get(audioInfo, "hash_320")), "320"),
                     (Pick(Get(record, "sqhash"), Get(record, "hash_flac"), Get(audioInfo, "hash_flac")), "flac"),
                     (Pick(Get(record, "highhash"), Get(record, "hash_high"), Get(audioInfo, "hash_high")), "high"),
                     (Pick(Get(record, "hash_320"), Get(audioInfo, "hash_320")), "320"),
                     (Pick(Get(record, "hash_flac"), Get(audioInfo, "hash_flac")), "flac"),
                     (Pick(Get(record, "hash_high"), Get(audioInfo, "hash_high")), "high"),
                     (Pick(Get(Object(Get(record, "HQ")), "Hash"), Get(Object(Get(record, "HQ")), "hash")), "320"),
                     (Pick(Get(Object(Get(record, "SQ")), "Hash"), Get(Object(Get(record, "SQ")), "hash")), "flac"),
                     (Pick(Get(Object(Get(record, "Res")), "Hash"), Get(Object(Get(record, "Res")), "hash")), "high")
                 })
        {
            var hash = ReadString(hashNode);
            if (!string.IsNullOrWhiteSpace(hash))
            {
                yield return new KugouSongRelateGood { Hash = hash, Quality = quality };
            }
        }
    }

    private static KugouSong CopySong(KugouSong source, long mxid, long playedAt, int? playCount, string historyKey)
    {
        return new KugouSong
        {
            Id = !string.IsNullOrWhiteSpace(source.Id) ? source.Id : (mxid > 0 ? mxid.ToString(CultureInfo.InvariantCulture) : string.Empty),
            SongId = source.SongId,
            Title = source.Title,
            Name = source.Name,
            Artist = source.Artist,
            Language = source.Language,
            AlbumName = source.AlbumName,
            Artists = source.Artists,
            Singers = source.Singers,
            Album = source.Album,
            AlbumId = source.AlbumId,
            Duration = source.Duration,
            CoverUrl = source.CoverUrl,
            Cover = source.Cover,
            AudioUrl = source.AudioUrl,
            Hash = source.Hash,
            MvHash = source.MvHash,
            MixSongId = mxid > 0 ? mxid : source.MixSongId,
            FileId = source.FileId,
            Source = source.Source,
            Lyric = source.Lyric,
            LyricSnippet = source.LyricSnippet,
            Privilege = source.Privilege,
            PayType = source.PayType,
            OldCpy = source.OldCpy,
            RelateGoods = source.RelateGoods,
            IsOriginal = source.IsOriginal,
            RecDesc = source.RecDesc,
            SimilarDesc = source.SimilarDesc,
            LastPlayedAt = playedAt > 0 ? playedAt : null,
            PlayCount = playCount,
            HistoryKey = historyKey
        };
    }

    private static string ResolveUrl(JsonNode? payload)
    {
        if (payload is null)
        {
            return string.Empty;
        }

        if (payload is JsonValue)
        {
            return ReadString(payload).Trim();
        }

        var array = ArrayFrom(payload);
        if (array.Length > 0)
        {
            return array.Select(ResolveUrl).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty;
        }

        var record = Object(payload);
        var urlField = Pick(
            Get(record, "url"),
            Get(record, "play_url"),
            Get(record, "playUrl"),
            Get(record, "downurl"),
            Get(record, "down_url"),
            Get(record, "tracker_url"),
            Get(record, "en_tracker_url"));
        var url = ResolveUrl(urlField);
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var backupUrl = ResolveBackupUrl(Pick(
            Get(record, "backup_url"),
            Get(record, "backupUrl"),
            Get(record, "backupdownurl")));
        if (!string.IsNullOrWhiteSpace(backupUrl))
        {
            return backupUrl;
        }

        return ResolveUrl(Pick(Get(record, "data"), Get(record, "info")));
    }

    private static AudioUrlCandidate ResolveAudioUrlCandidate(JsonNode? payload)
    {
        if (payload is null)
        {
            return default;
        }

        if (payload is JsonValue)
        {
            var url = ReadString(payload).Trim();
            return string.IsNullOrWhiteSpace(url) ? default : new AudioUrlCandidate(url, null, null, false);
        }

        var array = ArrayFrom(payload);
        if (array.Length > 0)
        {
            foreach (var item in array)
            {
                var candidate = ResolveAudioUrlCandidate(item);
                if (!string.IsNullOrWhiteSpace(candidate.Url))
                {
                    return candidate;
                }
            }

            return default;
        }

        var record = Object(payload);
        var info = Object(Get(record, "info"));
        var normalUrl = ResolveUrl(Pick(
            Get(record, "url"),
            Get(record, "play_url"),
            Get(record, "playUrl"),
            Get(record, "downurl"),
            Get(record, "down_url"),
            Get(record, "tracker_url")));
        if (!string.IsNullOrWhiteSpace(normalUrl))
        {
            return new AudioUrlCandidate(normalUrl, record, info, false);
        }

        var encryptedUrl = ResolveUrl(Get(record, "en_tracker_url"));
        if (!string.IsNullOrWhiteSpace(encryptedUrl))
        {
            return new AudioUrlCandidate(encryptedUrl, record, info, true);
        }

        var backupUrl = ResolveBackupUrl(Pick(
            Get(record, "backup_url"),
            Get(record, "backupUrl"),
            Get(record, "backupdownurl")));
        if (!string.IsNullOrWhiteSpace(backupUrl))
        {
            return new AudioUrlCandidate(backupUrl, record, info, false);
        }

        var dataCandidate = ResolveAudioUrlCandidate(Get(record, "data"));
        if (!string.IsNullOrWhiteSpace(dataCandidate.Url))
        {
            return dataCandidate;
        }

        return ResolveAudioUrlCandidate(Get(record, "info"));
    }

    private static string ResolveBackupUrl(JsonNode? payload)
    {
        if (payload is null)
        {
            return string.Empty;
        }

        if (payload is JsonValue)
        {
            var value = ReadString(payload).Trim();
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? value
                : string.Empty;
        }

        var array = ArrayFrom(payload);
        if (array.Length > 0)
        {
            return array.Select(ResolveBackupUrl).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty;
        }

        return Object(payload)?.Select(item => ResolveBackupUrl(item.Value)).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty;
    }

    private static KugouTrackLoudness? ResolveTrackLoudness(JsonNode? payload)
    {
        var record = Object(payload);
        var source = Has(record, "volume") ? record : Object(Get(record, "data"));
        if (!Has(source, "volume"))
        {
            return null;
        }

        var lufs = ParseDouble(Get(source, "volume"));
        var gain = ParseDouble(Pick(Get(source, "volume_gain"), Get(source, "volumeGain")));
        var peak = ParseDouble(Pick(Get(source, "volume_peak"), Get(source, "volumePeak")));
        if (Math.Abs(lufs) < double.Epsilon && Math.Abs(gain) < double.Epsilon)
        {
            return null;
        }

        return new KugouTrackLoudness { Lufs = lufs, Gain = gain, Peak = Math.Max(0, peak) };
    }

    private static double ParseDouble(JsonNode? value)
    {
        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<double>(out var number))
            {
                return number;
            }

            if (jsonValue.TryGetValue<long>(out var integer))
            {
                return integer;
            }
        }

        return double.TryParse(ReadString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static JsonObject?[] ResolveMvRecords(JsonNode? payload)
    {
        var root = Object(payload);
        var data = Array(Get(root, "data"));
        if (data is null || data.Count == 0)
        {
            return System.Array.Empty<JsonObject?>();
        }

        var firstGroup = Array(data[0]);
        if (firstGroup is null)
        {
            return System.Array.Empty<JsonObject?>();
        }

        return firstGroup.Select(Object).Where(item => item is not null).ToArray();
    }

    private static JsonObject? ResolveDetailRecord(JsonNode? payload)
    {
        var data = Array(Get(Object(payload), "data"));
        return data is { Count: > 0 } ? Object(data[0]) : null;
    }

    private static JsonObject? ResolvePrivilegeRecord(JsonNode? payload, string targetHash)
    {
        var data = Object(Get(Object(payload), "data"));
        if (data is null)
        {
            return null;
        }

        var lowerHash = targetHash.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(lowerHash) && Object(Get(data, lowerHash)) is { } matched)
        {
            return matched;
        }

        return data.Select(item => Object(item.Value)).FirstOrDefault(item => item is not null);
    }

    private static IEnumerable<KugouVideoAuthor> ParseVideoAuthors(JsonNode? value)
    {
        return ArrayFrom(value)
            .Select(Object)
            .Where(item => item is not null)
            .Select(item => new KugouVideoAuthor
            {
                Id = EmptyToNull(ReadString(Pick(Get(item, "author_id"), Get(item, "id"), Get(item, "user_id")))),
                Name = ReadString(Pick(Get(item, "author_name"), Get(item, "name"))),
                Avatar = EmptyToNull(GetCoverUrl(ReadString(Pick(Get(item, "sizable_avatar"), Get(item, "avatar"))), 240))
            })
            .Where(item => item.Name.Length > 0)
            .GroupBy(item => $"{item.Id}-{item.Name}")
            .Select(group => group.First());
    }

    private static IEnumerable<KugouVideoTag> ParseVideoTags(JsonNode? value)
    {
        return ArrayFrom(value)
            .Select(Object)
            .Where(item => item is not null)
            .Select(item => new KugouVideoTag
            {
                Id = EmptyToNull(ReadString(Pick(Get(item, "tag_id"), Get(item, "id")))),
                Name = ReadString(Pick(Get(item, "tag_name"), Get(item, "name")))
            })
            .Where(item => item.Name.Length > 0)
            .GroupBy(item => item.Name)
            .Select(group => group.First());
    }

    private static IEnumerable<KugouVideoSource> CollectVideoSources(JsonObject record)
    {
        var thumb = GetCoverUrl(ReadString(Pick(Get(record, "hdpic"), Get(record, "thumb"), Get(record, "img"), Get(record, "image"))), 360);
        foreach (var (key, label) in new[] { ("h265", "H.265"), ("h264", "H.264"), ("mkv", "MKV") })
        {
            var codec = Object(Get(record, key));
            var source = PickFirstVideoSource(codec, label, thumb);
            if (source is not null)
            {
                yield return source;
                yield break;
            }
        }
    }

    private static KugouVideoSource? PickFirstVideoSource(JsonObject? codecRecord, string codecLabel, string thumb)
    {
        foreach (var (quality, label, width, height) in new[]
                 {
                     ("fhd", "1080P", 1920, 1080),
                     ("hd", "720P", 1280, 720),
                     ("qhd", "540P", 960, 540),
                     ("sd", "432P", 768, 432),
                     ("ld", "270P", 480, 270)
                 })
        {
            var hash = ReadString(Get(codecRecord, $"{quality}_hash"));
            if (string.IsNullOrWhiteSpace(hash))
            {
                continue;
            }

            return new KugouVideoSource
            {
                Hash = hash,
                Url = string.Empty,
                Label = label,
                Thumb = thumb,
                Codec = codecLabel,
                Bitrate = ParseOptionalInt(Get(codecRecord, $"{quality}_bitrate")),
                Width = ParseOptionalInt(Get(codecRecord, $"{quality}_width")) ?? width,
                Height = ParseOptionalInt(Get(codecRecord, $"{quality}_height")) ?? height,
                Size = ParseOptionalLong(Get(codecRecord, $"{quality}_filesize"))
            };
        }

        return null;
    }

    private static long? ParsePublishTime(JsonNode? value)
    {
        var text = ReadString(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text.Replace('-', '/'), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.ToUnixTimeSeconds()
            : null;
    }

    [GeneratedRegex("^-?\\d+")]
    private static partial Regex LeadingIntegerRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex TextWhitespaceRegex();
}
