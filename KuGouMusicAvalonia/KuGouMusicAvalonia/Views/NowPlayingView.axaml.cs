using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KuGouMusicAvalonia.Services;
using KuGouMusicAvalonia.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace KuGouMusicAvalonia.Views;

public partial class NowPlayingView : UserControl
{
    private const double SplitLayoutMinWidth = 760;
    private const double LyricLineHeight = 58;
    private static readonly TimeSpan LyricSeekPreviewIdleTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ProgrammaticLyricScrollSuppressionTime = TimeSpan.FromMilliseconds(500);
    private readonly DispatcherTimer _lyricSeekPreviewIdleTimer;
    private bool _suppressLyricPreview;
    private DateTime _suppressLyricPreviewUntilUtc = DateTime.MinValue;
    private bool _lyricsEventsAttached;
    private NowPlayingViewModel? _viewModel;

    public NowPlayingView()
    {
        AvaloniaXamlLoader.Load(this);
        _lyricSeekPreviewIdleTimer = new DispatcherTimer
        {
            Interval = LyricSeekPreviewIdleTime
        };
        _lyricSeekPreviewIdleTimer.Tick += OnLyricSeekPreviewIdleTimerTick;
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
            viewModel.IsWideLayout = width >= SplitLayoutMinWidth && width > height;
            viewModel.IsTightLandscape = width > height && height <= 520;

            // Calculate cover size based on available height
            // Reserve space for: top bar (~60), progress (~40), bottom controls (~50), margins (~40)
            var availableForContent = height - 190;
            var maxCoverSize = viewModel.IsTightLandscape ? 120 : 260;
            viewModel.CoverSize = Math.Clamp(availableForContent, 100, maxCoverSize);

            if (this.FindControl<Grid>("NowPlayingRoot") is { } root)
            {
                root.Margin = viewModel.IsTightLandscape ? new Thickness(12, 8, 12, 10) : new Thickness(20, 8, 20, 10);
                root.RowSpacing = 6;
            }
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
        LyricsService.Instance.BeginWordHighlight();
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
        LyricsService.Instance.EndWordHighlight();
        _lyricsEventsAttached = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NowPlayingViewModel.IsWideLayout) ||
            e.PropertyName == nameof(NowPlayingViewModel.IsCompactLyricsVisible) ||
            e.PropertyName == nameof(NowPlayingViewModel.IsLyricSeekPreviewVisible))
        {
            var isShowingLyricSeekPreview =
                e.PropertyName == nameof(NowPlayingViewModel.IsLyricSeekPreviewVisible) &&
                _viewModel?.IsLyricSeekPreviewVisible == true;

            if (!isShowingLyricSeekPreview)
            {
                _lyricSeekPreviewIdleTimer.Stop();
            }

            QueueScrollActiveLyricIntoView(suppressLyricPreviewScroll: !isShowingLyricSeekPreview);
        }
    }

    private void OnLyricsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsService.ActiveLineIndex))
        {
            if (_viewModel?.IsLyricSeekPreviewVisible == true)
            {
                HideLyricSeekPreviewAndResume();
                return;
            }

            QueueScrollActiveLyricIntoView();
        }
    }

    private void OnLyricsLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _lyricSeekPreviewIdleTimer.Stop();
        _viewModel?.HideLyricSeekPreview();
        QueueScrollActiveLyricIntoView();
    }

    private void OnLyricsScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || DataContext is not NowPlayingViewModel viewModel)
        {
            return;
        }

        if (IsLyricPreviewScrollSuppressed())
        {
            return;
        }

        if (Math.Abs(e.OffsetDelta.Y) < 0.5 || LyricsService.Instance.Lines.Count == 0 || scrollViewer.Viewport.Height <= 0)
        {
            return;
        }

        if (TryGetVisibleLyricsContext(viewModel, out var visibleScrollViewer, out var itemsControl) &&
            ReferenceEquals(scrollViewer, visibleScrollViewer))
        {
            UpdateLyricsViewportPadding(scrollViewer, itemsControl);
            var index = GetLyricIndexAtViewportCenter(scrollViewer, itemsControl);
            viewModel.ShowLyricSeekPreview(LyricsService.Instance.Lines[index]);
            RestartLyricSeekPreviewIdleTimer();
        }
    }

    private void OnLyricSeekPreviewIdleTimerTick(object? sender, EventArgs e)
    {
        _lyricSeekPreviewIdleTimer.Stop();
        HideLyricSeekPreviewAndResume();
    }

    private void OnCompactLyricsTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not NowPlayingViewModel viewModel ||
            viewModel.IsWideLayout ||
            !viewModel.IsCompactLyricsVisible ||
            IsTapFromInteractiveControl(e.Source))
        {
            return;
        }

        _lyricSeekPreviewIdleTimer.Stop();
        viewModel.HideLyricSeekPreview();
        viewModel.ToggleCompactLyricsCommand.Execute(null);
        e.Handled = true;
    }

    private static bool IsTapFromInteractiveControl(object? source)
    {
        if (source is Button or ScrollBar)
        {
            return true;
        }

        return source is Visual visual &&
            visual.GetVisualAncestors().Any(ancestor => ancestor is Button or ScrollBar);
    }

    private void HideLyricSeekPreviewAndResume()
    {
        if (_viewModel?.IsLyricSeekPreviewVisible == true)
        {
            _viewModel.HideLyricSeekPreview();
        }

        QueueScrollActiveLyricIntoView();
    }

    private void RestartLyricSeekPreviewIdleTimer()
    {
        _lyricSeekPreviewIdleTimer.Stop();
        _lyricSeekPreviewIdleTimer.Start();
    }

    private void QueueScrollActiveLyricIntoView()
    {
        QueueScrollActiveLyricIntoView(suppressLyricPreviewScroll: true);
    }

    private void QueueScrollActiveLyricIntoView(bool suppressLyricPreviewScroll)
    {
        if (suppressLyricPreviewScroll)
        {
            SuppressLyricPreviewScroll();
        }

        Dispatcher.UIThread.Post(ScrollActiveLyricIntoView, DispatcherPriority.Background);
    }

    private bool IsLyricPreviewScrollSuppressed()
    {
        return _suppressLyricPreview || DateTime.UtcNow < _suppressLyricPreviewUntilUtc;
    }

    private void SuppressLyricPreviewScroll()
    {
        var until = DateTime.UtcNow + ProgrammaticLyricScrollSuppressionTime;
        if (until > _suppressLyricPreviewUntilUtc)
        {
            _suppressLyricPreviewUntilUtc = until;
        }
    }

    private void ScrollActiveLyricIntoView()
    {
        if (DataContext is not NowPlayingViewModel viewModel || viewModel.IsLyricSeekPreviewVisible)
        {
            return;
        }

        var activeLineIndex = LyricsService.Instance.ActiveLineIndex;
        if (!TryGetVisibleLyricsContext(viewModel, out var scrollViewer, out var itemsControl) ||
            activeLineIndex < 0 ||
            scrollViewer.Viewport.Height <= 0 ||
            scrollViewer.Extent.Height <= 0)
        {
            return;
        }

        UpdateLyricsViewportPadding(scrollViewer, itemsControl);
        SuppressLyricPreviewScroll();
        _suppressLyricPreview = true;
        itemsControl.ScrollIntoView(activeLineIndex);
        Dispatcher.UIThread.Post(() =>
        {
            CenterLyricContainer(activeLineIndex);
            Dispatcher.UIThread.Post(() => _suppressLyricPreview = false, DispatcherPriority.Background);
        }, DispatcherPriority.Background);
    }

    private void CenterLyricContainer(int index)
    {
        if (DataContext is not NowPlayingViewModel viewModel ||
            viewModel.IsLyricSeekPreviewVisible ||
            !TryGetVisibleLyricsContext(viewModel, out var scrollViewer, out var itemsControl))
        {
            return;
        }

        UpdateLyricsViewportPadding(scrollViewer, itemsControl);
        var targetY = GetCenteredOffset(scrollViewer, itemsControl, index);
        if (targetY < 0 || Math.Abs(scrollViewer.Offset.Y - targetY) < 2)
        {
            return;
        }

        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, targetY);
    }

    private static double GetCenteredOffset(ScrollViewer scrollViewer, ItemsControl itemsControl, int index)
    {
        var container = itemsControl.ContainerFromIndex(index);
        if (container is not null)
        {
            var center = container.TranslatePoint(new Point(container.Bounds.Width / 2, container.Bounds.Height / 2), scrollViewer);
            if (center is not null)
            {
                return ClampOffset(scrollViewer, scrollViewer.Offset.Y + center.Value.Y - scrollViewer.Viewport.Height / 2);
            }
        }

        var padding = GetLyricsViewportPadding(scrollViewer);
        var estimatedY = padding + index * LyricLineHeight + LyricLineHeight / 2 - scrollViewer.Viewport.Height / 2;
        return ClampOffset(scrollViewer, estimatedY);
    }

    private static int GetLyricIndexAtViewportCenter(ScrollViewer scrollViewer, ItemsControl itemsControl)
    {
        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        var viewportCenterY = scrollViewer.Viewport.Height / 2;

        foreach (var container in itemsControl.GetRealizedContainers())
        {
            var index = itemsControl.IndexFromContainer(container);
            if (index < 0)
            {
                continue;
            }

            var center = container.TranslatePoint(new Point(container.Bounds.Width / 2, container.Bounds.Height / 2), scrollViewer);
            if (center is null)
            {
                continue;
            }

            var distance = Math.Abs(center.Value.Y - viewportCenterY);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        if (bestIndex >= 0)
        {
            return bestIndex;
        }

        var centerY = scrollViewer.Offset.Y + scrollViewer.Viewport.Height / 2;
        var padding = GetLyricsViewportPadding(scrollViewer);
        var estimatedIndex = (int)Math.Round((centerY - padding - LyricLineHeight / 2) / LyricLineHeight);
        return Math.Clamp(estimatedIndex, 0, LyricsService.Instance.Lines.Count - 1);
    }

    private static void UpdateLyricsViewportPadding(ScrollViewer scrollViewer, ItemsControl itemsControl)
    {
        var padding = GetLyricsViewportPadding(scrollViewer);
        if (Math.Abs(itemsControl.Margin.Top - padding) < 0.5 &&
            Math.Abs(itemsControl.Margin.Bottom - padding) < 0.5)
        {
            return;
        }

        itemsControl.Margin = new Thickness(0, padding, 0, padding);
    }

    private static double GetLyricsViewportPadding(ScrollViewer scrollViewer)
    {
        return Math.Max(0, (scrollViewer.Viewport.Height - LyricLineHeight) / 2);
    }

    private static double ClampOffset(ScrollViewer scrollViewer, double y)
    {
        var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        return Math.Clamp(y, 0, maxY);
    }

    private bool TryGetVisibleLyricsContext(
        NowPlayingViewModel viewModel,
        out ScrollViewer scrollViewer,
        out ItemsControl itemsControl)
    {
        if (viewModel.IsWideLayout)
        {
            scrollViewer = this.FindControl<ScrollViewer>("WideLyricsScrollViewer")!;
            itemsControl = this.FindControl<ItemsControl>("WideLyricsItemsControl")!;
            return scrollViewer is not null && itemsControl is not null;
        }

        if (viewModel.IsLyricsModeVisible)
        {
            scrollViewer = this.FindControl<ScrollViewer>("CompactLyricsScrollViewer")!;
            itemsControl = this.FindControl<ItemsControl>("CompactLyricsItemsControl")!;
            return scrollViewer is not null && itemsControl is not null;
        }

        scrollViewer = null!;
        itemsControl = null!;
        return false;
    }
}
