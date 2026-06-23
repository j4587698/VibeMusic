using System.Text.Json.Nodes;

namespace KuGou.Lite;

public sealed record KugouTypedResult<T>(T? Data, KugouResponse Raw);

public sealed record KugouListResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    KugouResponse Raw,
    IReadOnlyList<T>? HotItems = null);

public sealed record KugouResolvedAudioSource(
    string Url,
    string? Quality,
    string Effect,
    KugouResponse Raw,
    string? EnEkey = null,
    long FileSize = 0);

public sealed record KugouConceptVipEnsureResult(
    bool IsLoggedIn,
    bool IsVipBefore,
    bool ClaimAttempted,
    bool ClaimSucceeded,
    bool UpgradeAttempted,
    bool UpgradeSucceeded,
    bool IsVipAfter,
    string ReceiveDay,
    KugouResponse? VipBefore,
    KugouResponse? ClaimResponse,
    KugouResponse? UpgradeResponse,
    KugouResponse? VipAfter);

public sealed class KugouSongRelateGood
{
    public string? Hash { get; init; }
    public string? Quality { get; init; }
    public int? Level { get; init; }
}

public sealed class KugouSongArtist
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class KugouSong
{
    public string Id { get; init; } = string.Empty;
    public string? SongId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string Artist { get; init; } = string.Empty;
    public string? Language { get; init; }
    public string? AlbumName { get; init; }
    public IReadOnlyList<KugouSongArtist> Artists { get; init; } = Array.Empty<KugouSongArtist>();
    public IReadOnlyList<KugouSongArtist> Singers { get; init; } = Array.Empty<KugouSongArtist>();
    public string? Album { get; init; }
    public string? AlbumId { get; init; }
    public int Duration { get; init; }
    public string CoverUrl { get; init; } = string.Empty;
    public string? Cover { get; init; }
    public string AudioUrl { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
    public string? MvHash { get; init; }
    public long MixSongId { get; init; }
    public long? FileId { get; init; }
    public string? Source { get; init; }
    public string? Lyric { get; init; }
    public string? LyricSnippet { get; init; }
    public int? Privilege { get; init; }
    public int? PayType { get; init; }
    public int? OldCpy { get; init; }
    public IReadOnlyList<KugouSongRelateGood> RelateGoods { get; init; } = Array.Empty<KugouSongRelateGood>();
    public bool? IsOriginal { get; init; }
    public string? RecDesc { get; init; }
    public string? SimilarDesc { get; init; }
    public int? PlayCount { get; init; }
    public long? LastPlayedAt { get; init; }
    public string? HistoryKey { get; init; }
}

public sealed class KugouPlaylist
{
    public int Id { get; init; }
    public string? GlobalCollectionId { get; init; }
    public string? ListCreateGid { get; init; }
    public int? ListCreateUserid { get; init; }
    public int? ListCreateListid { get; init; }
    public int? Listid { get; init; }
    public int? MusiclibId { get; init; }
    public int? IpId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Pic { get; init; } = string.Empty;
    public string Intro { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string UserPic { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
    public int PlayCount { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<KugouSong>? Songs { get; init; }
    public bool IsPrivate { get; init; }
    public int? Heat { get; init; }
    public string? PublishDate { get; init; }
    public int? CreateTime { get; init; }
    public int? UpdateTime { get; init; }
    public int Source { get; init; }
    public int? Type { get; init; }
    public bool? IsDefault { get; init; }
    public int? OriginalId { get; init; }
}

public sealed class KugouAlbum
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Pic { get; init; } = string.Empty;
    public string Intro { get; init; } = string.Empty;
    public string SingerName { get; init; } = string.Empty;
    public int SingerId { get; init; }
    public string PublishTime { get; init; } = string.Empty;
    public int SongCount { get; init; }
    public int PlayCount { get; init; }
    public int Heat { get; init; }
    public string Language { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
}

public sealed class KugouArtist
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Pic { get; init; } = string.Empty;
    public string Intro { get; init; } = string.Empty;
    public int SongCount { get; init; }
    public int AlbumCount { get; init; }
    public int MvCount { get; init; }
    public int FansCount { get; init; }
    public int Heat { get; init; }
    public string? Birthday { get; init; }
    public bool? IsFollowed { get; init; }
}

public sealed class KugouRank
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Pic { get; init; } = string.Empty;
    public int? RankType { get; init; }
    public string? RankTypeName { get; init; }
    public string? UpdateFrequency { get; init; }
    public string? Group { get; init; }
    public string? Type { get; init; }
}

public sealed class KugouCommentLike
{
    public int Count { get; init; }
}

public sealed class KugouComment
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? UserPic { get; init; }
    public string Avatar { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public string? AddTime { get; init; }
    public int LikeCount { get; init; }
    public KugouCommentLike Like { get; init; } = new();
    public int? ReplyCount { get; init; }
    public int? ReplyNum { get; init; }
    public bool? IsHot { get; init; }
    public bool? IsStar { get; init; }
    public string? SpecialId { get; init; }
    public string? SpecialChildId { get; init; }
    public string? Tid { get; init; }
    public string? Code { get; init; }
    public string? MixSongId { get; init; }
    public string? AudioId { get; init; }
    public string? AlbumAudioId { get; init; }
}

public sealed class KugouUser
{
    public int UserId { get; init; }
    public string Token { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string? Nickname { get; init; }
    public string? Mobile { get; init; }
    public string? Pic { get; init; }
    public int? Expires { get; init; }
    public string? T1 { get; init; }
    public int? VipType { get; init; }
    public int? PGrade { get; init; }
    public JsonObject? Detail { get; init; }
    public JsonObject? Vip { get; init; }
}

public sealed class KugouVideoAuthor
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Avatar { get; init; }
}

public sealed class KugouVideoTag
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class KugouVideoSource
{
    public string Hash { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Thumb { get; init; }
    public string? Codec { get; init; }
    public int? Bitrate { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public long? Size { get; init; }
}

public sealed class KugouVideo
{
    public string Id { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Remark { get; init; }
    public string? Topic { get; init; }
    public string CoverUrl { get; init; } = string.Empty;
    public int Duration { get; init; }
    public int? PlayCount { get; init; }
    public long? PublishTime { get; init; }
    public string? AlbumAudioId { get; init; }
    public string? SongName { get; init; }
    public string? ArtistName { get; init; }
    public string? AlbumName { get; init; }
    public IReadOnlyList<KugouVideoAuthor> Authors { get; init; } = Array.Empty<KugouVideoAuthor>();
    public IReadOnlyList<KugouVideoTag> Tags { get; init; } = Array.Empty<KugouVideoTag>();
    public IReadOnlyList<KugouVideoSource> Sources { get; init; } = Array.Empty<KugouVideoSource>();
    public int? CollectionCount { get; init; }
    public int? DownloadCount { get; init; }
    public int? HotScore { get; init; }
    public bool? Recommend { get; init; }
}

public sealed class KugouTrackLoudness
{
    public double Lufs { get; init; }
    public double Gain { get; init; }
    public double Peak { get; init; }
}

public sealed class KugouAudioUrl
{
    public string Url { get; init; } = string.Empty;
    public int? Bitrate { get; init; }
    public KugouTrackLoudness? Loudness { get; init; }
    public string? EnEkey { get; init; }
    public long FileSize { get; init; }
}