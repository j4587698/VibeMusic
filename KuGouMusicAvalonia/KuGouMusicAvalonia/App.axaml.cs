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
}
