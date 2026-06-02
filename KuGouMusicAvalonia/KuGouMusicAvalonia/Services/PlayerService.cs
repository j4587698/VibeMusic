using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KuGou.Lite;
using SimpleAudioPlayer;
using SimpleAudioPlayer.Handles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services;

public enum PlaybackMode
{
    Sequential,
    ListLoop,
    Shuffle,
    SingleLoop
}

public sealed partial class PlayerService : ObservableObject, IDisposable
{
    public static PlayerService Instance { get; } = new();

    private readonly DispatcherTimer _progressTimer;
    private readonly object _playerLock = new();
    private AudioPlayer? _audioPlayer;
    private CancellationTokenSource? _loadCts;
    private int _playRequestId;
    private bool _isUpdatingProgress;
    private bool _disposed;
    private readonly Random _random = new();
    private Func<KugouSong?, Task<KugouSong?>>? _radioNextSongProvider;
    private DateTime _lastPlaybackStateSaveUtc = DateTime.MinValue;
    private bool _isRestoringPlaybackState;
    private string _pendingResumeSongKey = string.Empty;
    private double _pendingResumeProgress;
    private string _currentHistoryId = string.Empty;

    private static string ErrorLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KuGouMusicAvalonia",
        "playback-errors.log");

    [NotifyPropertyChangedFor(nameof(CurrentTitle))]
    [NotifyPropertyChangedFor(nameof(CurrentArtist))]
    [NotifyPropertyChangedFor(nameof(CurrentCoverUrl))]
    [NotifyPropertyChangedFor(nameof(CurrentAlbumText))]
    [NotifyPropertyChangedFor(nameof(HasSong))]
    [NotifyPropertyChangedFor(nameof(IsCurrentSongFavorite))]
    [NotifyPropertyChangedFor(nameof(FavoriteStatusText))]
    [ObservableProperty]
    private KugouSong? _currentSong;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseIcon))]
    [NotifyPropertyChangedFor(nameof(PlaybackStateText))]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackStateText))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadProgressText))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadProgressText))]
    private long _downloadReceivedBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadProgressText))]
    private long _downloadTotalBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(BufferedProgress))]
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    [NotifyPropertyChangedFor(nameof(BufferedProgress))]
    private double _duration = 240;

    [ObservableProperty]
    private double _volume = 70;

    [ObservableProperty]
    private string _statusMessage = "播放器就绪";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlaybackError))]
    private string _lastErrorDetail = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayModeText))]
    [NotifyPropertyChangedFor(nameof(RepeatModeText))]
    [NotifyPropertyChangedFor(nameof(IsSequentialMode))]
    [NotifyPropertyChangedFor(nameof(IsListLoopMode))]
    [NotifyPropertyChangedFor(nameof(IsShuffleActive))]
    [NotifyPropertyChangedFor(nameof(IsRepeatActive))]
    [NotifyPropertyChangedFor(nameof(IsSingleLoopMode))]
    private PlaybackMode _playMode = PlaybackMode.Sequential;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QueueStatusText))]
    private string _queueTitle = "临时播放";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQueue))]
    [NotifyPropertyChangedFor(nameof(IsQueueEmpty))]
    [NotifyPropertyChangedFor(nameof(CurrentQueuePositionText))]
    [NotifyPropertyChangedFor(nameof(QueueStatusText))]
    private bool _isRadioMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentQueuePositionText))]
    [NotifyPropertyChangedFor(nameof(QueueStatusText))]
    private int _currentQueueIndex = -1;

    public ObservableCollection<KugouSong> Queue { get; } = new();

    public string PlayPauseIcon => IsPlaying ? "pause" : "play";
    public string PlaybackStateText => IsLoading ? "正在加载" : IsPlaying ? "正在播放" : "已暂停";
    public string CurrentTitle => CurrentSong?.Title ?? "未选择歌曲";
    public string CurrentArtist => CurrentSong?.Artist ?? "选择一首酷狗音乐开始";
    public string CurrentCoverUrl => CurrentSong?.CoverUrl ?? string.Empty;
    public string CurrentAlbumText => string.IsNullOrWhiteSpace(CurrentSong?.AlbumName) ? "酷狗音乐" : CurrentSong!.AlbumName!;
    public bool HasSong => CurrentSong is not null;
    public bool HasPlaybackError => !string.IsNullOrWhiteSpace(LastErrorDetail);
    public string DownloadProgressText =>
        !IsDownloading
            ? string.Empty
            : DownloadTotalBytes > 0
                ? $"下载中 {Math.Clamp((double)DownloadReceivedBytes / DownloadTotalBytes * 100, 0, 100):0}% ({FormatBytes(DownloadReceivedBytes)} / {FormatBytes(DownloadTotalBytes)})"
                : $"下载中 {FormatBytes(DownloadReceivedBytes)}";
    public bool IsCurrentSongFavorite => FavoriteSongService.Instance.IsFavorite(CurrentSong);
    public string FavoriteStatusText => IsCurrentSongFavorite ? "已喜欢" : "喜欢";
    public double BufferedProgress => Math.Clamp(Progress, 0, Math.Max(Duration, 0));
    public string ProgressText => FormatDuration(Progress);
    public string DurationText => FormatDuration(Duration);
    public string PlayModeText => PlayMode switch
    {
        PlaybackMode.ListLoop => "列表循环",
        PlaybackMode.Shuffle => "随机播放",
        PlaybackMode.SingleLoop => "单曲循环",
        _ => "顺序播放"
    };
    public string RepeatModeText => PlayMode switch
    {
        PlaybackMode.ListLoop => "列表循环",
        PlaybackMode.SingleLoop => "单曲循环",
        _ => "顺序播放"
    };
    public bool IsSequentialMode => PlayMode == PlaybackMode.Sequential;
    public bool IsListLoopMode => PlayMode == PlaybackMode.ListLoop;
    public bool IsShuffleActive => PlayMode == PlaybackMode.Shuffle;
    public bool IsRepeatActive => PlayMode is PlaybackMode.ListLoop or PlaybackMode.SingleLoop;
    public bool IsSingleLoopMode => PlayMode == PlaybackMode.SingleLoop;
    public bool HasQueue => !IsRadioMode && Queue.Count > 0;
    public bool IsQueueEmpty => IsRadioMode || Queue.Count == 0;
    public string CurrentQueuePositionText => IsRadioMode ? "FM" : Queue.Count == 0 || CurrentQueueIndex < 0 ? "0 / 0" : $"{CurrentQueueIndex + 1} / {Queue.Count}";
    public string QueueStatusText => IsRadioMode ? $"{QueueTitle} · 电台播放" : Queue.Count == 0 ? "暂无播放队列" : $"{QueueTitle} · {CurrentQueuePositionText}";

    private PlayerService()
    {
        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _progressTimer.Tick += OnProgressTick;
        RestorePersistedPlaybackState();
    }

    public Task PlaySongAsync(KugouSong song)
    {
        return PlayQueueAsync(new[] { song }, 0, "临时播放", replaceQueue: true);
    }

    public async Task PlayQueueAsync(IEnumerable<KugouSong> songs, int startIndex = 0, string? title = null, bool replaceQueue = true)
    {
        var songList = songs.Where(song => song is not null).ToList();
        if (songList.Count == 0)
        {
            StatusMessage = "列表里没有可播放歌曲";
            return;
        }

        ExitRadioMode();
        if (replaceQueue)
        {
            ReplaceQueue(songList, title);
            CurrentQueueIndex = Math.Clamp(startIndex, 0, Queue.Count - 1);
            PersistPlaybackState(force: true, saveQueue: true);
        }
        else
        {
            var appended = AppendToQueue(songList, title);
            if (appended == 0)
            {
                StatusMessage = "歌曲已在播放队列中";
                return;
            }

            if (!HasSong)
            {
                CurrentQueueIndex = Math.Clamp(startIndex, 0, Queue.Count - 1);
            }
            else
            {
                StatusMessage = $"已加入 {appended} 首到播放队列";
                return;
            }
        }

        await PlayQueueIndexAsync(CurrentQueueIndex).ConfigureAwait(false);
    }

    public async Task StartRadioAsync(KugouSong firstSong, Func<KugouSong?, Task<KugouSong?>> nextSongProvider, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(firstSong);
        ArgumentNullException.ThrowIfNull(nextSongProvider);

        _radioNextSongProvider = nextSongProvider;
        Queue.Clear();
        QueueTitle = string.IsNullOrWhiteSpace(title) ? "FM 电台" : title.Trim();
        CurrentQueueIndex = -1;
        IsRadioMode = true;
        NotifyQueueChanged();
        PersistPlaybackState(force: true, saveQueue: true);
        await PlayRadioSongAsync(firstSong).ConfigureAwait(false);
    }

    public int AppendToQueue(IEnumerable<KugouSong> songs, string? title = null)
    {
        var songList = songs.Where(song => song is not null).ToList();
        if (songList.Count == 0)
        {
            return 0;
        }

        if (IsRadioMode)
        {
            ExitRadioMode();
        }

        var added = 0;
        foreach (var song in songList)
        {
            if (Queue.Any(item => IsSameSong(item, song)))
            {
                continue;
            }

            Queue.Add(song);
            added++;
        }

        if (!string.IsNullOrWhiteSpace(title) && Queue.Count > 0 && QueueTitle == "临时播放")
        {
            QueueTitle = title.Trim();
        }

        NotifyQueueChanged();
        if (added > 0)
        {
            StatusMessage = $"已加入 {added} 首到播放队列";
            PersistPlaybackState(force: true, saveQueue: true);
        }

        return added;
    }

    public Task PlayQueueSongAsync(KugouSong song)
    {
        if (song is null)
        {
            return Task.CompletedTask;
        }

        var index = Queue.ToList().FindIndex(item => IsSameSong(item, song));
        if (index < 0)
        {
            StatusMessage = "这首歌不在当前播放队列中";
            return Task.CompletedTask;
        }

        return PlayQueueIndexAsync(index);
    }

    public async Task RemoveFromQueueAsync(KugouSong song)
    {
        if (song is null || Queue.Count == 0)
        {
            return;
        }

        var index = Queue.ToList().FindIndex(item => IsSameSong(item, song));
        if (index < 0)
        {
            StatusMessage = "这首歌不在当前播放队列中";
            return;
        }

        var currentIndex = ResolveCurrentQueueIndex();
        var isRemovingCurrent = index == currentIndex;
        Queue.RemoveAt(index);

        if (Queue.Count == 0)
        {
            ClearQueue();
            return;
        }

        if (isRemovingCurrent)
        {
            var nextIndex = Math.Min(index, Queue.Count - 1);
            CurrentQueueIndex = nextIndex;
            NotifyQueueChanged();
            await PlayQueueIndexAsync(nextIndex).ConfigureAwait(false);
            return;
        }

        if (index < CurrentQueueIndex)
        {
            CurrentQueueIndex--;
        }

        NotifyQueueChanged();
        PersistPlaybackState(force: true, saveQueue: true);
        StatusMessage = "已从播放队列移除";
    }

    public void ClearQueue()
    {
        ExitRadioMode();
        FinishCurrentPlaybackHistory(completed: false);
        ResetActiveLoad();
        DisposeCurrentPlayer();
        Queue.Clear();
        QueueTitle = "临时播放";
        CurrentQueueIndex = -1;
        CurrentSong = null;
        IsPlaying = false;
        Duration = 0;
        SetProgressFromPlayer(0);
        LastErrorDetail = string.Empty;
        StopProgressTimer();
        NotifyQueueChanged();
        PersistPlaybackState(force: true, saveQueue: true);
        StatusMessage = "播放队列已清空";
    }

    public void ToggleShuffle()
    {
        PlayMode = PlayMode == PlaybackMode.Shuffle ? PlaybackMode.Sequential : PlaybackMode.Shuffle;
        StatusMessage = PlayModeText;
        PersistPlaybackState(force: true);
    }

    public void CyclePlayMode()
    {
        PlayMode = PlayMode switch
        {
            PlaybackMode.Sequential => PlaybackMode.ListLoop,
            PlaybackMode.ListLoop => PlaybackMode.SingleLoop,
            PlaybackMode.SingleLoop => PlaybackMode.Shuffle,
            PlaybackMode.Shuffle => PlaybackMode.Sequential,
            _ => PlaybackMode.Sequential
        };
        StatusMessage = PlayModeText;
        PersistPlaybackState(force: true);
    }

    public void CycleRepeatMode()
    {
        CyclePlayMode();
    }

    public async Task SkipNextAsync()
    {
        if (IsRadioMode)
        {
            await PlayNextRadioSongAsync(userInitiated: true).ConfigureAwait(false);
            return;
        }

        if (Queue.Count == 0)
        {
            StatusMessage = "播放队列为空";
            return;
        }

        var nextIndex = ResolveNextQueueIndex(forceAdvance: true);
        if (nextIndex < 0)
        {
            StatusMessage = "已经是最后一首";
            return;
        }

        await PlayQueueIndexAsync(nextIndex).ConfigureAwait(false);
    }

    public async Task SkipPreviousAsync()
    {
        if (IsRadioMode)
        {
            StatusMessage = "FM 电台只有下一首";
            return;
        }

        if (Queue.Count == 0)
        {
            StatusMessage = "播放队列为空";
            return;
        }

        var previousIndex = ResolvePreviousQueueIndex();
        if (previousIndex < 0)
        {
            StatusMessage = "已经是第一首";
            return;
        }

        await PlayQueueIndexAsync(previousIndex).ConfigureAwait(false);
    }

    private async Task PlayQueueIndexAsync(int index)
    {
        if (index < 0 || index >= Queue.Count)
        {
            StatusMessage = "播放队列索引无效";
            return;
        }

        CurrentQueueIndex = index;
        PersistPlaybackState(force: true);
        await PlayResolvedSongAsync(Queue[index]).ConfigureAwait(false);
    }

    private Task PlayRadioSongAsync(KugouSong song)
    {
        CurrentQueueIndex = -1;
        NotifyQueueChanged();
        PersistPlaybackState(force: true, saveQueue: true);
        return PlayResolvedSongAsync(song);
    }

    private async Task PlayNextRadioSongAsync(bool userInitiated)
    {
        var nextSongProvider = _radioNextSongProvider;
        if (nextSongProvider is null)
        {
            ExitRadioMode();
            StatusMessage = "FM 电台未就绪";
            return;
        }

        if (IsLoading)
        {
            StatusMessage = "FM 正在加载下一首";
            return;
        }

        if (userInitiated)
        {
            ResetActiveLoad();
            DisposeCurrentPlayer();
        }

        IsPlaying = false;
        StopProgressTimer();
        if (!userInitiated)
        {
            SetProgressFromPlayer(Duration);
        }

        IsLoading = true;
        StatusMessage = "正在加载下一首 FM";
        LastErrorDetail = string.Empty;

        try
        {
            var nextSong = await nextSongProvider(CurrentSong).ConfigureAwait(false);
            if (nextSong is null)
            {
                IsLoading = false;
                StatusMessage = "FM 暂时没有下一首";
                return;
            }

            if (!IsRadioMode)
            {
                IsLoading = false;
                return;
            }

            await PlayRadioSongAsync(nextSong).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            IsLoading = false;
            IsPlaying = false;
            StopProgressTimer();
            var message = FirstLine(ex.GetBaseException().Message);
            StatusMessage = $"FM 加载失败：{message}";
            LastErrorDetail = ex.ToString();
        }
    }

    private async Task PlayResolvedSongAsync(KugouSong song)
    {
        if (_disposed)
        {
            return;
        }

        var requestId = Interlocked.Increment(ref _playRequestId);
        FinishCurrentPlaybackHistory(completed: false);
        ResetActiveLoad();
        DisposeCurrentPlayer();

        CurrentSong = song;
        LastErrorDetail = string.Empty;
        SetProgressFromPlayer(0);
        Duration = NormalizeDuration(song.Duration);
        IsPlaying = false;
        IsLoading = true;
        StatusMessage = "正在解析音源";
        StopProgressTimer();
        await Task.Yield();

        var loadCts = new CancellationTokenSource();
        var cancellationToken = loadCts.Token;
        _loadCts = loadCts;

        AudioPlayer? player = null;
        PlaybackSource? playbackSource = null;
        var localFallbackAttempted = false;
        try
        {
            var cachedFile = AudioCacheService.Instance.FindCachedFile(song);
            if (!string.IsNullOrWhiteSpace(cachedFile))
            {
                playbackSource = new PlaybackSource(cachedFile, IsLocalFile: true, Quality: null);
                StatusMessage = "正在打开已下载歌曲";
            }
            else
            {
                if (VipPrivilegeService.Instance.AutoReceiveBeforePlayback)
                {
                    StatusMessage = "正在检查畅听/VIP权益";
                    var vipStatus = await VipPrivilegeService.Instance.EnsureBeforePlaybackAsync(cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(vipStatus.Message))
                    {
                        StatusMessage = vipStatus.Message;
                    }
                }
                StatusMessage = "正在解析音源";
                playbackSource = await ResolvePlaybackSourceAsync(song, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (requestId != _playRequestId)
            {
                return;
            }

            while (true)
            {
                try
                {
                    StatusMessage = playbackSource.IsLocalFile ? "正在打开已下载歌曲" : "正在打开音频流";
                    player = new AudioPlayer();
                    player.Volume = ToPlayerVolume(Volume);
                    player.PlayCompleted = () => Dispatcher.UIThread.Post(OnPlaybackCompleted);

                    if (playbackSource.IsLocalFile)
                    {
                        var handle = await Task.Run(() => new CachedStreamHandle(File.OpenRead(playbackSource.Location)), cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (requestId != _playRequestId)
                        {
                            handle.Dispose();
                            return;
                        }

                        try
                        {
                            await Task.Run(() => player.Load(handle), cancellationToken).ConfigureAwait(false);
                        }
                        catch
                        {
                            handle.Dispose();
                            throw;
                        }
                    }
                    else
                    {
                        var handle = await HttpStreamHandle.CreateAsync(playbackSource.Location);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (requestId != _playRequestId)
                        {
                            handle.Dispose();
                            return;
                        }

                        try
                        {
                            await Task.Run(() => player.Load(handle), cancellationToken).ConfigureAwait(false);
                        }
                        catch
                        {
                            handle.Dispose();
                            throw;
                        }
                    }

                    var realDuration = player.GetDuration();
                    if (realDuration > 0)
                    {
                        Duration = realDuration;
                    }

                    var resumeProgress = TakePendingResumeProgress(song);
                    if (resumeProgress > 3 && resumeProgress < Math.Max(Duration - 3, 0))
                    {
                        player.Seek(resumeProgress);
                        SetProgressFromPlayer(resumeProgress);
                    }

                    var playStarted = await Task.Run(() => player.Play(), cancellationToken).ConfigureAwait(false);
                    if (!playStarted)
                    {
                        throw new InvalidOperationException("SimpleAudioPlayer 启动播放失败");
                    }

                    break;
                }
                catch (Exception ex) when (playbackSource.IsLocalFile && !localFallbackAttempted)
                {
                    player?.Dispose();
                    player = null;
                    localFallbackAttempted = true;
                    HandleLocalPlaybackFailure(song, playbackSource.Location, ex);
                    StatusMessage = "本地缓存异常，改用网络音源";
                    playbackSource = await ResolvePlaybackSourceAsync(song, cancellationToken).ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (requestId != _playRequestId)
            {
                player?.Dispose();
                return;
            }

            lock (_playerLock)
            {
                _audioPlayer = player;
                player = null;
            }

            IsPlaying = true;
            IsLoading = false;
            StatusMessage = playbackSource.IsLocalFile ? "正在播放已下载歌曲" : "正在播放";
            StartProgressTimer();
            StartPlaybackHistory(song, playbackSource);
            PersistPlaybackState(force: true);
            if (!playbackSource.IsLocalFile)
            {
                _ = AudioCacheService.Instance.CacheRemoteSourceAsync(song, playbackSource.Location, playbackSource.Quality);
            }
        }
        catch (OperationCanceledException)
        {
            player?.Dispose();
        }
        catch (Exception ex)
        {
            player?.Dispose();
            IsPlaying = false;
            IsLoading = false;
            StopProgressTimer();
            RecordPlaybackError(song, ex, playbackSource?.Location);
        }
        finally
        {
            if (ReferenceEquals(_loadCts, loadCts))
            {
                _loadCts = null;
                IsLoading = false;
            }

            loadCts.Dispose();
        }
    }

    public async Task DownloadAsync(KugouSong? song)
    {
        song ??= CurrentSong;
        if (song is null)
        {
            StatusMessage = "先选择一首要下载的歌曲";
            return;
        }

        if (IsDownloading)
        {
            StatusMessage = "已有下载任务进行中，请稍候";
            return;
        }

        var title = string.IsNullOrWhiteSpace(song.Title) ? "歌曲" : song.Title;
        var existing = AudioCacheService.Instance.FindCachedFile(song);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            StatusMessage = $"《{title}》已在本地，无需重复下载";
            return;
        }

        IsDownloading = true;
        DownloadReceivedBytes = 0;
        DownloadTotalBytes = 0;
        StatusMessage = $"正在下载《{title}》…";
        try
        {
            var source = await ResolvePlaybackSourceAsync(song, CancellationToken.None).ConfigureAwait(false);
            if (source.IsLocalFile)
            {
                StatusMessage = $"《{title}》已在本地，无需重复下载";
                return;
            }

            var path = await AudioCacheService.Instance
                .DownloadSourceAsync(
                    song,
                    source.Location,
                    source.Quality,
                    cancellationToken: CancellationToken.None,
                    progress: new Progress<DownloadProgressInfo>(progress =>
                    {
                        DownloadReceivedBytes = Math.Max(progress.ReceivedBytes, 0);
                        DownloadTotalBytes = progress.TotalBytes.HasValue ? Math.Max(progress.TotalBytes.Value, 0) : 0;
                    }))
                .ConfigureAwait(false);
            StatusMessage = $"已下载《{title}》到 {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"《{title}》下载失败：{FirstLine(ex.GetBaseException().Message)}";
            LastErrorDetail = ex.ToString();
        }
        finally
        {
            IsDownloading = false;
        }
    }

    partial void OnIsDownloadingChanged(bool value)
    {
        if (!value)
        {
            DownloadReceivedBytes = 0;
            DownloadTotalBytes = 0;
        }

        OnPropertyChanged(nameof(DownloadProgressText));
    }

    public void TogglePlayPause()
    {
        if (IsLoading)
        {
            StatusMessage = "正在加载，请稍候";
            return;
        }        if (!HasSong)
        {
            StatusMessage = "先选择一首歌曲";
            LastErrorDetail = string.Empty;
            return;
        }

        var player = _audioPlayer;
        if (player is null)
        {
            if (IsRadioMode)
            {
                _ = PlayResolvedSongAsync(CurrentSong!);
                return;
            }

            if (CurrentQueueIndex >= 0 && CurrentQueueIndex < Queue.Count)
            {
                _ = PlayQueueIndexAsync(CurrentQueueIndex);
            }
            else
            {
                _ = PlaySongAsync(CurrentSong!);
            }
            return;
        }

        if (IsPlaying)
        {
            if (player.Pause())
            {
                IsPlaying = false;
                StatusMessage = "已暂停";
                LastErrorDetail = string.Empty;
                StopProgressTimer();
                PersistPlaybackState(force: true);
            }
            return;
        }

        if (player.Play())
        {
            IsPlaying = true;
            StatusMessage = "正在播放";
            LastErrorDetail = string.Empty;
            StartProgressTimer();
            PersistPlaybackState(force: true);
        }
    }

    public async Task ToggleCurrentFavoriteAsync()
    {
        if (!HasSong)
        {
            StatusMessage = "先选择一首歌曲";
            return;
        }

        var willFavorite = !FavoriteSongService.Instance.IsFavorite(CurrentSong);
        StatusMessage = MusicService.IsLoggedIn
            ? willFavorite ? "正在同步到我喜欢" : "正在从我喜欢移除"
            : willFavorite ? "正在添加到本地喜欢" : "正在取消本地喜欢";

        try
        {
            var isFavorite = await FavoriteSongService.Instance.ToggleFavoriteAsync(CurrentSong);
            RefreshFavoriteState();
            StatusMessage = MusicService.IsLoggedIn
                ? isFavorite ? "已同步到我喜欢" : "已从我喜欢移除"
                : isFavorite ? "已添加到本地喜欢" : "已取消本地喜欢";
        }
        catch (Exception ex)
        {
            RefreshFavoriteState();
            StatusMessage = $"我喜欢同步失败：{FirstLine(ex.GetBaseException().Message)}";
        }
    }

    public void RefreshFavoriteState()
    {
        OnPropertyChanged(nameof(IsCurrentSongFavorite));
        OnPropertyChanged(nameof(FavoriteStatusText));
    }

    public void Stop()
    {
        ExitRadioMode();
        FinishCurrentPlaybackHistory(completed: false);
        ResetActiveLoad();
        _audioPlayer?.Stop();
        IsPlaying = false;
        SetProgressFromPlayer(0);
        StatusMessage = "已停止";
        LastErrorDetail = string.Empty;
        StopProgressTimer();
        PersistPlaybackState(force: true);
    }

    private void ReplaceQueue(IReadOnlyList<KugouSong> songs, string? title)
    {
        Queue.Clear();
        foreach (var song in songs)
        {
            Queue.Add(song);
        }

        QueueTitle = string.IsNullOrWhiteSpace(title) ? "临时播放" : title.Trim();
        CurrentQueueIndex = Queue.Count > 0 ? 0 : -1;
        NotifyQueueChanged();
    }

    private int ResolveNextQueueIndex(bool forceAdvance)
    {
        if (Queue.Count == 0)
        {
            return -1;
        }

        var currentIndex = ResolveCurrentQueueIndex();
        if (PlayMode == PlaybackMode.SingleLoop && !forceAdvance)
        {
            return Math.Clamp(currentIndex, 0, Queue.Count - 1);
        }

        if (PlayMode == PlaybackMode.Shuffle)
        {
            if (Queue.Count == 1)
            {
                return forceAdvance || PlayMode != PlaybackMode.Sequential ? 0 : -1;
            }

            var nextIndex = currentIndex;
            for (var attempt = 0; attempt < 8 && nextIndex == currentIndex; attempt++)
            {
                nextIndex = _random.Next(Queue.Count);
            }

            return nextIndex == currentIndex ? (currentIndex + 1) % Queue.Count : nextIndex;
        }

        if (currentIndex < 0)
        {
            return 0;
        }

        if (currentIndex < Queue.Count - 1)
        {
            return currentIndex + 1;
        }

        return PlayMode == PlaybackMode.ListLoop ? 0 : -1;
    }

    private int ResolvePreviousQueueIndex()
    {
        if (Queue.Count == 0)
        {
            return -1;
        }

        var currentIndex = ResolveCurrentQueueIndex();
        if (currentIndex > 0)
        {
            return currentIndex - 1;
        }

        return PlayMode == PlaybackMode.ListLoop ? Queue.Count - 1 : -1;
    }

    private int ResolveCurrentQueueIndex()
    {
        if (CurrentQueueIndex >= 0 && CurrentQueueIndex < Queue.Count)
        {
            return CurrentQueueIndex;
        }

        if (CurrentSong is null)
        {
            return -1;
        }

        var index = Queue.ToList().FindIndex(song => IsSameSong(song, CurrentSong));
        if (index >= 0)
        {
            CurrentQueueIndex = index;
        }

        return index;
    }

    private void NotifyQueueChanged()
    {
        OnPropertyChanged(nameof(Queue));
        OnPropertyChanged(nameof(HasQueue));
        OnPropertyChanged(nameof(IsQueueEmpty));
        OnPropertyChanged(nameof(CurrentQueuePositionText));
        OnPropertyChanged(nameof(QueueStatusText));
    }

    partial void OnIsRadioModeChanged(bool value)
    {
        NotifyQueueChanged();
    }

    private void ExitRadioMode()
    {
        _radioNextSongProvider = null;
        if (IsRadioMode)
        {
            IsRadioMode = false;
        }
    }

    public static bool IsSameSong(KugouSong? left, KugouSong? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

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

    partial void OnVolumeChanged(double value)
    {
        var player = _audioPlayer;
        if (player is not null)
        {
            player.Volume = ToPlayerVolume(value);
        }

        PersistPlaybackState();
    }

    partial void OnProgressChanged(double value)
    {
        if (_isUpdatingProgress || _audioPlayer is null || !HasSong)
        {
            return;
        }

        var target = Math.Clamp(value, 0, Math.Max(Duration, 0));
        if (Math.Abs(_audioPlayer.GetTime() - target) > 1.25)
        {
            _audioPlayer.Seek(target);
            PersistPlaybackState(force: true);
        }
    }

    private async Task<PlaybackSource> ResolvePlaybackSourceAsync(KugouSong song, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(song.AudioUrl))
        {
            return new PlaybackSource(song.AudioUrl, IsLocalFile: false, Quality: null);
        }

        if (string.IsNullOrWhiteSpace(song.Hash))
        {
            throw new InvalidOperationException("当前歌曲缺少 hash，无法解析播放地址");
        }

        var source = await MusicService.Client.ResolveSongAudioUrlTypedAsync(song, preferredQuality: MusicService.DefaultPlaybackQualityValue, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(source.Url))
        {
            throw new InvalidOperationException("酷狗接口没有返回可播放地址。" + Environment.NewLine + DescribeResponse(source.Raw));
        }

        return new PlaybackSource(source.Url, IsLocalFile: false, Quality: source.Quality);
    }

    private void OnProgressTick(object? sender, EventArgs e)
    {
        var player = _audioPlayer;
        if (!IsPlaying || player is null)
        {
            StopProgressTimer();
            return;
        }

        var duration = player.GetDuration();
        if (duration > 0 && Math.Abs(duration - Duration) > 0.5)
        {
            Duration = duration;
        }

        SetProgressFromPlayer(player.GetTime());
        PersistPlaybackState();
    }

    private void OnPlaybackCompleted()
    {
        if (_disposed)
        {
            return;
        }

        FinishCurrentPlaybackHistory(completed: true);
        PersistPlaybackState(force: true);

        if (IsRadioMode)
        {
            _ = PlayNextRadioSongAsync(userInitiated: false);
            return;
        }

        var nextIndex = ResolveNextQueueIndex(forceAdvance: false);
        if (nextIndex >= 0)
        {
            _ = PlayQueueIndexAsync(nextIndex);
            return;
        }

        IsPlaying = false;
        SetProgressFromPlayer(Duration);
        StopProgressTimer();
        StatusMessage = "播放完成";
        LastErrorDetail = string.Empty;
    }

    private void RestorePersistedPlaybackState()
    {
        _isRestoringPlaybackState = true;
        try
        {
            var state = LocalMusicStore.Instance.LoadPlaybackState();
            if (state is null)
            {
                return;
            }

            if (Enum.TryParse<PlaybackMode>(state.PlaybackMode, out var playbackMode))
            {
                PlayMode = playbackMode;
            }

            if (state.Volume > 0)
            {
                Volume = Math.Clamp(state.Volume, 0, 100);
            }

            QueueTitle = string.IsNullOrWhiteSpace(state.QueueTitle) ? "临时播放" : state.QueueTitle;
            IsRadioMode = false;
            Queue.Clear();
            foreach (var song in LocalMusicStore.Instance.LoadCurrentQueueSongs())
            {
                Queue.Add(song);
            }

            CurrentQueueIndex = Queue.Count > 0 ? Math.Clamp(state.CurrentQueueIndex, 0, Queue.Count - 1) : -1;
            CurrentSong = LocalMusicStore.Instance.LoadSongSnapshot(state.CurrentSongKey) ??
                (CurrentQueueIndex >= 0 && CurrentQueueIndex < Queue.Count ? Queue[CurrentQueueIndex] : null);

            if (CurrentSong is not null)
            {
                Duration = state.DurationSeconds > 0 ? state.DurationSeconds : NormalizeDuration(CurrentSong.Duration);
                SetProgressFromPlayer(state.ProgressSeconds);
                _pendingResumeSongKey = LocalMusicStore.GetSongKey(CurrentSong);
                _pendingResumeProgress = state.ProgressSeconds;
                StatusMessage = "已恢复上次播放";
            }
        }
        catch
        {
            StatusMessage = "本地播放状态恢复失败";
        }
        finally
        {
            _isRestoringPlaybackState = false;
            NotifyQueueChanged();
        }
    }

    private void RecordPlaybackError(KugouSong song, Exception ex, string? resolvedUrl)
    {
        var message = FirstLine(ex.GetBaseException().Message);
        StatusMessage = $"播放失败：{message}";
        LastErrorDetail = BuildPlaybackErrorDetail(song, ex, resolvedUrl);
        AppendPlaybackErrorLog(LastErrorDetail);
    }

    private static string BuildPlaybackErrorDetail(KugouSong song, Exception ex, string? resolvedUrl)
    {
        var builder = new StringBuilder();
        builder.AppendLine("播放失败详情");
        builder.AppendLine($"时间: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"日志: {ErrorLogPath}");
        builder.AppendLine();
        builder.AppendLine("歌曲:");
        builder.AppendLine($"  标题: {song.Title}");
        builder.AppendLine($"  歌手: {song.Artist}");
        builder.AppendLine($"  Hash: {song.Hash}");
        builder.AppendLine($"  MixSongId: {song.MixSongId}");
        builder.AppendLine($"  AlbumId: {song.AlbumId ?? string.Empty}");
        builder.AppendLine($"  AudioUrl: {SanitizeUrlForDisplay(song.AudioUrl)}");
        builder.AppendLine($"  ResolvedUrl: {SanitizeUrlForDisplay(resolvedUrl)}");
        builder.AppendLine();
        builder.AppendLine("异常:");
        builder.AppendLine(ex.ToString());
        return builder.ToString();
    }

    private static string DescribeResponse(KugouResponse response)
    {
        var body = TrimForDisplay(response.BodyText, 12000);
        return $"HTTP: {response.StatusCodeNumber} {response.StatusCode}" + Environment.NewLine +
               $"BodyLength: {response.BodyText.Length}" + Environment.NewLine +
               "Body:" + Environment.NewLine +
               body;
    }

    private static void AppendPlaybackErrorLog(string detail)
    {
        try
        {
            var directory = Path.GetDirectoryName(ErrorLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(ErrorLogPath, detail + Environment.NewLine + new string('-', 88) + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // The on-screen error detail is the primary reporting surface.
        }
    }

    private static string FirstLine(string text)
    {
        var line = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "未知错误";
        return line.Length > 140 ? line[..140] + "..." : line;
    }

    private static string TrimForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + Environment.NewLine + $"... 已截断，完整响应长度 {text.Length} 字符";
    }

    private static string SanitizeUrlForDisplay(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Path) + (string.IsNullOrEmpty(uri.Query) ? string.Empty : "?...");
        }

        return url;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        var size = (double)bytes;
        var units = new[] { "B", "KB", "MB", "GB" };
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private static void HandleLocalPlaybackFailure(KugouSong song, string localPath, Exception ex)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }
        }
        catch
        {
            // Ignore cleanup failure and continue with remote fallback.
        }

        LocalMusicStore.Instance.MarkDownloadFailed(song, localPath, null, localPath, ex);
    }

    private void ResetActiveLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        IsLoading = false;
    }

    private void DisposeCurrentPlayer()
    {
        AudioPlayer? player;
        lock (_playerLock)
        {
            player = _audioPlayer;
            _audioPlayer = null;
        }

        player?.Dispose();
    }

    private void SetProgressFromPlayer(double value)
    {
        _isUpdatingProgress = true;
        try
        {
            Progress = Math.Clamp(value, 0, Math.Max(Duration, 0));
        }
        finally
        {
            _isUpdatingProgress = false;
        }
    }

    private void PersistPlaybackState(bool force = false, bool saveQueue = false)
    {
        if (_isRestoringPlaybackState)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!force && now - _lastPlaybackStateSaveUtc < TimeSpan.FromSeconds(10))
        {
            return;
        }

        _lastPlaybackStateSaveUtc = now;
        try
        {
            LocalMusicStore.Instance.SavePlaybackState(
                CurrentSong,
                Queue.ToList(),
                QueueTitle,
                CurrentQueueIndex,
                PlayMode,
                Volume,
                Progress,
                Duration,
                IsRadioMode,
                saveQueue);
            UpdateCurrentPlaybackHistory(completed: false);
        }
        catch
        {
        }
    }

    private double TakePendingResumeProgress(KugouSong song)
    {
        var songKey = LocalMusicStore.GetSongKey(song);
        if (string.IsNullOrWhiteSpace(songKey) || !string.Equals(songKey, _pendingResumeSongKey, StringComparison.Ordinal))
        {
            return 0;
        }

        var progress = _pendingResumeProgress;
        _pendingResumeSongKey = string.Empty;
        _pendingResumeProgress = 0;
        return progress;
    }

    private void StartPlaybackHistory(KugouSong song, PlaybackSource playbackSource)
    {
        try
        {
            _currentHistoryId = LocalMusicStore.Instance.RecordPlaybackStarted(
                song,
                playbackSource.IsLocalFile ? "local" : "stream",
                Duration);
        }
        catch
        {
            _currentHistoryId = string.Empty;
        }
    }

    private void UpdateCurrentPlaybackHistory(bool completed)
    {
        if (string.IsNullOrWhiteSpace(_currentHistoryId))
        {
            return;
        }

        try
        {
            LocalMusicStore.Instance.UpdatePlaybackHistory(_currentHistoryId, Progress, Duration, completed);
        }
        catch
        {
        }
    }

    private void FinishCurrentPlaybackHistory(bool completed)
    {
        UpdateCurrentPlaybackHistory(completed);
        _currentHistoryId = string.Empty;
    }

    private void StartProgressTimer()
    {
        if (!_progressTimer.IsEnabled)
        {
            _progressTimer.Start();
        }
    }

    private void StopProgressTimer()
    {
        if (_progressTimer.IsEnabled)
        {
            _progressTimer.Stop();
        }
    }

    private static double NormalizeDuration(int duration)
    {
        if (duration <= 0)
        {
            return 240;
        }

        return duration > 10000 ? Math.Round(duration / 1000d) : duration;
    }

    private static float ToPlayerVolume(double value)
    {
        return (float)(Math.Clamp(value, 0, 100) / 100d);
    }

    private static string FormatDuration(double seconds)
    {
        if (double.IsNaN(seconds) || seconds <= 0)
        {
            return "0:00";
        }

        var value = TimeSpan.FromSeconds(seconds);
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }

    private sealed record PlaybackSource(string Location, bool IsLocalFile, string? Quality);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopProgressTimer();
        ResetActiveLoad();
        FinishCurrentPlaybackHistory(completed: false);
        PersistPlaybackState(force: true);
        DisposeCurrentPlayer();
    }
}