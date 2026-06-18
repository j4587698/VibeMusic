using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using KuGouMusicAvalonia.Services;
using System;

namespace KuGouMusicAvalonia.ViewModels;

public sealed partial class DesktopLyricsViewModel : ViewModelBase
{
    public DesktopLyricsViewModel()
    {
        FloatingLyricsService.Instance.StateChanged += OnServiceStateChanged;
    }

    private void OnServiceStateChanged(object? sender, System.EventArgs e)
    {
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(IsCompactMode));
        OnPropertyChanged(nameof(ShowExpandedContent));
        OnPropertyChanged(nameof(CurrentLineFontSize));
        OnPropertyChanged(nameof(NextLineFontSize));
        OnPropertyChanged(nameof(CurrentLineMaxLines));
        OnPropertyChanged(nameof(ContentMargin));
        OnPropertyChanged(nameof(WindowHeight));
        OnPropertyChanged(nameof(WindowMinHeight));
    }

    public void Cleanup()
    {
        FloatingLyricsService.Instance.StateChanged -= OnServiceStateChanged;
    }

    public PlayerService Player => PlayerService.Instance;

    public LyricsService Lyrics => LyricsService.Instance;

    public bool IsLocked
    {
        get => FloatingLyricsService.Instance.IsLocked;
        set => FloatingLyricsService.Instance.IsLocked = value;
    }

    public bool IsCompactMode => FloatingLyricsService.Instance.IsCompactMode;

    public bool ShowExpandedContent => !IsCompactMode;

    public double CurrentLineFontSize => Math.Round(FloatingLyricsService.Instance.FontSize);

    public double NextLineFontSize => Math.Round(FloatingLyricsService.Instance.FontSize / 2);

    public int CurrentLineMaxLines => IsCompactMode ? 1 : 2;

    public Thickness ContentMargin => IsCompactMode ? new Thickness(18, 8) : new Thickness(20, 12);

    public double WindowHeight => IsCompactMode ? 92 : 160;

    public double WindowMinHeight => IsCompactMode ? 76 : 120;

    [ObservableProperty]
    private bool _isHovered;

    [RelayCommand]
    private void ToggleLock()
    {
        IsLocked = !IsLocked;
    }
}
