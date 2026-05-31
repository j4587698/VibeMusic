using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KuGouMusicAvalonia.ViewModels;
using System.Linq;

namespace KuGouMusicAvalonia.Views;

public partial class ArtistDetailView : UserControl
{
    private ScrollViewer? _pageScrollViewer;

    public ArtistDetailView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(AttachPageScrollViewer);
        DetachedFromVisualTree += (_, _) => DetachPageScrollViewer();
    }

    private void AttachPageScrollViewer()
    {
        DetachPageScrollViewer();

        var pageList = this.FindControl<ListBox>("ArtistSongsPageList");
        _pageScrollViewer = pageList?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_pageScrollViewer is not null)
        {
            _pageScrollViewer.ScrollChanged += OnArtistSongsScrollChanged;
        }
    }

    private void DetachPageScrollViewer()
    {
        if (_pageScrollViewer is not null)
        {
            _pageScrollViewer.ScrollChanged -= OnArtistSongsScrollChanged;
            _pageScrollViewer = null;
        }
    }

    private void OnArtistSongsScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || DataContext is not ArtistDetailViewModel viewModel)
        {
            return;
        }

        var remaining = scrollViewer.Extent.Height - scrollViewer.Viewport.Height - scrollViewer.Offset.Y;
        if (remaining > 640 || !viewModel.CanLoadMore)
        {
            return;
        }

        viewModel.LoadMoreSongsCommand.Execute(null);
    }
}