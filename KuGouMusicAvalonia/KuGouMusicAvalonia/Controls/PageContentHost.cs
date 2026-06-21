using Avalonia;
using Avalonia.Controls;
using System;

namespace KuGouMusicAvalonia.Controls;

public class PageContentHost : ContentControl
{
    protected override Type StyleKeyOverride => typeof(ContentControl);

    public static readonly StyledProperty<double> BottomInsetProperty =
        AvaloniaProperty.Register<PageContentHost, double>(nameof(BottomInset));

    static PageContentHost()
    {
        BottomInsetProperty.Changed.AddClassHandler<PageContentHost>((host, args) =>
        {
            PageChrome.SetBottomInset(host, args.GetNewValue<double>());
        });
    }

    public PageContentHost()
    {
        PageChrome.SetBottomInset(this, BottomInset);
    }

    public double BottomInset
    {
        get => GetValue(BottomInsetProperty);
        set => SetValue(BottomInsetProperty, value);
    }
}
