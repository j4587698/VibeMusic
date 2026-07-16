using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KuGouMusicAvalonia.ViewModels;
using System;

namespace KuGouMusicAvalonia.Views;

public partial class RankingsView : UserControl
{
    public RankingsView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateCardsPerRow();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateCardsPerRow();
    }

    private void UpdateCardsPerRow()
    {
        if (DataContext is not RankingsViewModel viewModel)
        {
            return;
        }

        var availableWidth = Math.Max(156, Bounds.Width - 24);
        var cardsPerRow = Math.Max(1, (int)((availableWidth + 8) / 164));
        viewModel.SetCardsPerRow(cardsPerRow);
    }
}
