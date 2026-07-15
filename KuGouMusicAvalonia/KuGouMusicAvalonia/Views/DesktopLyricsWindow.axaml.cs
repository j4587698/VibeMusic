using Avalonia.Input;
using LuminaUI.Controls;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class DesktopLyricsWindow : LuminaFloatingWindow
{
    public DesktopLyricsWindow()
    {
        InitializeComponent();
        DataContext = new DesktopLyricsViewModel();
        KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.StateChanged += OnServiceStateChanged;
    }

    private void OnServiceStateChanged(object? sender, System.EventArgs e)
    {
        IsLocked = KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.IsLocked;
    }

    protected override void OnClosed(System.EventArgs e)
    {
        KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.StateChanged -= OnServiceStateChanged;
        KuGouMusicAvalonia.Services.LyricsService.Instance.EndWordHighlight();
        (DataContext as DesktopLyricsViewModel)?.Cleanup();
        base.OnClosed(e);
    }

    private void OnDragWindow(object? sender, PointerPressedEventArgs e)
    {
        BeginDrag(e);
    }

    private void OnCloseWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        KuGouMusicAvalonia.Services.MusicService.FloatingLyricsOpen = false;
        Close();
    }

    private void OnWindowOpened(object? sender, System.EventArgs e)
    {
        PositionAtScreenBottom();
        IsLocked = KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.IsLocked;
        KuGouMusicAvalonia.Services.LyricsService.Instance.BeginWordHighlight();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is DesktopLyricsViewModel vm && !KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.IsLocked)
        {
            vm.IsHovered = true;
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is DesktopLyricsViewModel vm)
        {
            vm.IsHovered = false;
        }
    }
}
