using Avalonia;
using Avalonia.Controls;

namespace KuGouMusicAvalonia.Views;

public partial class LyricsView : UserControl
{
    public LyricsView()
    {
        InitializeComponent();
        UpdateLayoutMode();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateLayoutMode();
        }
    }

    private void UpdateLayoutMode()
    {
        if (DesktopLyricsLayout is null || CompactLyricsLayout is null)
        {
            return;
        }

        var compact = Bounds.Width > 0 && (Bounds.Width < 760 || Bounds.Height > Bounds.Width * 1.08);
        DesktopLyricsLayout.IsVisible = !compact;
        CompactLyricsLayout.IsVisible = compact;
    }
}