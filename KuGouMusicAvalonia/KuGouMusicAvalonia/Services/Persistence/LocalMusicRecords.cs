using System;
using System.Collections.Generic;
using TinyDb.Attributes;

namespace KuGouMusicAvalonia.Services;

[Entity("app_settings")]
public sealed partial class AppSettingRecord
{
    [Id]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("auth_cookies")]
public sealed partial class CookieRecord
{
    [Id]
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("user_profile_cache")]
public sealed partial class UserProfileCacheRecord
{
    [Id]
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string UserIdText { get; set; } = string.Empty;
    public string PlaylistCountText { get; set; } = string.Empty;
    public string CollectionCountText { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("user_library_cache_items")]
public sealed partial class UserLibraryCacheItemRecord
{
    [Id]
    public string Id { get; set; } = string.Empty;
    [Index]
    public string Category { get; set; } = string.Empty;
    [Index]
    public int Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("songs")]
public sealed partial class LocalSongRecord
{
    [Id]
    public string SongKey { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string SongId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string AlbumId { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string CoverUrl { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    [Index]
    public string Hash { get; set; } = string.Empty;
    public string MvHash { get; set; } = string.Empty;
    [Index]
    public long MixSongId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Lyric { get; set; } = string.Empty;
    public string LyricSnippet { get; set; } = string.Empty;
    public List<string> RelateHashes { get; set; } = new();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("favorites")]
public sealed partial class FavoriteSongRecord
{
    [Id]
    public string SongKey { get; set; } = string.Empty;
    [Index]
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("downloads")]
public sealed partial class DownloadRecord
{
    [Id]
    public string Id { get; set; } = string.Empty;
    [Index]
    public string SongKey { get; set; } = string.Empty;
    [Index]
    public string Hash { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    [Index]
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [Index]
    public string Status { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LastVerifiedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("playback_state")]
public sealed partial class PlaybackStateRecord
{
    [Id]
    public string Id { get; set; } = string.Empty;
    public string CurrentSongKey { get; set; } = string.Empty;
    public string QueueTitle { get; set; } = string.Empty;
    public int CurrentQueueIndex { get; set; } = -1;
    public string PlaybackMode { get; set; } = string.Empty;
    public double Volume { get; set; }
    public double ProgressSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public bool IsRadioMode { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("playback_queue_items")]
public sealed partial class PlaybackQueueItemRecord
{
    [Id]
    public string Id { get; set; } = string.Empty;
    [Index]
    public string QueueId { get; set; } = string.Empty;
    [Index]
    public int Position { get; set; }
    public string SongKey { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("playback_history")]
public sealed partial class PlaybackHistoryRecord
{
    [Id]
    public string Id { get; set; } = string.Empty;
    [Index]
    public string SongKey { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    [Index]
    public DateTime PlayedAtUtc { get; set; } = DateTime.UtcNow;
    public double ProgressSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public bool Completed { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

[Entity("lyrics")]
public sealed partial class LyricRecord
{
    [Id]
    public string Id { get; set; } = string.Empty;
    [Index]
    public string Hash { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;
}
