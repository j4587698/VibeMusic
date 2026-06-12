using Android.App;
using Android;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Text;
using KuGouMusicAvalonia.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Android;

internal sealed class AndroidMediaControlManager : IDisposable
{
    internal const string ChannelId = "kugou_playback";
    internal const int NotificationId = 1001;

    internal const string ActionTogglePlayPause = "com.kugoumusicavalonia.action.TOGGLE_PLAY_PAUSE";
    internal const string ActionNext = "com.kugoumusicavalonia.action.NEXT";
    internal const string ActionPrevious = "com.kugoumusicavalonia.action.PREVIOUS";
    internal const string ActionSyncForeground = "com.kugoumusicavalonia.action.SYNC_FOREGROUND";
    internal const string ActionStopForeground = "com.kugoumusicavalonia.action.STOP_FOREGROUND";

    private static readonly Lazy<AndroidMediaControlManager> LazyInstance = new(() => new AndroidMediaControlManager());
    private static readonly HttpClient CoverClient = new();

    private NotificationManager? _notificationManager;
    private MediaSession? _mediaSession;
    private Context? _context;
    private bool _initialized;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private CancellationTokenSource? _coverLoadCts;
    private string _cachedCoverUrl = string.Empty;
    private Bitmap? _cachedCoverBitmap;

    public static AndroidMediaControlManager Instance => LazyInstance.Value;

    private AndroidMediaControlManager()
    {
    }

    public void Initialize(Context context)
    {
        if (_initialized)
        {
            return;
        }

        _context = context.ApplicationContext;
        _notificationManager = (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        EnsureNotificationChannel();

        _mediaSession = new MediaSession(_context, "KuGouMusicPlaybackSession");
        _mediaSession.SetCallback(new SessionCallback());
        _mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls);
        _mediaSession.Active = true;

        PlayerService.Instance.PropertyChanged += OnPlayerPropertyChanged;
        _initialized = true;
        _ = UpdateNowPlayingNotificationAsync();
    }

    public void SyncNowPlayingNotification()
    {
        _ = UpdateNowPlayingNotificationAsync();
    }

