using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;
using KuGou.Lite;
using KuGouMusicAvalonia.Controls;
using KuGouMusicAvalonia.Services;
using KuGouMusicAvalonia.ViewModels;
using LuminaUI.Controls;

namespace KuGouMusicAvalonia.Views;

public partial class MainView : UserControl
{
    private static readonly TimeSpan AndroidBackExitWindow = TimeSpan.FromSeconds(2);

    private static readonly HashSet<string> RootNavigationKeys = new(StringComparer.Ordinal)
    {
        "NavDiscover",
        "NavPlaylists",
        "NavArtists",
        "NavRankings",
        "NavSearch",
        "NavSettings"
    };

    private static readonly HashSet<string> InnerNavigationKeys = new(StringComparer.Ordinal)
    {
        "NavHistory",
        "NavCloud"
    };

    private MainViewModel? _viewModel;
    private bool _routesRegistered;
    private bool _syncingActiveNavigationKey;
    private bool _isStackOperationRunning;
    private bool _layoutRestructured;
    private DateTimeOffset _lastUnhandledAndroidBackRequestedAt;

    public MainView()
    {
        InitializeComponent();
        Loaded += (_, _) => RestructureLayout();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateLayoutMode();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateLayoutMode();

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _routesRegistered = false;
        AppShell.ClearRoutes();
        _viewModel = DataContext as MainViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            RegisterShellRoutes();
            NavigateRoot(_viewModel.ActiveNavigationKey, closeMenuOnNavigate: false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        AppShell.PropertyChanged += AppShell_PropertyChanged;
        AppShell.UnhandledBackRequested += OnUnhandledBackRequested;

        var navigation = ShellNavigationService.Instance;
        navigation.NavigationRequested += OnNavigationRequested;
        navigation.BackRequested += OnBackRequested;
        navigation.NowPlayingRequested += OnNowPlayingRequested;
        navigation.NowPlayingCloseRequested += OnBackRequested;
        navigation.LyricsRequested += OnLyricsRequested;
        navigation.PlaylistDetailRequested += OnPlaylistDetailRequested;
        navigation.RankingDetailRequested += OnRankingDetailRequested;
        navigation.ArtistDetailRequested += OnArtistDetailRequested;

        RegisterShellRoutes();
        if (_viewModel != null)
        {
            NavigateRoot(_viewModel.ActiveNavigationKey, closeMenuOnNavigate: false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AppShell.PropertyChanged -= AppShell_PropertyChanged;
        AppShell.UnhandledBackRequested -= OnUnhandledBackRequested;

        var navigation = ShellNavigationService.Instance;
        navigation.NavigationRequested -= OnNavigationRequested;
        navigation.BackRequested -= OnBackRequested;
        navigation.NowPlayingRequested -= OnNowPlayingRequested;
        navigation.NowPlayingCloseRequested -= OnBackRequested;
        navigation.LyricsRequested -= OnLyricsRequested;
        navigation.PlaylistDetailRequested -= OnPlaylistDetailRequested;
        navigation.RankingDetailRequested -= OnRankingDetailRequested;
        navigation.ArtistDetailRequested -= OnArtistDetailRequested;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnUnhandledBackRequested(object? sender, LuminaBackRequestedEventArgs e)
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastUnhandledAndroidBackRequestedAt <= AndroidBackExitWindow)
        {
            _lastUnhandledAndroidBackRequestedAt = default;
            e.Handled = PlatformApplicationService.TryExitApplication();
            return;
        }

        _lastUnhandledAndroidBackRequestedAt = now;
        e.Handled = true;
        AppShell.ShowToast("再按一次关闭界面", AndroidBackExitWindow);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsQueueOpen))
        {
            if (_viewModel?.IsQueueOpen == true && _viewModel.Player.CurrentSong != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    QueueListBox.ScrollIntoView(_viewModel.Player.CurrentSong);
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        if (e.PropertyName == nameof(MainViewModel.ActiveNavigationKey) &&
            !_syncingActiveNavigationKey &&
            _viewModel != null &&
            RootNavigationKeys.Contains(_viewModel.ActiveNavigationKey))
        {
            NavigateRoot(_viewModel.ActiveNavigationKey);
        }
    }

    private void RegisterShellRoutes()
    {
        if (_viewModel == null || _routesRegistered)
        {
            return;
        }

        AppShell.ClearRoutes();
        foreach (var key in RootNavigationKeys)
        {
            AppShell.RegisterRoute(key, () => CreateShellPage(key, _viewModel.ResolveRootNavigationPage(key)));
        }

        _routesRegistered = true;
    }

    private PageContentHost CreatePageHost(object viewModel)
    {
        var host = new PageContentHost
        {
            Content = viewModel
        };

        if (_viewModel != null)
        {
            host.Bind(PageContentHost.BottomInsetProperty, new Binding(nameof(MainViewModel.PageBottomInset))
            {
                Source = _viewModel
            });
        }

        return host;
    }

    private LuminaPage CreateShellPage(string navigationKey, object viewModel)
    {
        return new LuminaPage
        {
            NavigationKey = navigationKey,
            ShellTitle = ResolveShellTitle(navigationKey),
            Content = CreatePageHost(viewModel),
            Padding = default
        };
    }

    private void NavigateRoot(string navigationKey, bool closeMenuOnNavigate = true)
    {
        if (_viewModel == null || !RootNavigationKeys.Contains(navigationKey))
        {
            return;
        }

        RegisterShellRoutes();
        AppShell.NavigateTo(navigationKey, closeMenuOnNavigate);
    }

    private async void OnNavigationRequested(string navigationKey)
    {
        if (RootNavigationKeys.Contains(navigationKey))
        {
            SyncActiveNavigationKey(navigationKey);
            NavigateRoot(navigationKey);
            return;
        }

        if (InnerNavigationKeys.Contains(navigationKey))
        {
            await PushInnerPageAsync(navigationKey);
            return;
        }

        if (navigationKey == "NavNowPlaying")
        {
            OnNowPlayingRequested();
        }
        else if (navigationKey == "NavLyrics")
        {
            OnLyricsRequested();
        }
    }

    private async void OnBackRequested()
    {
        await PopShellAsync();
    }

    private async void OnNowPlayingRequested()
    {
        if (_viewModel == null || TryGetNavigationKey(AppShell.ActiveRouteContent) == "NavNowPlaying")
        {
            return;
        }

        await PushPageAsync(_viewModel.NowPlayingPage, "NavNowPlaying", fullScreen: true);
    }

    private async void OnLyricsRequested()
    {
        if (_viewModel == null || TryGetNavigationKey(AppShell.ActiveRouteContent) == "NavLyrics")
        {
            return;
        }

        await PushPageAsync(_viewModel.LyricsPage, "NavLyrics", fullScreen: true);
    }

    private async Task PushInnerPageAsync(string navigationKey)
    {
        if (_viewModel == null || TryGetNavigationKey(AppShell.ActiveRouteContent) == navigationKey)
        {
            return;
        }

        var pageViewModel = _viewModel.ResolveInnerNavigationPage(navigationKey);
        if (pageViewModel == null)
        {
            return;
        }

        await PushPageAsync(pageViewModel, navigationKey);
    }

    private async void OnPlaylistDetailRequested(KugouPlaylist playlist)
    {
        if (playlist == null)
        {
            return;
        }

        await PushPageAsync(new PlaylistDetailViewModel(playlist), "NavPlaylists");
    }

    private async void OnRankingDetailRequested(KugouRank rank)
    {
        if (rank == null)
        {
            return;
        }

        await PushPageAsync(new RankingDetailViewModel(rank), "NavRankings");
    }

    private async void OnArtistDetailRequested(KugouArtist artist)
    {
        if (artist == null)
        {
            return;
        }

        await PushPageAsync(new ArtistDetailViewModel(artist), "NavArtists");
    }

    private async Task PushPageAsync(object viewModel, string navigationKey, bool fullScreen = false)
    {
        if (_isStackOperationRunning)
        {
            return;
        }

        _isStackOperationRunning = true;
        try
        {
            var page = CreateShellPage(navigationKey, viewModel);
            await AppShell.PushAsync(page, CreatePushOptions(fullScreen));
            SyncActiveNavigationKey(navigationKey);
        }
        finally
        {
            _isStackOperationRunning = false;
        }
    }

    private async Task PopShellAsync()
    {
        if (_isStackOperationRunning)
        {
            return;
        }

        _isStackOperationRunning = true;
        try
        {
            await AppShell.PopAsync();
            var navigationKey = TryGetNavigationKey(AppShell.ActiveRouteContent);
            if (!string.IsNullOrWhiteSpace(navigationKey))
            {
                SyncActiveNavigationKey(navigationKey);
            }
        }
        finally
        {
            _isStackOperationRunning = false;
        }
    }

    private static LuminaShellPushOptions CreatePushOptions(bool fullScreen)
    {
        var options = new LuminaShellPushOptions(new CrossFade
        {
            Duration = TimeSpan.FromMilliseconds(250)
        });

        if (fullScreen)
        {
            options.ShowShellChrome = false;
            options.ShowShellHeader = false;
            options.ShowShellMenu = false;
        }

        return options;
    }

    private void AppShell_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != LuminaShell.ActiveRouteContentProperty)
        {
            return;
        }

        var navigationKey = TryGetNavigationKey(AppShell.ActiveRouteContent);
        if (!string.IsNullOrWhiteSpace(navigationKey))
        {
            SyncActiveNavigationKey(navigationKey);
        }
    }

