using KuGou.Lite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyDb.Collections;
using TinyDb.Core;

namespace KuGouMusicAvalonia.Services;

internal sealed class LocalMusicStore
{
    public static string DatabasePath { get; } = Path.Combine(AppStateStore.AppDirectory, "music.db");
    public static LocalMusicStore Instance { get; } = new();

    private const string LegacyImportSettingKey = "migration.sessionJson.v1";
    private const string CurrentPlaybackStateId = "current";
    private const string CurrentQueueId = "current";
    private const string MissingDownloadStatus = "Missing";
    private const string CompletedDownloadStatus = "Completed";
    private const string DownloadingStatus = "Downloading";
    private const string FailedDownloadStatus = "Failed";

    private readonly object _gate = new();
    private readonly TinyDbEngine _database;
    private readonly ITinyCollection<AppSettingRecord> _settings;
    private readonly ITinyCollection<CookieRecord> _cookies;
    private readonly ITinyCollection<LocalSongRecord> _songs;
    private readonly ITinyCollection<FavoriteSongRecord> _favorites;
    private readonly ITinyCollection<DownloadRecord> _downloads;
    private readonly ITinyCollection<PlaybackStateRecord> _playbackStates;
    private readonly ITinyCollection<PlaybackQueueItemRecord> _queueItems;
    private readonly ITinyCollection<PlaybackHistoryRecord> _history;

    private LocalMusicStore()
    {
        Directory.CreateDirectory(AppStateStore.AppDirectory);
        _database = new TinyDbEngine(DatabasePath, new TinyDbOptions { EnableJournaling = true });
        _settings = _database.GetCollection<AppSettingRecord>();
        _cookies = _database.GetCollection<CookieRecord>();
        _songs = _database.GetCollection<LocalSongRecord>();
        _favorites = _database.GetCollection<FavoriteSongRecord>();
        _downloads = _database.GetCollection<DownloadRecord>();
        _playbackStates = _database.GetCollection<PlaybackStateRecord>();
        _queueItems = _database.GetCollection<PlaybackQueueItemRecord>();
        _history = _database.GetCollection<PlaybackHistoryRecord>();
        MigrateLegacyAppStateIfNeeded();
    }

