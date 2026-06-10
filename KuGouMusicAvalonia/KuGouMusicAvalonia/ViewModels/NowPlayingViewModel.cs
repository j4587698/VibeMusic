using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public sealed partial class NowPlayingViewModel : ViewModelBase
{
    public NowPlayingViewModel()
    {
        DesktopLyricsWindowService.Instance.StateChanged += (_, _) => 
        {
            OnPropertyChanged(nameof(IsDesktopLyricsOpen));
            OnPropertyChanged(nameof(IsDesktopLyricsLocked));
        };
    }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCoverModeVisible))]
    [NotifyPropertyChangedFor(nameof(IsLyricsModeVisible))]
    private bool _isWideLayout;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCoverModeVisible))]
    [NotifyPropertyChangedFor(nameof(IsLyricsModeVisible))]
    private bool _isCompactLyricsVisible;

    [ObservableProperty]
    private bool _isTightLandscape;

    private double _lyricSeekPreviewTime = -1;

    [ObservableProperty]
    private bool _isLyricSeekPreviewVisible;

    [ObservableProperty]
    private string _lyricSeekPreviewTimeText = string.Empty;

    [ObservableProperty]
    private string _lyricSeekPreviewLineText = string.Empty;

    public PlayerService Player => PlayerService.Instance;

    public LyricsService Lyrics => LyricsService.Instance;

    public bool IsCoverModeVisible => IsWideLayout || !IsCompactLyricsVisible;

    public bool IsLyricsModeVisible => IsWideLayout || IsCompactLyricsVisible;

    public bool IsDesktopLyricsOpen => DesktopLyricsWindowService.Instance.IsOpen;

    public bool IsDesktopLyricsSupported => DesktopLyricsWindowService.Instance.IsSupported;

    public bool IsDesktopLyricsLocked => DesktopLyricsWindowService.Instance.IsLocked;

    [RelayCommand]
    private void Close()
    {
        ShellNavigationService.Instance.CloseNowPlaying();
    }

    [RelayCommand]
    private void ToggleDesktopLyrics()
    {
        DesktopLyricsWindowService.Instance.Toggle();
    }

    [RelayCommand]
    private void ToggleDesktopLyricsLock()
    {
        DesktopLyricsWindowService.Instance.IsLocked = !DesktopLyricsWindowService.Instance.IsLocked;
    }

    [RelayCommand]
    private void ToggleCompactLyrics()
    {
        if (!IsWideLayout)
        {
            IsCompactLyricsVisible = !IsCompactLyricsVisible;
        }
    }

    [RelayCommand]
    private void ToggleQueue()
    {
        ShellNavigationService.Instance.ToggleQueue();
    }

    [RelayCommand]
    private void TogglePlay()
    {
        Player.TogglePlayPause();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        await Player.ToggleCurrentFavoriteAsync();
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        await Player.DownloadAsync(null);
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        Player.ToggleShuffle();
    }

    [RelayCommand]
    private void CyclePlayMode()
    {
        Player.CyclePlayMode();
    }

    [RelayCommand]
    private void CycleRepeatMode()
    {
        Player.CyclePlayMode();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await Player.SkipNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await Player.SkipPreviousAsync();
    }

    [RelayCommand]
    private async Task PlayQueueSongAsync(KugouSong song)
    {
        await Player.PlayQueueSongAsync(song);
    }

    [RelayCommand]
    private async Task RemoveQueueSongAsync(KugouSong song)
    {
        await Player.RemoveFromQueueAsync(song);
    }

    [RelayCommand]
    private void ClearQueue()
    {
        Player.ClearQueue();
    }

    [RelayCommand]
    private void OpenLyrics()
    {
        IsCompactLyricsVisible = true;
    }

    public void ShowLyricSeekPreview(LyricLine? line)
    {
        if (line is null || line.IsPlaceholder || line.StartTime < 0)
        {
            return;
        }

        _lyricSeekPreviewTime = line.StartTime;
        LyricSeekPreviewTimeText = string.IsNullOrWhiteSpace(line.TimeText) ? FormatTime(line.StartTime) : line.TimeText;
        LyricSeekPreviewLineText = line.Text;
        IsLyricSeekPreviewVisible = true;
    }

    public void HideLyricSeekPreview()
    {
        _lyricSeekPreviewTime = -1;
        LyricSeekPreviewTimeText = string.Empty;
        LyricSeekPreviewLineText = string.Empty;
        IsLyricSeekPreviewVisible = false;
    }

    [RelayCommand]
    private void ConfirmLyricSeekPreview()
    {
        if (_lyricSeekPreviewTime >= 0)
        {
            Player.Progress = _lyricSeekPreviewTime;
        }

        HideLyricSeekPreview();
    }

    [RelayCommand]
    private void CancelLyricSeekPreview()
    {
        HideLyricSeekPreview();
    }

    private static string FormatTime(double seconds)
    {
        var totalSeconds = (int)Math.Round(seconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