    private void SyncActiveNavigationKey(string navigationKey)
    {
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.IsInnerPageActive = InnerNavigationKeys.Contains(navigationKey);

        if (_viewModel.ActiveNavigationKey == navigationKey)
        {
            return;
        }

        _syncingActiveNavigationKey = true;
        try
        {
            _viewModel.ActiveNavigationKey = navigationKey;
        }
        finally
        {
            _syncingActiveNavigationKey = false;
        }
    }

    private static string? TryGetNavigationKey(Control? content)
    {
        return content switch
        {
            LuminaPage page => page.NavigationKey,
            _ => null
        };
    }

    private static string? ResolveShellTitle(string navigationKey)
    {
        return navigationKey switch
        {
    
            "NavHistory" => "播放记录",
            "NavCloud" => "云盘",
            _ => null
        };
    }

    private void UpdateLayoutMode()
    {
        if (DataContext is MainViewModel viewModel)
        {
            var width = Bounds.Width;
            var height = Bounds.Height;
            var isLandscape = height > 0 && width > height;

            viewModel.IsCompactLayout = width > 0 && width < 680 && !isLandscape;
            if (width > 0)
            {
                viewModel.QueuePopupWidth = Math.Clamp(width - 32, 300, 420);
            }
        }
    }

    private void RestructureLayout()
    {
        if (_layoutRestructured) return;

        if (!TryFindVisualChild(AppShell, "PART_Footer", out Border? footer)) return;
        if (!TryFindVisualChild(AppShell, "PART_SplitView", out SplitView? splitView)) return;

        if (footer.Parent is not Panel parentPanel) return;
        if (splitView.Content is not Border contentBorder) return;
        if (contentBorder.Child is not Grid contentGrid) return;

        parentPanel.Children.Remove(footer);
        contentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var footerRow = contentGrid.RowDefinitions.Count - 1;
        Grid.SetRow(footer, footerRow);
        contentGrid.Children.Add(footer);
        _layoutRestructured = true;
    }

    private static bool TryFindVisualChild<T>(Visual root, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? result) where T : class
    {
        result = null;
        foreach (var visual in root.GetVisualDescendants())
        {
            if (visual is T typed && visual.Name == name)
            {
                result = typed;
                return true;
            }
        }
        return false;
    }
}
