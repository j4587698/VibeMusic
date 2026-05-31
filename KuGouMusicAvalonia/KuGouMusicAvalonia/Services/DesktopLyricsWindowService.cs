using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using KuGouMusicAvalonia.Views;

namespace KuGouMusicAvalonia.Services;

public sealed class DesktopLyricsWindowService
{
    public static DesktopLyricsWindowService Instance { get; } = new();

    private Window? _window;

    private DesktopLyricsWindowService()
    {
    }

    public bool IsSupported => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    public void ShowOrActivate()
    {
        if (!IsSupported)
        {
            return;
        }

        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        var window = new DesktopLyricsWindow();
        window.Closed += (_, _) => _window = null;
        _window = window;
        window.Show();
    }
}