    public MusicAppState LoadAppState()
    {
        return new MusicAppState
        {
            Cookies = LoadCookies(),
            AutoReceiveVipBeforePlayback = GetBoolSetting(LocalSettingKeys.AutoReceiveVipBeforePlayback, true),
            ThemeMode = GetStringSetting(LocalSettingKeys.ThemeMode, "深色"),
            StreamWhileDownloading = GetBoolSetting(LocalSettingKeys.StreamWhileDownloading, true),
            DownloadDirectory = GetStringSetting(LocalSettingKeys.DownloadDirectory, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
            DefaultPlaybackQuality = GetStringSetting(LocalSettingKeys.DefaultPlaybackQuality, "标准 128k"),
            FavoriteSongKeys = LoadFavoriteKeys().ToList()
        };
    }

    public Dictionary<string, string> LoadCookies()
    {
        lock (_gate)
        {
            return _cookies.FindAll()
                .Where(cookie => !string.IsNullOrWhiteSpace(cookie.Name))
                .GroupBy(cookie => cookie.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
        }
    }

    public void SaveCookies(IReadOnlyDictionary<string, string> cookies)
    {
        lock (_gate)
        {
            _cookies.DeleteAll();
            var now = DateTime.UtcNow;
            var records = cookies
                .Where(cookie => !string.IsNullOrWhiteSpace(cookie.Key))
                .Select(cookie => new CookieRecord
                {
                    Name = cookie.Key,
                    Value = cookie.Value,
                    UpdatedAtUtc = now
                })
                .ToList();
            if (records.Count > 0)
            {
                _cookies.Insert(records);
            }
        }
    }

    public void ClearCookies()
    {
        lock (_gate)
        {
            _cookies.DeleteAll();
        }
    }

    public string GetStringSetting(string key, string fallback)
    {
        lock (_gate)
        {
            return _settings.FindById(key)?.Value ?? fallback;
        }
    }

    public bool GetBoolSetting(string key, bool fallback)
    {
        var value = GetStringSetting(key, fallback.ToString());
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public void SetSetting(string key, string value)
    {
        lock (_gate)
        {
            _settings.Upsert(new AppSettingRecord
            {
                Key = key,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
    }

    public void SetSetting(string key, bool value)
    {
        SetSetting(key, value.ToString());
    }

    public IReadOnlyList<string> LoadFavoriteKeys()
    {
        lock (_gate)
        {
            return _favorites.FindAll()
                .Select(favorite => favorite.SongKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
        }
    }

    public bool IsFavorite(string songKey)
    {
        if (string.IsNullOrWhiteSpace(songKey))
        {
            return false;
        }

        lock (_gate)
        {
            return _favorites.FindById(songKey) is not null;
        }
    }

    public void SetFavorite(KugouSong? song, bool isFavorite)
    {
        var songKey = GetSongKey(song);
        if (string.IsNullOrWhiteSpace(songKey))
        {
            return;
        }

        SaveSongSnapshot(song);
        lock (_gate)
        {
            if (isFavorite)
            {
                _favorites.Upsert(new FavoriteSongRecord
                {
                    SongKey = songKey,
                    AddedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                _favorites.Delete(songKey);
            }
        }
    }

    public void ReplaceFavoriteSongs(IEnumerable<KugouSong> songs)
    {
        var now = DateTime.UtcNow;
        var songRecords = songs
            .Select(ToSongRecord)
            .Where(record => !string.IsNullOrWhiteSpace(record.SongKey))
            .GroupBy(record => record.SongKey, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
        var favoriteRecords = songRecords
            .Select(record => new FavoriteSongRecord
            {
                SongKey = record.SongKey,
                AddedAtUtc = now
            })
            .ToList();

        lock (_gate)
        {
            _favorites.DeleteAll();
            foreach (var record in songRecords)
            {
                _songs.Upsert(record);
            }

            if (favoriteRecords.Count > 0)
            {
                _favorites.Insert(favoriteRecords);
            }
        }
    }

    public void SaveSongSnapshot(KugouSong? song)
    {
        if (song is null)
        {
            return;
        }

        var record = ToSongRecord(song);
        if (string.IsNullOrWhiteSpace(record.SongKey))
        {
            return;
        }

        lock (_gate)
        {
            _songs.Upsert(record);
        }
    }

    public KugouSong? LoadSongSnapshot(string? songKey)
    {
        if (string.IsNullOrWhiteSpace(songKey))
        {
            return null;
        }

        lock (_gate)
        {
            return ToSong(_songs.FindById(songKey));
        }
    }

    public string? FindCompletedDownload(KugouSong song)
    {
        var candidates = FindDownloadCandidates(song);
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate.FilePath))
            {
                MarkDownloadVerified(candidate);
                return candidate.FilePath;
            }

            MarkDownloadMissing(candidate);
        }

        return null;
    }

    public void SaveDiscoveredDownload(KugouSong song, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        SaveDownloadRecord(song, filePath, string.Empty, string.Empty, CompletedDownloadStatus, null);
    }

    public void MarkDownloadStarted(KugouSong song, string filePath, string? quality, string sourceUrl)
    {
        SaveDownloadRecord(song, filePath, quality, sourceUrl, DownloadingStatus, null);
    }

    public void MarkDownloadCompleted(KugouSong song, string filePath, string? quality, string sourceUrl)
    {
        SaveDownloadRecord(song, filePath, quality, sourceUrl, CompletedDownloadStatus, null);
    }

    public void MarkDownloadFailed(KugouSong song, string filePath, string? quality, string sourceUrl, Exception exception)
    {
        SaveDownloadRecord(song, filePath, quality, sourceUrl, FailedDownloadStatus, exception.GetBaseException().Message);
    }

    public PlaybackStateRecord? LoadPlaybackState()
    {
        lock (_gate)
        {
            return _playbackStates.FindById(CurrentPlaybackStateId);
        }
    }

    public IReadOnlyList<KugouSong> LoadCurrentQueueSongs()
    {
        lock (_gate)
        {
            return _queueItems.Find(item => item.QueueId == CurrentQueueId)
                .OrderBy(item => item.Position)
                .Select(item => ToSong(_songs.FindById(item.SongKey)))
                .Where(song => song is not null)
                .Cast<KugouSong>()
                .ToList();
        }
    }

    public void SavePlaybackState(
        KugouSong? currentSong,
        IReadOnlyList<KugouSong> queue,
        string queueTitle,
        int currentQueueIndex,
        PlaybackMode playbackMode,
        double volume,
        double progressSeconds,
        double durationSeconds,
        bool isRadioMode,
        bool saveQueue)
    {
        var currentSongKey = GetSongKey(currentSong);
        if (currentSong is not null)
        {
            SaveSongSnapshot(currentSong);
        }

        lock (_gate)
        {
            if (saveQueue)
            {
                PersistQueue(queue);
            }

            _playbackStates.Upsert(new PlaybackStateRecord
            {
                Id = CurrentPlaybackStateId,
                CurrentSongKey = currentSongKey,
                QueueTitle = string.IsNullOrWhiteSpace(queueTitle) ? "临时播放" : queueTitle.Trim(),
                CurrentQueueIndex = currentQueueIndex,
                PlaybackMode = playbackMode.ToString(),
                Volume = Math.Clamp(volume, 0, 100),
                ProgressSeconds = Math.Max(0, progressSeconds),
                DurationSeconds = Math.Max(0, durationSeconds),
                IsRadioMode = isRadioMode,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
    }

    public string RecordPlaybackStarted(KugouSong song, string source, double durationSeconds)
    {
        var songKey = GetSongKey(song);
        if (string.IsNullOrWhiteSpace(songKey))
        {
            return string.Empty;
        }

        SaveSongSnapshot(song);
        var record = new PlaybackHistoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            SongKey = songKey,
            Source = source,
            PlayedAtUtc = DateTime.UtcNow,
            ProgressSeconds = 0,
            DurationSeconds = Math.Max(0, durationSeconds),
            Completed = false,
            UpdatedAtUtc = DateTime.UtcNow
        };

        lock (_gate)
        {
            _history.Insert(record);
        }

        return record.Id;
    }

    public void UpdatePlaybackHistory(string historyId, double progressSeconds, double durationSeconds, bool completed)
    {
        if (string.IsNullOrWhiteSpace(historyId))
        {
            return;
        }

        lock (_gate)
        {
            var record = _history.FindById(historyId);
            if (record is null)
            {
                return;
            }

            record.ProgressSeconds = Math.Max(record.ProgressSeconds, progressSeconds);
            record.DurationSeconds = Math.Max(record.DurationSeconds, durationSeconds);
            record.Completed = record.Completed || completed;
            record.UpdatedAtUtc = DateTime.UtcNow;
            record.UpdatedAtUtc = DateTime.UtcNow;
            _history.Update(record);
        }
    }

    public IReadOnlyList<KugouSong> LoadLocalHistory(int count = 100)
    {
        lock (_gate)
        {
            return _history.FindAll()
                .OrderByDescending(record => record.PlayedAtUtc)
                .Select(record => ToSong(_songs.FindById(record.SongKey)))
                .Where(song => song is not null)
                .Cast<KugouSong>()
                .DistinctBy(song => GetSongKey(song))
                .Take(count)
                .ToList();
        }
    }

    public static string GetSongKey(KugouSong? song)
    {
        if (song is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(song.Hash))
        {
            return "hash:" + song.Hash.Trim().ToUpperInvariant();
        }

        if (song.MixSongId > 0)
        {
            return "mix:" + song.MixSongId;
        }

        if (!string.IsNullOrWhiteSpace(song.SongId))
        {
            return "song:" + song.SongId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(song.Id))
        {
            return "id:" + song.Id.Trim();
        }

        if (!string.IsNullOrWhiteSpace(song.Title) || !string.IsNullOrWhiteSpace(song.Artist))
        {
            return $"text:{song.Title.Trim()}|{song.Artist.Trim()}";
        }

        return string.Empty;
    }

    public static IReadOnlyList<string> GetKnownHashes(KugouSong song)
    {
        return EnumerateKnownHashes(song).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void MigrateLegacyAppStateIfNeeded()
    {
        if (GetBoolSetting(LegacyImportSettingKey, false))
        {
            return;
        }

        var legacyState = AppStateStore.Load();
        SetSetting(LocalSettingKeys.AutoReceiveVipBeforePlayback, legacyState.AutoReceiveVipBeforePlayback);
        SetSetting(LocalSettingKeys.ThemeMode, legacyState.ThemeMode);
        SetSetting(LocalSettingKeys.StreamWhileDownloading, legacyState.StreamWhileDownloading);
        SetSetting(LocalSettingKeys.DownloadDirectory, legacyState.DownloadDirectory);
        SetSetting(LocalSettingKeys.DefaultPlaybackQuality, legacyState.DefaultPlaybackQuality);
        SaveCookies(legacyState.Cookies);

        lock (_gate)
        {
            var now = DateTime.UtcNow;
            foreach (var key in legacyState.FavoriteSongKeys.Where(key => !string.IsNullOrWhiteSpace(key)))
            {
                _favorites.Upsert(new FavoriteSongRecord
                {
                    SongKey = key,
                    AddedAtUtc = now
                });
            }
        }

        SetSetting(LegacyImportSettingKey, true);
    }

    private IReadOnlyList<DownloadRecord> FindDownloadCandidates(KugouSong song)
    {
        var songKey = GetSongKey(song);
        var hashes = GetKnownHashes(song);
        var candidates = new List<DownloadRecord>();

        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(songKey))
            {
                candidates.AddRange(_downloads.Find(record => record.SongKey == songKey && record.Status == CompletedDownloadStatus));
            }

            foreach (var hash in hashes.Where(hash => !string.IsNullOrWhiteSpace(hash)))
            {
                candidates.AddRange(_downloads.Find(record => record.Hash == hash && record.Status == CompletedDownloadStatus));
            }
        }

        return candidates
            .Where(record => !string.IsNullOrWhiteSpace(record.FilePath))
            .GroupBy(record => record.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(record => record.LastVerifiedAtUtc ?? record.CompletedAtUtc ?? record.UpdatedAtUtc).First())
            .ToList();
    }

    private void MarkDownloadVerified(DownloadRecord record)
    {
        lock (_gate)
        {
            record.LastVerifiedAtUtc = DateTime.UtcNow;
            record.UpdatedAtUtc = DateTime.UtcNow;
            _downloads.Update(record);
        }
    }

    private void MarkDownloadMissing(DownloadRecord record)
    {
        lock (_gate)
        {
            record.Status = MissingDownloadStatus;
            record.LastVerifiedAtUtc = DateTime.UtcNow;
            record.UpdatedAtUtc = DateTime.UtcNow;
            _downloads.Update(record);
        }
    }

    private void SaveDownloadRecord(KugouSong song, string filePath, string? quality, string sourceUrl, string status, string? errorMessage)
    {
        var songKey = GetSongKey(song);
        if (string.IsNullOrWhiteSpace(songKey) || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        SaveSongSnapshot(song);
        var normalizedQuality = string.IsNullOrWhiteSpace(quality) ? "default" : quality.Trim().ToLowerInvariant();
        var id = BuildDownloadId(songKey, normalizedQuality);
        var now = DateTime.UtcNow;
        var fileInfo = TryGetFileInfo(filePath);

        lock (_gate)
        {
            var existing = _downloads.FindById(id);
            _downloads.Upsert(new DownloadRecord
            {
                Id = id,
                SongKey = songKey,
                Hash = FirstNonEmpty(song.Hash, GetKnownHashes(song).FirstOrDefault()),
                Quality = normalizedQuality,
                FilePath = filePath,
                FileSize = fileInfo?.Length ?? existing?.FileSize ?? 0,
                Status = status,
                SourceUrl = sourceUrl,
                ErrorMessage = errorMessage ?? string.Empty,
                CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                CompletedAtUtc = status == CompletedDownloadStatus ? now : existing?.CompletedAtUtc,
                LastVerifiedAtUtc = status == CompletedDownloadStatus ? now : existing?.LastVerifiedAtUtc,
                UpdatedAtUtc = now
            });
        }
    }

    private void PersistQueue(IReadOnlyList<KugouSong> queue)
    {
        _queueItems.DeleteMany(item => item.QueueId == CurrentQueueId);
        var now = DateTime.UtcNow;
        var records = new List<PlaybackQueueItemRecord>();
        for (var index = 0; index < queue.Count; index++)
        {
            var song = queue[index];
            var songKey = GetSongKey(song);
            if (string.IsNullOrWhiteSpace(songKey))
            {
                continue;
            }

            _songs.Upsert(ToSongRecord(song));
            records.Add(new PlaybackQueueItemRecord
            {
                Id = $"{CurrentQueueId}:{index:D6}:{songKey}",
                QueueId = CurrentQueueId,
                Position = index,
                SongKey = songKey,
                AddedAtUtc = now
            });
        }

        if (records.Count > 0)
        {
            _queueItems.Insert(records);
        }
    }

    private static LocalSongRecord ToSongRecord(KugouSong song)
    {
        return new LocalSongRecord
        {
            SongKey = GetSongKey(song),
            Id = song.Id,
            SongId = song.SongId ?? string.Empty,
            Title = FirstNonEmpty(song.Title, song.Name, "未知歌曲"),
            Name = song.Name ?? string.Empty,
            Artist = song.Artist,
            AlbumName = song.AlbumName ?? string.Empty,
            Album = song.Album ?? string.Empty,
            AlbumId = song.AlbumId ?? string.Empty,
            Duration = song.Duration,
            CoverUrl = song.CoverUrl,
            Cover = song.Cover ?? string.Empty,
            AudioUrl = song.AudioUrl,
            Hash = song.Hash,
            MvHash = song.MvHash ?? string.Empty,
            MixSongId = song.MixSongId,
            Source = song.Source ?? string.Empty,
            Lyric = song.Lyric ?? string.Empty,
            LyricSnippet = song.LyricSnippet ?? string.Empty,
            RelateHashes = GetKnownHashes(song).ToList(),
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static KugouSong? ToSong(LocalSongRecord? record)
    {
        if (record is null)
        {
            return null;
        }

        return new KugouSong
        {
            Id = record.Id,
            SongId = EmptyToNull(record.SongId),
            Title = record.Title,
            Name = EmptyToNull(record.Name),
            Artist = record.Artist,
            AlbumName = EmptyToNull(record.AlbumName),
            Album = EmptyToNull(record.Album),
            AlbumId = EmptyToNull(record.AlbumId),
            Duration = record.Duration,
            CoverUrl = record.CoverUrl,
            Cover = EmptyToNull(record.Cover),
            AudioUrl = record.AudioUrl,
            Hash = record.Hash,
            MvHash = EmptyToNull(record.MvHash),
            MixSongId = record.MixSongId,
            Source = EmptyToNull(record.Source),
            Lyric = EmptyToNull(record.Lyric),
            LyricSnippet = EmptyToNull(record.LyricSnippet),
            RelateGoods = record.RelateHashes
                .Where(hash => !string.IsNullOrWhiteSpace(hash) && !string.Equals(hash, record.Hash, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(hash => new KugouSongRelateGood { Hash = hash })
                .ToArray()
        };
    }

    private static IEnumerable<string> EnumerateKnownHashes(KugouSong song)
    {
        if (!string.IsNullOrWhiteSpace(song.Hash))
        {
            yield return song.Hash.Trim();
        }

        foreach (var hash in song.RelateGoods.Select(item => item.Hash).Where(hash => !string.IsNullOrWhiteSpace(hash)))
        {
            yield return hash!.Trim();
        }
    }

    private static string BuildDownloadId(string songKey, string quality)
    {
        return $"{songKey}|{quality}";
    }

    private static FileInfo? TryGetFileInfo(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? new FileInfo(filePath) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}

internal static class LocalSettingKeys
{
    public const string AutoReceiveVipBeforePlayback = "settings.autoReceiveVipBeforePlayback";
    public const string ThemeMode = "settings.themeMode";
    public const string StreamWhileDownloading = "settings.streamWhileDownloading";
    public const string DownloadDirectory = "settings.downloadDirectory";
    public const string DefaultPlaybackQuality = "settings.defaultPlaybackQuality";
}