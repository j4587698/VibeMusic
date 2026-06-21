using Avalonia;
using Avalonia.Controls;

namespace KuGouMusicAvalonia.Controls;

public static class PageChrome
{
    public static readonly AttachedProperty<double> BottomInsetProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "BottomInset",
            typeof(PageChrome),
            defaultValue: 0,
            inherits: true);

    public static double GetBottomInset(Control element) => element.GetValue(BottomInsetProperty);

    public static void SetBottomInset(Control element, double value) => element.SetValue(BottomInsetProperty, value);
}
