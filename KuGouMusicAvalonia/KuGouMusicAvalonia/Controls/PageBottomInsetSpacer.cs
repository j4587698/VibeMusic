using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace KuGouMusicAvalonia.Controls;

public class PageBottomInsetSpacer : Control
{
    public PageBottomInsetSpacer()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncHeight(PageChrome.GetBottomInset(this));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PageChrome.BottomInsetProperty)
        {
            SyncHeight(change.GetNewValue<double>());
        }
    }

    private void SyncHeight(double inset)
    {
        Height = Math.Max(0, inset);
    }
}
