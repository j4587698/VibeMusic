using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using LuminaUI.Controls;
using KuGouMusicAvalonia.Services;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class DesktopLyricsWindow : LuminaFloatingWindow
{
    private const double DefaultBottomMargin = 48;

    private readonly DispatcherTimer _savePlacementTimer;
    private bool _isLoadedAndRestored;

    public DesktopLyricsWindow()
    {
        InitializeComponent();
        DataContext = new DesktopLyricsViewModel();

        _savePlacementTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _savePlacementTimer.Tick += OnSavePlacementTick;

        DesktopLyricsWindowService.Instance.StateChanged += OnServiceStateChanged;
    }

    private void OnServiceStateChanged(object? sender, EventArgs e)
    {
        IsLocked = DesktopLyricsWindowService.Instance.IsLocked;
    }

    protected override void OnClosed(EventArgs e)
    {
        _savePlacementTimer.Stop();
        _savePlacementTimer.Tick -= OnSavePlacementTick;
        PositionChanged -= OnPositionChanged;
        Resized -= OnResized;

        DesktopLyricsWindowService.Instance.StateChanged -= OnServiceStateChanged;
        LyricsService.Instance.EndWordHighlight();
        (DataContext as DesktopLyricsViewModel)?.Cleanup();
        base.OnClosed(e);
    }

    private void OnDragWindow(object? sender, PointerPressedEventArgs e)
    {
        BeginDrag(e);
    }

    private void OnCloseWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MusicService.FloatingLyricsOpen = false;
        Close();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        IsLocked = DesktopLyricsWindowService.Instance.IsLocked;
        LyricsService.Instance.BeginWordHighlight();

        // 1. 恢复宽度
        RestoreWidth();

        // 2. 直接读取数据库中保存的位置并精准放上去
        if (MusicService.FloatingLyricsWindowX is { } x &&
            MusicService.FloatingLyricsWindowY is { } y)
        {
            // 数据库有历史记录：直接原样放置在保存的坐标上
            Position = new PixelPoint(x, y);
        }
        else
        {
            // 首次打开无历史记录：放置在当前屏幕工作区底部居中
            PositionAtDefaultLocation();
        }

        // 3. 延迟挂载用户移动与尺寸变化监听，确保后续只有用户手动拖动时才保存
        Dispatcher.UIThread.Post(() =>
        {
            _isLoadedAndRestored = true;
            PositionChanged += OnPositionChanged;
            Resized += OnResized;
        }, DispatcherPriority.Loaded);
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is DesktopLyricsViewModel vm && !DesktopLyricsWindowService.Instance.IsLocked)
        {
            vm.IsHovered = true;
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is DesktopLyricsViewModel vm)
        {
            vm.IsHovered = false;
        }
    }

    private void RestoreWidth()
    {
        if (MusicService.FloatingLyricsWindowWidth is { } width && !double.IsNaN(width) && width > 0)
        {
            Width = Math.Clamp(width, 400, 3840);
        }
    }

    /// <summary>
    /// 仅在首次打开（无历史记录）时计算默认位置：屏幕工作区底部居中。
    /// </summary>
    private void PositionAtDefaultLocation()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var scaling = screen.Scaling > 0 ? screen.Scaling : (RenderScaling > 0 ? RenderScaling : 1.0);
        var width = (double.IsNaN(Width) || Width <= 0 ? 800 : Width) * scaling;
        var height = ((DataContext as DesktopLyricsViewModel)?.IsCompactMode == true ? 92 : 160) * scaling;
        var margin = DefaultBottomMargin * scaling;

        var x = (int)Math.Round(area.X + Math.Max(0, (area.Width - width) / 2));
        var y = (int)Math.Round(area.Y + Math.Max(0, area.Height - height - margin));
        Position = new PixelPoint(x, y);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (!_isLoadedAndRestored) return;
        _savePlacementTimer.Stop();
        _savePlacementTimer.Start();
    }

    private void OnResized(object? sender, WindowResizedEventArgs e)
    {
        if (!_isLoadedAndRestored) return;
        _savePlacementTimer.Stop();
        _savePlacementTimer.Start();
    }

    private void OnSavePlacementTick(object? sender, EventArgs e)
    {
        _savePlacementTimer.Stop();
        if (!_isLoadedAndRestored || WindowState != WindowState.Normal) return;

        // 用户拖动停下后，直接把当前坐标写入数据库
        var pos = Position;
        MusicService.FloatingLyricsWindowX = pos.X;
        MusicService.FloatingLyricsWindowY = pos.Y;

        var width = Bounds.Width > 0 ? Bounds.Width : Width;
        if (!double.IsNaN(width) && width > 0)
        {
            MusicService.FloatingLyricsWindowWidth = width;
        }
    }
}
