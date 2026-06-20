using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using KuGouMusicAvalonia.ViewModels;
using System;
using System.Linq;

namespace KuGouMusicAvalonia.Views;

public partial class PlaylistsView : UserControl
{
    private ScrollViewer? _pageScrollViewer;

    public PlaylistsView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(AttachPageScrollViewer);
        DetachedFromVisualTree += (_, _) => DetachPageScrollViewer();
    }

    private void AttachPageScrollViewer()
    {
        DetachPageScrollViewer();

        var pageList = this.FindControl<ListBox>("PlaylistsPageList");
        _pageScrollViewer = pageList?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_pageScrollViewer is not null)
        {
            _pageScrollViewer.ScrollChanged += OnPlaylistsScrollChanged;
        }

        UpdateCardsPerRow();
    }

    private void DetachPageScrollViewer()
    {
        if (_pageScrollViewer is not null)
        {
            _pageScrollViewer.ScrollChanged -= OnPlaylistsScrollChanged;
            _pageScrollViewer = null;
        }
    }

    private void OnPlaylistsScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || DataContext is not PlaylistsViewModel viewModel)
        {
            return;
        }

        var remaining = scrollViewer.Extent.Height - scrollViewer.Viewport.Height - scrollViewer.Offset.Y;
        if (remaining > 520 || !viewModel.CanLoadMore)
        {
            return;
        }

        if (viewModel.LoadMoreCommand.CanExecute(null))
        {
            viewModel.LoadMoreCommand.Execute(null);
        }
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

        var availableWidth = Math.Max(144, Bounds.Width - 24);
        var cardsPerRow = Math.Max(1, (int)((availableWidth + 10) / 154));
        viewModel.SetPlaylistCardsPerRow(cardsPerRow);
    }
}
