using Avalonia.Controls;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => UpdateLayoutMode();
        SizeChanged += (_, _) => UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.IsCompactLayout = Bounds.Width > 0 && Bounds.Width < 620;
            if (Bounds.Width > 0)
            {
                viewModel.LoginDialogWidth = System.Math.Clamp(Bounds.Width - 36, 320, 460);
            }
        }
    }
}
