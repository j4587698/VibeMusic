using Avalonia;
using Avalonia.Controls;
using KuGouMusicAvalonia.Services;

namespace KuGouMusicAvalonia.Views;

public partial class LyricsView : UserControl
{
    public LyricsView()
    {
        InitializeComponent();
        UpdateLayoutMode();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LyricsService.Instance.BeginWordHighlight();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LyricsService.Instance.EndWordHighlight();
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