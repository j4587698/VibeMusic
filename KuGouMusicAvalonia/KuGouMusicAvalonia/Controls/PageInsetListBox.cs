using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System;

namespace KuGouMusicAvalonia.Controls;

public class PageInsetListBox : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    public static readonly StyledProperty<Thickness> BasePaddingProperty =
        AvaloniaProperty.Register<PageInsetListBox, Thickness>(nameof(BasePadding));

    static PageInsetListBox()
    {
        BasePaddingProperty.Changed.AddClassHandler<PageInsetListBox>((listBox, _) =>
        {
            listBox.ApplyBottomInset();
        });
    }

    public Thickness BasePadding
    {
        get => GetValue(BasePaddingProperty);
        set => SetValue(BasePaddingProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyBottomInset();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PageChrome.BottomInsetProperty)
        {
            ApplyBottomInset();
        }
    }

    private void ApplyBottomInset()
    {
        var bottomInset = PageChrome.GetBottomInset(this);
        Padding = new Thickness(BasePadding.Left, BasePadding.Top, BasePadding.Right, BasePadding.Bottom + bottomInset);
    }
}
