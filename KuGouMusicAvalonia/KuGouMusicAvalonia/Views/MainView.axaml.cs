using System;
using Avalonia;
using Avalonia.Controls;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateLayoutMode();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsCompactLayout = Bounds.Width > 0 && Bounds.Width < 760;
            if (Bounds.Width > 0)
            {
                viewModel.QueuePopupWidth = Math.Clamp(Bounds.Width - 32, 320, 420);
            }
        }
    }
}