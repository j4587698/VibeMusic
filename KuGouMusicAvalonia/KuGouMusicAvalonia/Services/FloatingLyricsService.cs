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
}

public sealed class FloatingLyricsService : IFloatingLyricsController
{
    public static FloatingLyricsService Instance { get; } = new();

    private IFloatingLyricsController _controller = DesktopLyricsWindowService.Instance;

    private FloatingLyricsService()
    {
        _controller.StateChanged += OnControllerStateChanged;
    }

    public bool IsSupported => _controller.IsSupported;

    public bool IsOpen => _controller.IsOpen;

    public bool IsLocked
    {
        get => _controller.IsLocked;
        set => _controller.IsLocked = value;
    }

    public bool SupportsCompactMode => _controller.SupportsCompactMode;

    public bool IsCompactMode
    {
        get => _controller.IsCompactMode;
        set => _controller.IsCompactMode = value;
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
        _controller.StateChanged += OnControllerStateChanged;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle() => _controller.Toggle();

    public void ShowOrActivate() => _controller.ShowOrActivate();

    private void OnControllerStateChanged(object? sender, EventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }
}
