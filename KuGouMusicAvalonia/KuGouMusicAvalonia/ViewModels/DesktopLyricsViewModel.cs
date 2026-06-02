using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGouMusicAvalonia.Services;

namespace KuGouMusicAvalonia.ViewModels;

public sealed partial class DesktopLyricsViewModel : ViewModelBase
{
    public DesktopLyricsViewModel()
    {
        DesktopLyricsWindowService.Instance.StateChanged += OnServiceStateChanged;
    }

    private void OnServiceStateChanged(object? sender, System.EventArgs e)
        => OnPropertyChanged(nameof(IsLocked));

    public void Cleanup()
    {
        DesktopLyricsWindowService.Instance.StateChanged -= OnServiceStateChanged;
    }

    public PlayerService Player => PlayerService.Instance;

    public LyricsService Lyrics => LyricsService.Instance;

    public bool IsLocked
    {
        get => DesktopLyricsWindowService.Instance.IsLocked;
        set => DesktopLyricsWindowService.Instance.IsLocked = value;
    }

    [ObservableProperty]
    private bool _isHovered;

    [RelayCommand]
    private void ToggleLock()
    {
        IsLocked = !IsLocked;
    }
}
