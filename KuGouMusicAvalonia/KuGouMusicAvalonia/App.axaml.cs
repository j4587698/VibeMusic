using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using System.Reflection;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using LuminaUI.Services;
using KuGouMusicAvalonia.Services;
using KuGouMusicAvalonia.ViewModels;
using KuGouMusicAvalonia.Views;

namespace KuGouMusicAvalonia;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;
    private NativeMenu? _trayMenu;
    private NativeMenuItem? _trayToggleFloatingLyricsMenuItem;
    private NativeMenuItem? _trayToggleFloatingLyricsLockMenuItem;

    public App()
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RequestedThemeVariant = MusicService.ThemeMode switch
        {
            "浅色" => ThemeVariant.Light,
            "跟随系统" => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainViewModel = new MainViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };
            FloatingLyricsService.Instance.RestorePersistedState();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        TryResolveTrayMenu();
        FloatingLyricsService.Instance.StateChanged += OnFloatingLyricsStateChanged;
        UpdateTrayFloatingLyricsMenuItems();
        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(ShowMainWindow);
    }

    private void ShowMainWindow_OnClick(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(ShowMainWindow);
    }

    private void HideMainWindow_OnClick(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Hide();
            }
        });
    }

    private void ExitApp_OnClick(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });
    }

    private void TrayMenu_NeedsUpdate(object? sender, EventArgs e)
    {
        _trayMenu = sender as NativeMenu;
        ResolveTrayFloatingLyricsMenuItems();
        UpdateTrayFloatingLyricsMenuItems();
    }

    private void ToggleFloatingLyrics_OnClick(object? sender, EventArgs e)
    {
        _trayToggleFloatingLyricsMenuItem = sender as NativeMenuItem ?? _trayToggleFloatingLyricsMenuItem;
        Dispatcher.UIThread.Post(() =>
        {
            var floatingLyrics = FloatingLyricsService.Instance;
            if (!floatingLyrics.IsSupported)
            {
                UpdateTrayFloatingLyricsMenuItems();
                return;
            }

            if (MusicService.FloatingLyricsOpen || floatingLyrics.IsOpen)
            {
                MusicService.FloatingLyricsOpen = false;
                if (floatingLyrics.IsOpen)
                {
                    floatingLyrics.Toggle();
                }
            }
            else
            {
                floatingLyrics.ShowOrActivate();
            }

            UpdateTrayFloatingLyricsMenuItems();
        });
    }

    private void ToggleFloatingLyricsLock_OnClick(object? sender, EventArgs e)
    {
        _trayToggleFloatingLyricsLockMenuItem = sender as NativeMenuItem ?? _trayToggleFloatingLyricsLockMenuItem;
        Dispatcher.UIThread.Post(() =>
        {
            var floatingLyrics = FloatingLyricsService.Instance;
            if (!floatingLyrics.IsSupported)
            {
                UpdateTrayFloatingLyricsMenuItems();
                return;
            }

            floatingLyrics.IsLocked = !floatingLyrics.IsLocked;
            UpdateTrayFloatingLyricsMenuItems();
        });
    }

    private async void About_OnClick(object? sender, EventArgs e)
    {
        ShowMainWindow();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0";
            await LuminaDialogService.Instance.ShowConfirmAsync(
                window,
                "关于 VibeMusic",
                $"VibeMusic\n版本 {version}",
                confirmText: "确定",
                cancelText: "关闭");
        }
    }

    private void ShowMainWindow()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var window = desktop.MainWindow;
        if (window is not null && TryActivateWindow(window))
        {
            return;
        }

        // The previous window was closed (or never existed): recreate it,
        // reusing the existing view model so playback/app state is preserved.
        _mainViewModel ??= new MainViewModel();
        var newWindow = new MainWindow { DataContext = _mainViewModel };
        desktop.MainWindow = newWindow;
        newWindow.Show();
        newWindow.Activate();
    }

    private static bool TryActivateWindow(Window window)
    {
        try
        {
            if (!window.IsVisible)
            {
                window.Show();
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            return true;
        }
        catch (InvalidOperationException)
        {
            // Window was already closed and cannot be re-shown.
            return false;
        }
    }

    private void OnFloatingLyricsStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateTrayFloatingLyricsMenuItems);
    }

    private void UpdateTrayFloatingLyricsMenuItems()
    {
        TryResolveTrayMenu();
        ResolveTrayFloatingLyricsMenuItems();

        var floatingLyrics = FloatingLyricsService.Instance;
        var isSupported = floatingLyrics.IsSupported;
        var isOpen = MusicService.FloatingLyricsOpen || floatingLyrics.IsOpen;

        if (_trayToggleFloatingLyricsMenuItem is not null)
        {
            _trayToggleFloatingLyricsMenuItem.Header = isOpen ? "关闭悬浮歌词" : "打开悬浮歌词";
            _trayToggleFloatingLyricsMenuItem.IsEnabled = isSupported;
        }

        if (_trayToggleFloatingLyricsLockMenuItem is not null)
        {
            _trayToggleFloatingLyricsLockMenuItem.Header = floatingLyrics.IsLocked ? "解锁悬浮歌词" : "锁定悬浮歌词";
            _trayToggleFloatingLyricsLockMenuItem.IsEnabled = isSupported && isOpen;
        }
    }

    private void ResolveTrayFloatingLyricsMenuItems()
    {
        if (_trayMenu is null ||
            (_trayToggleFloatingLyricsMenuItem is not null && _trayToggleFloatingLyricsLockMenuItem is not null))
        {
            return;
        }

        foreach (var item in _trayMenu.OfType<NativeMenuItem>())
        {
            switch (item.CommandParameter?.ToString())
            {
                case "ToggleFloatingLyrics":
                    _trayToggleFloatingLyricsMenuItem = item;
                    break;
                case "ToggleFloatingLyricsLock":
                    _trayToggleFloatingLyricsLockMenuItem = item;
                    break;
            }
        }
    }

    private void TryResolveTrayMenu()
    {
        if (_trayMenu is not null)
        {
            return;
        }

        var trayIcons = TrayIcon.GetIcons(this);
        var trayIcon = trayIcons?.FirstOrDefault();
        _trayMenu = trayIcon?.Menu;
    }

    public void RestartAppUI()
    {
        _mainViewModel = new MainViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var oldWindow = desktop.MainWindow;
            var newWindow = new MainWindow { DataContext = _mainViewModel };
            desktop.MainWindow = newWindow;
            newWindow.Show();
            oldWindow?.Close();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = _mainViewModel };
        }
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        // 关库前先把去抖窗口内未落盘的播放状态/队列同步写入，避免退出丢失。
        try
        {
            PlayerService.Instance.FlushPendingState();
        }
        catch
        {
        }

        LocalMusicStore.Shutdown();
    }
}
