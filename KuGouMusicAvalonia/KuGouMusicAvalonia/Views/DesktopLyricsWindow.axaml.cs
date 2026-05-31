using Avalonia.Controls;
using Avalonia.Input;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class DesktopLyricsWindow : Window
{
    public DesktopLyricsWindow()
    {
        InitializeComponent();
        DataContext = new DesktopLyricsViewModel();
    }

    private void OnDragWindow(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
