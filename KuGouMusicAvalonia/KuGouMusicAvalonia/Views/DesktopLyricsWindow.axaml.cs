using System;
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
    private bool _placementRestored;

    public DesktopLyricsWindow()
    {
        InitializeComponent();
        DataContext = new DesktopLyricsViewModel();

        _savePlacementTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _savePlacementTimer.Tick += OnSavePlacementTick;

        PositionChanged += OnPositionChanged;
        Resized += OnResized;
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
        SavePlacement();

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
        RestorePlacement();
        IsLocked = DesktopLyricsWindowService.Instance.IsLocked;
        LyricsService.Instance.BeginWordHighlight();
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

    private void RestorePlacement()
    {
        RestoreWidth();

        if (MusicService.FloatingLyricsWindowX is { } x &&
            MusicService.FloatingLyricsWindowY is { } y &&
            TryClampToScreen(new PixelPoint(x, y), out var restored))
        {
            Position = restored;
        }
        else
        {
            PositionAtDefaultLocation();
        }

        _placementRestored = true;
    }

    private void RestoreWidth()
    {
        if (MusicService.FloatingLyricsWindowWidth is not { } width || double.IsNaN(width) || width <= 0)
        {
            return;
        }

        var minWidth = double.IsNaN(MinWidth) || MinWidth <= 0 ? 400 : MinWidth;
        var maxWidth = double.IsNaN(MaxWidth) || double.IsInfinity(MaxWidth) || MaxWidth <= 0
            ? width
            : MaxWidth;

        Width = Math.Clamp(width, minWidth, Math.Max(minWidth, maxWidth));
    }

    /// <summary>
    /// 首次打开时把窗口放到当前屏幕工作区（不含任务栏）底部居中。
    /// 尺寸取显式设置的 Width/Height 再按缩放换算成物理像素，避免布局尚未完成时用 Bounds 算出错误坐标。
    /// </summary>
    private void PositionAtDefaultLocation()
    {
        var screen = GetTargetScreen();
        if (screen is null)
        {
            PositionAtScreenBottom();
            return;
        }

        var area = screen.WorkingArea;
        var size = GetWindowPixelSize();
        var margin = (int)Math.Round(DefaultBottomMargin * screen.Scaling);

        var x = area.X + Math.Max(0, (area.Width - size.Width) / 2);
        var y = area.Y + Math.Max(0, area.Height - size.Height - margin);
        Position = new PixelPoint(x, y);
    }

    private bool TryClampToScreen(PixelPoint position, out PixelPoint result)
    {
        result = position;

        var size = GetWindowPixelSize();
        var screen = Screens.ScreenFromPoint(position)
            ?? Screens.ScreenFromBounds(new PixelRect(position, size))
            ?? GetTargetScreen();
        if (screen is null)
        {
            return false;
        }

        var area = screen.WorkingArea;
        var maxX = area.X + Math.Max(0, area.Width - size.Width);
        var maxY = area.Y + Math.Max(0, area.Height - size.Height);

        result = new PixelPoint(
            Math.Clamp(position.X, area.X, Math.Max(area.X, maxX)),
            Math.Clamp(position.Y, area.Y, Math.Max(area.Y, maxY)));
        return true;
    }

    private Screen? GetTargetScreen() => Screens.ScreenFromWindow(this) ?? Screens.Primary;

    private double GetScaling()
    {
        var scaling = RenderScaling;
        if (scaling <= 0 || double.IsNaN(scaling))
        {
            scaling = GetTargetScreen()?.Scaling ?? 1;
        }

        return scaling <= 0 || double.IsNaN(scaling) ? 1 : scaling;
    }

    private PixelSize GetWindowPixelSize()
    {
        var scaling = GetScaling();

        var width = double.IsNaN(Width) || Width <= 0 ? Bounds.Width : Width;
        var height = double.IsNaN(Height) || Height <= 0 ? Bounds.Height : Height;

        if (width <= 0)
        {
            width = double.IsNaN(MinWidth) || MinWidth <= 0 ? 400 : MinWidth;
        }

        if (height <= 0)
        {
            height = double.IsNaN(MinHeight) || MinHeight <= 0 ? 120 : MinHeight;
        }

        return new PixelSize(
            Math.Max(1, (int)Math.Round(width * scaling)),
            Math.Max(1, (int)Math.Round(height * scaling)));
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e) => SchedulePlacementSave();

    private void OnResized(object? sender, WindowResizedEventArgs e) => SchedulePlacementSave();

    private void SchedulePlacementSave()
    {
        if (!_placementRestored)
        {
            return;
        }

        _savePlacementTimer.Stop();
        _savePlacementTimer.Start();
    }

    private void OnSavePlacementTick(object? sender, EventArgs e)
    {
        _savePlacementTimer.Stop();
        SavePlacement();
    }

    private void SavePlacement()
    {
        if (!_placementRestored || WindowState != WindowState.Normal)
        {
            return;
        }

        var position = Position;
        MusicService.FloatingLyricsWindowX = position.X;
        MusicService.FloatingLyricsWindowY = position.Y;

        var width = Bounds.Width > 0 ? Bounds.Width : Width;
        if (!double.IsNaN(width) && width > 0)
        {
            MusicService.FloatingLyricsWindowWidth = width;
        }
    }
}