    public void HandleAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        switch (action)
        {
            case ActionTogglePlayPause:
                PlayerService.Instance.TogglePlayPause();
                break;
            case ActionNext:
                _ = PlayerService.Instance.SkipNextAsync();
                break;
            case ActionPrevious:
                _ = PlayerService.Instance.SkipPreviousAsync();
                break;
        }
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerService.CurrentSong)
            or nameof(PlayerService.IsPlaying)
            or nameof(PlayerService.IsLoading)
            or nameof(PlayerService.Duration)
            or nameof(PlayerService.CurrentTitle)
            or nameof(PlayerService.CurrentArtist)
            or nameof(PlayerService.CurrentCoverUrl))
        {
            _ = UpdateNowPlayingNotificationAsync();
        }
    }

    private async Task UpdateNowPlayingNotificationAsync()
    {
        if (!_initialized || _context is null || _notificationManager is null || _mediaSession is null)
        {
            return;
        }

        await _syncGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var player = PlayerService.Instance;
            if (!player.HasSong)
            {
                _notificationManager.Cancel(NotificationId);
                PlaybackNotificationService.RequestStop(_context);
                return;
            }

            if (!HasNotificationPermission())
            {
                _notificationManager.Cancel(NotificationId);
                PlaybackNotificationService.RequestStop(_context);
                return;
            }

            var coverBitmap = await EnsureCoverBitmapAsync(player.CurrentCoverUrl).ConfigureAwait(false);

            UpdatePlaybackState(player);
            UpdateMetadata(player, coverBitmap);

            var notification = BuildNotification(player, coverBitmap);
            _notificationManager.Notify(NotificationId, notification);
            PlaybackNotificationService.RequestSync(_context);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    internal Notification? BuildNotificationForForegroundService()
    {
        if (!_initialized)
        {
            return null;
        }

        var player = PlayerService.Instance;
        if (!player.HasSong || !HasNotificationPermission())
        {
            return null;
        }

        return BuildNotification(player, _cachedCoverBitmap);
    }

    private bool HasNotificationPermission()
    {
        return _context is not null
            && (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu
                || _context.CheckSelfPermission(Manifest.Permission.PostNotifications) == Permission.Granted);
    }

    private Notification BuildNotification(PlayerService player, Bitmap? coverBitmap)
    {
        if (_context is null || _mediaSession is null)
        {
            throw new InvalidOperationException("Android media control manager is not initialized.");
        }

        var launchIntent = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName!);
        var contentIntent = launchIntent is null
            ? null
            : PendingIntent.GetActivity(
                _context,
                0,
                launchIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var previousIntent = BuildActionPendingIntent(ActionPrevious, 1);
        var toggleIntent = BuildActionPendingIntent(ActionTogglePlayPause, 2);
        var nextIntent = BuildActionPendingIntent(ActionNext, 3);

        var builder = new Notification.Builder(_context, ChannelId)
            .SetContentTitle(player.CurrentTitle)
            .SetContentText(player.CurrentArtist)
            .SetSmallIcon(Resource.Drawable.Icon)
            .SetShowWhen(false)
            .SetOnlyAlertOnce(true)
            .SetOngoing(player.IsPlaying)
            .SetVisibility(NotificationVisibility.Public)
            .SetStyle(new Notification.MediaStyle().SetMediaSession(_mediaSession.SessionToken).SetShowActionsInCompactView(0, 1, 2))
            .AddAction(new Notification.Action.Builder(global::Android.Resource.Drawable.IcMediaPrevious, ToCharSequence("上一首"), previousIntent).Build())
            .AddAction(new Notification.Action.Builder(player.IsPlaying ? global::Android.Resource.Drawable.IcMediaPause : global::Android.Resource.Drawable.IcMediaPlay, ToCharSequence(player.IsPlaying ? "暂停" : "播放"), toggleIntent).Build())
            .AddAction(new Notification.Action.Builder(global::Android.Resource.Drawable.IcMediaNext, ToCharSequence("下一首"), nextIntent).Build());

        if (coverBitmap is not null)
        {
            builder.SetLargeIcon(coverBitmap);
        }

        if (contentIntent is not null)
        {
            builder.SetContentIntent(contentIntent);
        }

        return builder.Build();
    }

    private PendingIntent BuildActionPendingIntent(string action, int requestCode)
    {
        if (_context is null)
        {
            throw new InvalidOperationException("Android media control manager is not initialized.");
        }

        var intent = new Intent(_context, typeof(MediaControlBroadcastReceiver));
        intent.SetAction(action);
        return PendingIntent.GetBroadcast(
            _context,
            requestCode,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    private void EnsureNotificationChannel()
    {
        if (_notificationManager is null || Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, "播放控制", NotificationImportance.Low)
        {
            Description = "音乐播放通知与控制"
        };
        channel.SetShowBadge(false);
        _notificationManager.CreateNotificationChannel(channel);
    }

    private void UpdatePlaybackState(PlayerService player)
    {
        if (_mediaSession is null)
        {
            return;
        }

        var actions = PlaybackState.ActionPlay
            | PlaybackState.ActionPause
            | PlaybackState.ActionPlayPause
            | PlaybackState.ActionSkipToNext
            | PlaybackState.ActionSkipToPrevious;

        var state = player.IsPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused;
        if (player.IsLoading)
        {
            state = PlaybackStateCode.Buffering;
        }

        var playbackState = new PlaybackState.Builder()
            .SetActions((long)actions)
            .SetState(state, (long)(player.Progress * 1000), player.IsPlaying ? 1f : 0f, SystemClock.ElapsedRealtime())
            .Build();

        _mediaSession.SetPlaybackState(playbackState);
    }

    private void UpdateMetadata(PlayerService player, Bitmap? coverBitmap)
    {
        if (_mediaSession is null)
        {
            return;
        }

        var metadataBuilder = new MediaMetadata.Builder()
            .PutString(MediaMetadata.MetadataKeyTitle, player.CurrentTitle)
            .PutString(MediaMetadata.MetadataKeyArtist, player.CurrentArtist)
            .PutLong(MediaMetadata.MetadataKeyDuration, (long)(player.Duration * 1000));

        if (coverBitmap is not null)
        {
            metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, coverBitmap);
        }

        var metadata = metadataBuilder.Build();

        _mediaSession.SetMetadata(metadata);
    }

    private async Task<Bitmap?> EnsureCoverBitmapAsync(string? coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            _cachedCoverUrl = string.Empty;
            _cachedCoverBitmap?.Dispose();
            _cachedCoverBitmap = null;
            return null;
        }

        if (string.Equals(_cachedCoverUrl, coverUrl, StringComparison.OrdinalIgnoreCase) && _cachedCoverBitmap is not null)
        {
            return _cachedCoverBitmap;
        }

        _coverLoadCts?.Cancel();
        _coverLoadCts?.Dispose();
        _coverLoadCts = new CancellationTokenSource();

        try
        {
            var bytes = await CoverClient.GetByteArrayAsync(coverUrl, _coverLoadCts.Token).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return null;
            }

            var bitmap = await Task.Run(() =>
            {
                using var ms = new MemoryStream(bytes);
                return BitmapFactory.DecodeStream(ms);
            }).ConfigureAwait(false);
            if (bitmap is null)
            {
                return null;
            }

            _cachedCoverBitmap?.Dispose();
            _cachedCoverBitmap = bitmap;
            _cachedCoverUrl = coverUrl;
            return _cachedCoverBitmap;
        }
        catch
        {
            return _cachedCoverBitmap;
        }
    }

    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        PlayerService.Instance.PropertyChanged -= OnPlayerPropertyChanged;
        _coverLoadCts?.Cancel();
        _coverLoadCts?.Dispose();
        _coverLoadCts = null;
        _cachedCoverBitmap?.Dispose();
        _cachedCoverBitmap = null;
        _mediaSession?.Release();
        _mediaSession?.Dispose();
        _mediaSession = null;
        _initialized = false;
    }

    private static Java.Lang.ICharSequence ToCharSequence(string str)
    {
        return new Java.Lang.String(str);
    }

    private sealed class SessionCallback : MediaSession.Callback
    {
        public override void OnPlay()
        {
            var player = PlayerService.Instance;
            if (!player.IsPlaying)
            {
                player.TogglePlayPause();
            }
        }

        public override void OnPause()
        {
            var player = PlayerService.Instance;
            if (player.IsPlaying)
            {
                player.TogglePlayPause();
            }
        }

        public override void OnSkipToNext()
        {
            _ = PlayerService.Instance.SkipNextAsync();
        }

        public override void OnSkipToPrevious()
        {
            _ = PlayerService.Instance.SkipPreviousAsync();
        }
    }
}
