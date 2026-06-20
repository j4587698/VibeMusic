using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KuGouMusicAvalonia.ViewModels;
using System;
using System.Linq;

namespace KuGouMusicAvalonia.Views;

public partial class ArtistsView : UserControl
{
    private ScrollViewer? _pageScrollViewer;

    public ArtistsView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += OnSizeChanged;
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(AttachPageScrollViewer);
        DetachedFromVisualTree += (_, _) => DetachPageScrollViewer();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateCardsPerRow();
    }

    private void AttachPageScrollViewer()
    {
        DetachPageScrollViewer();

        var pageList = this.FindControl<ListBox>("ArtistsPageList");
        _pageScrollViewer = pageList?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_pageScrollViewer is not null)
        {
            _pageScrollViewer.ScrollChanged += OnArtistsScrollChanged;
        }

        UpdateCardsPerRow();
    }

    private void DetachPageScrollViewer()
    {
        if (_pageScrollViewer is not null)
        {
            _pageScrollViewer.ScrollChanged -= OnArtistsScrollChanged;
            _pageScrollViewer = null;
        }
    }

    private void UpdateCardsPerRow()
    {
        if (DataContext is not ArtistsViewModel viewModel)
        {
            return;
        }

        var availableWidth = Math.Max(144, Bounds.Width - 24);
        var cardsPerRow = Math.Max(1, (int)((availableWidth + 10) / 154));
        viewModel.SetCardsPerRow(cardsPerRow);
    }

    private void OnArtistsScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || DataContext is not ArtistsViewModel viewModel)
        {
            return;
        }

        var remaining = scrollViewer.Extent.Height - scrollViewer.Viewport.Height - scrollViewer.Offset.Y;
        if (remaining > 520 || !viewModel.CanLoadMore)
        {
            return;
        }

        viewModel.LoadMoreCommand.Execute(null);
    }
}
