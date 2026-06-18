using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using KuGouMusicAvalonia.Views;

namespace KuGouMusicAvalonia.Services;

public sealed class DesktopLyricsWindowService : IFloatingLyricsController
{
    public static DesktopLyricsWindowService Instance { get; } = new();

    private Window? _window;

    private DesktopLyricsWindowService()
    {
    }

    public bool IsSupported => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    public bool IsOpen => _window is not null;

    public bool SupportsCompactMode => true;

    private bool _isCompactMode = MusicService.FloatingLyricsCompactMode;

    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            if (_isCompactMode != value)
            {
                _isCompactMode = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? StateChanged;

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked != value)
            {
                _isLocked = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            _window?.Close();
        }
        else
        {
            ShowOrActivate();
        }
    }

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
        window.Closed += (_, _) =>
        {
            _window = null;
            _isLocked = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
        _window = window;
        window.Show();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplySettings()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
