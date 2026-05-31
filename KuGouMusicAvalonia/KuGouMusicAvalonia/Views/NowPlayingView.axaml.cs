using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KuGouMusicAvalonia.Services;
using KuGouMusicAvalonia.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;

namespace KuGouMusicAvalonia.Views;

public partial class NowPlayingView : UserControl
{
    private const double LyricLineHeight = 56;
    private bool _suppressLyricPreview;
    private bool _lyricsEventsAttached;
    private NowPlayingViewModel? _viewModel;

    public NowPlayingView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += OnSizeChanged;
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => AttachLyricsEvents();
        DetachedFromVisualTree += (_, _) => DetachLyricsEvents();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width, e.NewSize.Height);
        QueueScrollActiveLyricIntoView();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as NowPlayingViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateLayoutMode(Bounds.Width, Bounds.Height);
        QueueScrollActiveLyricIntoView();
    }

    private void UpdateLayoutMode(double width, double height)
    {
        if (DataContext is NowPlayingViewModel viewModel)
        {
            viewModel.IsWideLayout = width >= 860 && width > height;
        }
    }

    private void AttachLyricsEvents()
    {
        if (_lyricsEventsAttached)
        {
            return;
        }

        LyricsService.Instance.PropertyChanged += OnLyricsPropertyChanged;
        LyricsService.Instance.Lines.CollectionChanged += OnLyricsLinesChanged;
        _lyricsEventsAttached = true;
    }

    private void DetachLyricsEvents()
    {
        if (!_lyricsEventsAttached)
        {
            return;
        }

        LyricsService.Instance.PropertyChanged -= OnLyricsPropertyChanged;
        LyricsService.Instance.Lines.CollectionChanged -= OnLyricsLinesChanged;
        _lyricsEventsAttached = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NowPlayingViewModel.IsWideLayout) ||
            e.PropertyName == nameof(NowPlayingViewModel.IsCompactLyricsVisible) ||
            e.PropertyName == nameof(NowPlayingViewModel.IsLyricSeekPreviewVisible))
        {
            QueueScrollActiveLyricIntoView();
        }
    }

    private void OnLyricsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsService.ActiveLineIndex))
        {
            QueueScrollActiveLyricIntoView();
        }
    }

    private void OnLyricsLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _viewModel?.HideLyricSeekPreview();
        QueueScrollActiveLyricIntoView();
    }

    private void OnLyricsScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_suppressLyricPreview || sender is not ScrollViewer scrollViewer || DataContext is not NowPlayingViewModel viewModel)
        {
            return;
        }

        if (Math.Abs(e.OffsetDelta.Y) < 0.5 || LyricsService.Instance.Lines.Count == 0 || scrollViewer.Viewport.Height <= 0)
        {
            return;
        }

        var centerY = scrollViewer.Offset.Y + scrollViewer.Viewport.Height / 2;
        var index = (int)Math.Round((centerY - LyricLineHeight / 2) / LyricLineHeight);
        index = Math.Clamp(index, 0, LyricsService.Instance.Lines.Count - 1);
        viewModel.ShowLyricSeekPreview(LyricsService.Instance.Lines[index]);
    }

    private void QueueScrollActiveLyricIntoView()
    {
        Dispatcher.UIThread.Post(ScrollActiveLyricIntoView, DispatcherPriority.Background);
    }

    private void ScrollActiveLyricIntoView()
    {
        if (DataContext is not NowPlayingViewModel viewModel || viewModel.IsLyricSeekPreviewVisible)
        {
            return;
        }

        var scrollViewer = GetVisibleLyricsScrollViewer(viewModel);
        var activeLineIndex = LyricsService.Instance.ActiveLineIndex;
        if (scrollViewer is null || activeLineIndex < 0 || scrollViewer.Viewport.Height <= 0 || scrollViewer.Extent.Height <= 0)
        {
            return;
        }

        var targetY = activeLineIndex * LyricLineHeight - (scrollViewer.Viewport.Height - LyricLineHeight) / 2;
        var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        targetY = Math.Clamp(targetY, 0, maxY);
        if (Math.Abs(scrollViewer.Offset.Y - targetY) < 4)
        {
            return;
        }

        _suppressLyricPreview = true;
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, targetY);
        Dispatcher.UIThread.Post(() => _suppressLyricPreview = false, DispatcherPriority.Background);
    }

    private ScrollViewer? GetVisibleLyricsScrollViewer(NowPlayingViewModel viewModel)
    {
        if (viewModel.IsWideLayout)
        {
            return this.FindControl<ScrollViewer>("WideLyricsScrollViewer");
        }

        return viewModel.IsLyricsModeVisible ? this.FindControl<ScrollViewer>("CompactLyricsScrollViewer") : null;
    }
}