using CommunityToolkit.Mvvm.Input;
using KuGouMusicAvalonia.Services;
using System.ComponentModel;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public sealed partial class LyricsViewModel : ViewModelBase
{
    public PlayerService Player => PlayerService.Instance;

    public LyricsService Lyrics => LyricsService.Instance;

    public string HeaderTitle => Player.CurrentTitle;

    public string HeaderArtist => Player.CurrentArtist;

    public LyricsViewModel()
    {
        Player.PropertyChanged += OnPlayerPropertyChanged;
    }

    [RelayCommand]
    private async Task LoadLyricsAsync()
    {
        await Lyrics.LoadForCurrentSongAsync();
    }

    [RelayCommand]
    private void OpenNowPlaying()
    {
        ShellNavigationService.Instance.Navigate("NavNowPlaying");
    }

    [RelayCommand]
    private void TogglePlay()
    {
        Player.TogglePlayPause();
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

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerService.CurrentSong) or nameof(PlayerService.CurrentTitle) or nameof(PlayerService.CurrentArtist))
        {
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderArtist));
        }
    }

}
