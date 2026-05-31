using Avalonia;
using Avalonia.Controls;
using KuGouMusicAvalonia.ViewModels;
using System;

namespace KuGouMusicAvalonia.Views;

public partial class PlaylistsView : UserControl
{
    public PlaylistsView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        AttachedToVisualTree += (_, _) => UpdateCardsPerRow();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateCardsPerRow();
    }

    private void UpdateCardsPerRow()
    {
        if (DataContext is not PlaylistsViewModel viewModel)
        {
            return;
        }

        var availableWidth = Math.Max(150, Bounds.Width - 40);
        var cardsPerRow = Math.Max(1, (int)((availableWidth + 10) / 160));
        viewModel.SetPlaylistCardsPerRow(cardsPerRow);
    }
}
