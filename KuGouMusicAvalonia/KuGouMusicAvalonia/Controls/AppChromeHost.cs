using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using LuminaUI.Controls;

namespace KuGouMusicAvalonia.Controls;

public sealed class AppChromeHost : ContentControl
{
    protected override Type StyleKeyOverride => typeof(ContentControl);

    public static readonly StyledProperty<IBrush?> BottomSafeAreaBackgroundProperty =
        AvaloniaProperty.Register<AppChromeHost, IBrush?>(nameof(BottomSafeAreaBackground));

    public static readonly DirectProperty<AppChromeHost, Thickness> SafeAreaPaddingProperty =
        AvaloniaProperty.RegisterDirect<AppChromeHost, Thickness>(
            nameof(SafeAreaPadding),
            host => host.SafeAreaPadding);

    private Thickness _safeAreaPadding;

    static AppChromeHost()
    {
        AffectsRender<AppChromeHost>(BottomSafeAreaBackgroundProperty, SafeAreaPaddingProperty);
    }

    public IBrush? BottomSafeAreaBackground
    {
        get => GetValue(BottomSafeAreaBackgroundProperty);
        set => SetValue(BottomSafeAreaBackgroundProperty, value);
    }

    public Thickness SafeAreaPadding
    {
        get => _safeAreaPadding;
        private set
        {
            if (SetAndRaise(SafeAreaPaddingProperty, ref _safeAreaPadding, value))
            {
                Padding = value;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSafeAreaPadding();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (BottomSafeAreaBackground is not { } background || SafeAreaPadding.Bottom <= 0)
        {
            return;
        }

        context.FillRectangle(
            background,
            new Rect(0, Bounds.Height - SafeAreaPadding.Bottom, Bounds.Width, SafeAreaPadding.Bottom));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LuminaInsets.SafeAreaPaddingProperty)
        {
            UpdateSafeAreaPadding();
        }
    }

    private void UpdateSafeAreaPadding()
    {
        SafeAreaPadding = LuminaInsets.GetSafeAreaPadding(this);
    }
}
