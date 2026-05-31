using KuGouMusicAvalonia.Services;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class DesktopLyricsViewModel : ViewModelBase
{
    public PlayerService Player => PlayerService.Instance;

    public LyricsService Lyrics => LyricsService.Instance;
}
