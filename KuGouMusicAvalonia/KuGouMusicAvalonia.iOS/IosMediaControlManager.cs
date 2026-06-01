using AVFoundation;
using Foundation;
using KuGouMusicAvalonia.Services;
using MediaPlayer;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using UIKit;

namespace KuGouMusicAvalonia.iOS;

internal sealed class IosMediaControlManager : IDisposable
{
    private static readonly Lazy<IosMediaControlManager> LazyInstance = new(() => new IosMediaControlManager());
    private static readonly HttpClient CoverClient = new();

    private NSObject? _playHandler;
    private NSObject? _pauseHandler;
    private NSObject? _toggleHandler;
    private NSObject? _nextHandler;
    private NSObject? _previousHandler;
    private string _cachedCoverUrl = string.Empty;
    private MPMediaItemArtwork? _cachedArtwork;
    private bool _isUpdatingArtwork;
    private bool _initialized;

    public static IosMediaControlManager Instance => LazyInstance.Value;

    private IosMediaControlManager()
    {
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        ConfigureAudioSession();
        BindRemoteCommands();
        PlayerService.Instance.PropertyChanged += OnPlayerPropertyChanged;
        _ = EnsureArtworkAsync(PlayerService.Instance.CurrentCoverUrl);
        UpdateNowPlayingInfo();
        _initialized = true;
    }

    private static void ConfigureAudioSession()
    {
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSession.CategoryPlayback);
        session.SetActive(true);
        UIApplication.SharedApplication.BeginReceivingRemoteControlEvents();
    }

    private void BindRemoteCommands()
    {
        var commandCenter = MPRemoteCommandCenter.Shared;

        commandCenter.PlayCommand.Enabled = true;
        commandCenter.PauseCommand.Enabled = true;
        commandCenter.TogglePlayPauseCommand.Enabled = true;
        commandCenter.NextTrackCommand.Enabled = true;
        commandCenter.PreviousTrackCommand.Enabled = true;

        _playHandler = commandCenter.PlayCommand.AddTarget(_ =>
        {
            var player = PlayerService.Instance;
            if (!player.IsPlaying)
            {
                player.TogglePlayPause();
            }
            return MPRemoteCommandHandlerStatus.Success;
        });

        _pauseHandler = commandCenter.PauseCommand.AddTarget(_ =>
        {
            var player = PlayerService.Instance;
            if (player.IsPlaying)
            {
                player.TogglePlayPause();
            }
            return MPRemoteCommandHandlerStatus.Success;
        });

        _toggleHandler = commandCenter.TogglePlayPauseCommand.AddTarget(_ =>
        {
            PlayerService.Instance.TogglePlayPause();
            return MPRemoteCommandHandlerStatus.Success;
        });

        _nextHandler = commandCenter.NextTrackCommand.AddTarget(_ =>
        {
            _ = PlayerService.Instance.SkipNextAsync();
            return MPRemoteCommandHandlerStatus.Success;
        });

        _previousHandler = commandCenter.PreviousTrackCommand.AddTarget(_ =>
        {
            _ = PlayerService.Instance.SkipPreviousAsync();
            return MPRemoteCommandHandlerStatus.Success;
        });
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerService.CurrentSong)
            or nameof(PlayerService.CurrentTitle)
            or nameof(PlayerService.CurrentArtist)
            or nameof(PlayerService.IsPlaying)
            or nameof(PlayerService.Duration)
            or nameof(PlayerService.CurrentCoverUrl))
        {
            if (e.PropertyName == nameof(PlayerService.CurrentCoverUrl))
            {
                _ = EnsureArtworkAsync(PlayerService.Instance.CurrentCoverUrl);
            }

            UIApplication.SharedApplication.BeginInvokeOnMainThread(UpdateNowPlayingInfo);
        }
    }

    private void UpdateNowPlayingInfo()
    {
        var player = PlayerService.Instance;
        if (!player.HasSong)
        {
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null;
            return;
        }

        var nowPlaying = new NSMutableDictionary
        {
            [MPMediaItemProperty.Title] = new NSString(player.CurrentTitle),
            [MPMediaItemProperty.Artist] = new NSString(player.CurrentArtist),
            [MPMediaItemProperty.PlaybackDuration] = NSNumber.FromDouble(Math.Max(0, player.Duration)),
            [MPNowPlayingInfoProperty.ElapsedPlaybackTime] = NSNumber.FromDouble(Math.Max(0, player.Progress)),
            [MPNowPlayingInfoProperty.PlaybackRate] = NSNumber.FromDouble(player.IsPlaying ? 1.0 : 0.0)
        };

        if (_cachedArtwork is not null)
        {
            nowPlaying[MPMediaItemProperty.Artwork] = _cachedArtwork;
        }

        MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlaying;
    }

    private async Task EnsureArtworkAsync(string? coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            _cachedCoverUrl = string.Empty;
            _cachedArtwork = null;
            UIApplication.SharedApplication.BeginInvokeOnMainThread(UpdateNowPlayingInfo);
            return;
        }

        if (_isUpdatingArtwork || string.Equals(_cachedCoverUrl, coverUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _isUpdatingArtwork = true;
        try
        {
            var bytes = await CoverClient.GetByteArrayAsync(coverUrl).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return;
            }

            using var data = NSData.FromArray(bytes);
            var image = UIImage.LoadFromData(data);
            if (image is null)
            {
                return;
            }

            _cachedArtwork = new MPMediaItemArtwork(image);
            _cachedCoverUrl = coverUrl;
            UIApplication.SharedApplication.BeginInvokeOnMainThread(UpdateNowPlayingInfo);
        }
        catch
        {
            // Ignore artwork failures to keep media control flow resilient.
        }
        finally
        {
            _isUpdatingArtwork = false;
        }
    }

    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        PlayerService.Instance.PropertyChanged -= OnPlayerPropertyChanged;

        var commandCenter = MPRemoteCommandCenter.Shared;
        if (_playHandler is not null)
        {
            commandCenter.PlayCommand.RemoveTarget(_playHandler);
        }

        if (_pauseHandler is not null)
        {
            commandCenter.PauseCommand.RemoveTarget(_pauseHandler);
        }

        if (_toggleHandler is not null)
        {
            commandCenter.TogglePlayPauseCommand.RemoveTarget(_toggleHandler);
        }

        if (_nextHandler is not null)
        {
            commandCenter.NextTrackCommand.RemoveTarget(_nextHandler);
        }

        if (_previousHandler is not null)
        {
            commandCenter.PreviousTrackCommand.RemoveTarget(_previousHandler);
        }

        UIApplication.SharedApplication.EndReceivingRemoteControlEvents();
        _initialized = false;
    }
}
