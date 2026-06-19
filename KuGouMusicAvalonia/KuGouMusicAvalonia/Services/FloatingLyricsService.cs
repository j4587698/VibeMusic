using System;

namespace KuGouMusicAvalonia.Services;

public interface IFloatingLyricsController
{
    bool IsSupported { get; }

    bool IsOpen { get; }

    bool IsLocked { get; set; }

    bool SupportsCompactMode { get; }

    bool IsCompactMode { get; set; }

    event EventHandler? StateChanged;

    void Toggle();

    void ShowOrActivate();

    void ApplySettings();
}

public sealed class FloatingLyricsService : IFloatingLyricsController
{
    public static FloatingLyricsService Instance { get; } = new();
    public static double DefaultFontSize => OperatingSystem.IsAndroid() ? 22 : 36;
    public static double MinFontSize => OperatingSystem.IsAndroid() ? 14 : 24;
    public static double MaxFontSize => OperatingSystem.IsAndroid() ? 32 : 56;

    private IFloatingLyricsController _controller = DesktopLyricsWindowService.Instance;
    private bool _isRestoringPersistedState;

    private FloatingLyricsService()
    {
        ApplyPersistedControllerSettings();
        _controller.StateChanged += OnControllerStateChanged;
    }

    public bool IsSupported => _controller.IsSupported;

    public bool IsOpen => _controller.IsOpen;

    public bool IsLocked
    {
        get => _controller.IsLocked;
        set
        {
            if (_controller.IsLocked == value)
            {
                return;
            }

            MusicService.FloatingLyricsLocked = value;
            _controller.IsLocked = value;
        }
    }

    public bool SupportsCompactMode => _controller.SupportsCompactMode;

    public bool IsCompactMode
    {
        get => _controller.IsCompactMode;
        set
        {
            if (!_controller.SupportsCompactMode || _controller.IsCompactMode == value)
            {
                return;
            }

            MusicService.FloatingLyricsCompactMode = value;
            _controller.IsCompactMode = value;
        }
    }

    public double FontSize
    {
        get => MusicService.FloatingLyricsFontSize;
        set
        {
            var normalized = Math.Clamp(value, MinFontSize, MaxFontSize);
            if (Math.Abs(MusicService.FloatingLyricsFontSize - normalized) < 0.001)
            {
                return;
            }

            MusicService.FloatingLyricsFontSize = normalized;
            _controller.ApplySettings();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? StateChanged;

    public void RegisterController(IFloatingLyricsController controller)
    {
        if (ReferenceEquals(_controller, controller))
        {
            return;
        }

        _controller.StateChanged -= OnControllerStateChanged;
        _controller = controller;
        ApplyPersistedControllerSettings();
        _controller.ApplySettings();
        _controller.StateChanged += OnControllerStateChanged;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle()
    {
        MusicService.FloatingLyricsOpen = !_controller.IsOpen;
        _controller.Toggle();
    }

    public void ShowOrActivate()
    {
        MusicService.FloatingLyricsOpen = true;
        _controller.ShowOrActivate();
    }

    public void ApplySettings() => _controller.ApplySettings();

    public void RestorePersistedState()
    {
        _isRestoringPersistedState = true;
        try
        {
            ApplyPersistedControllerSettings();
            _controller.ApplySettings();

            if (MusicService.FloatingLyricsOpen && _controller.IsSupported && !_controller.IsOpen)
            {
                _controller.ShowOrActivate();
            }
        }
        finally
        {
            _isRestoringPersistedState = false;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyPersistedControllerSettings()
    {
        if (_controller.SupportsCompactMode)
        {
            _controller.IsCompactMode = MusicService.FloatingLyricsCompactMode;
        }

        _controller.IsLocked = MusicService.FloatingLyricsLocked;
    }

    private void OnControllerStateChanged(object? sender, EventArgs e)
    {
        if (!_isRestoringPersistedState)
        {
            MusicService.FloatingLyricsLocked = _controller.IsLocked;
        }

        StateChanged?.Invoke(this, e);
    }
}
