using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using LuminaUI.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const double CompactMiniPlayerPageBottomInset = 96;

    private readonly DiscoverViewModel _discoverViewModel = new();
    private readonly PlaylistsViewModel _playlistsViewModel = new();
    private readonly CloudViewModel _cloudViewModel = new();
    private readonly ArtistsViewModel _artistsViewModel = new();
    private readonly RankingsViewModel _rankingsViewModel = new();
    private readonly HistoryViewModel _historyViewModel = new();
    private readonly SearchViewModel _searchViewModel = new();
    private readonly SettingsViewModel _settingsViewModel = new();
    private readonly NowPlayingViewModel _nowPlayingViewModel = new();
    private readonly LyricsViewModel _lyricsViewModel = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiscoverActive))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistsActive))]
    [NotifyPropertyChangedFor(nameof(IsArtistsActive))]
    [NotifyPropertyChangedFor(nameof(IsRankingsActive))]
    [NotifyPropertyChangedFor(nameof(IsHistoryActive))]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    [NotifyPropertyChangedFor(nameof(IsLyricsActive))]
    [NotifyPropertyChangedFor(nameof(ShowMiniPlayer))]
    [NotifyPropertyChangedFor(nameof(ShowCompactMiniPlayer))]
    [NotifyPropertyChangedFor(nameof(ShowAppFooter))]
    [NotifyPropertyChangedFor(nameof(ShowCompactChrome))]
    [NotifyPropertyChangedFor(nameof(ShowShellChrome))]
    [NotifyPropertyChangedFor(nameof(ShowShellHeader))]
    [NotifyPropertyChangedFor(nameof(PageBottomInset))]
    [NotifyPropertyChangedFor(nameof(PageContentPaddingValue))]
    [NotifyPropertyChangedFor(nameof(HeaderedPageContentPaddingValue))]
    private string _activeNavigationKey = "NavDiscover";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDesktopLayout))]
    [NotifyPropertyChangedFor(nameof(ShowCompactMiniPlayer))]
    [NotifyPropertyChangedFor(nameof(ShowAppFooter))]
    [NotifyPropertyChangedFor(nameof(ShowShellChrome))]
    [NotifyPropertyChangedFor(nameof(ShowShellHeader))]
    [NotifyPropertyChangedFor(nameof(PageBottomInset))]
    [NotifyPropertyChangedFor(nameof(PageContentPaddingValue))]
    [NotifyPropertyChangedFor(nameof(HeaderedPageContentPaddingValue))]
    [NotifyPropertyChangedFor(nameof(FooterPlacementValue))]
    private bool _isCompactLayout;

    [ObservableProperty]
    private bool _isShellMenuOpen;

    [ObservableProperty]
    private bool _isQueueOpen;

    [ObservableProperty]
    private double _queuePopupWidth = 420;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowShellHeader))]
    private bool _isInnerPageActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiscoverActive))]
    [NotifyPropertyChangedFor(nameof(IsPlaylistsActive))]
    [NotifyPropertyChangedFor(nameof(IsArtistsActive))]
    [NotifyPropertyChangedFor(nameof(IsRankingsActive))]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    private string _selectedRootNavigationKey = "NavDiscover";

    public PlayerService Player => PlayerService.Instance;

    public bool IsDesktopLayout => !IsCompactLayout;

    public bool IsDiscoverActive => SelectedRootNavigationKey == "NavDiscover";
    public bool IsPlaylistsActive => SelectedRootNavigationKey == "NavPlaylists";
    public bool IsArtistsActive => SelectedRootNavigationKey == "NavArtists";
    public bool IsRankingsActive => SelectedRootNavigationKey == "NavRankings";
    public bool IsHistoryActive => ActiveNavigationKey == "NavHistory";
    public bool IsSearchActive => SelectedRootNavigationKey == "NavSearch";
    public bool IsSettingsActive => SelectedRootNavigationKey == "NavSettings";
    public bool IsLyricsActive => ActiveNavigationKey == "NavLyrics";
    public bool ShowMiniPlayer => Player.HasSong && ActiveNavigationKey is not "NavNowPlaying" and not "NavLyrics";
    public bool ShowCompactMiniPlayer => IsCompactLayout && ShowMiniPlayer;
    public bool ShowAppFooter => IsCompactLayout || ShowMiniPlayer;
    public bool ShowCompactChrome => ActiveNavigationKey is not "NavNowPlaying" and not "NavLyrics";
    public bool ShowShellChrome => IsDesktopLayout || ShowCompactChrome;
    public bool ShowShellHeader => ShowCompactChrome && IsInnerPageActive;
    public double PageBottomInset => ShowCompactMiniPlayer ? CompactMiniPlayerPageBottomInset : 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerBarLeftWidth))]
    private double _renderScaling = 1.0;

    public GridLength PlayerBarLeftWidth => new(350.0 / RenderScaling);
    public Thickness PageContentPaddingValue => ComputePageContentPadding(isHeadered: false);
    public Thickness HeaderedPageContentPaddingValue => ComputePageContentPadding(isHeadered: true);
    public LuminaShellFooterPlacement FooterPlacementValue => IsDesktopLayout
        ? LuminaShellFooterPlacement.ContentArea
        : LuminaShellFooterPlacement.Normal;
    public NowPlayingViewModel NowPlayingPage => _nowPlayingViewModel;
    public LyricsViewModel LyricsPage => _lyricsViewModel;

    [ObservableProperty]
    private bool _isFirstLaunchDisclaimerDialogOpen;

    [ObservableProperty]
    private bool _isCreatePlaylistDialogOpen;

    [ObservableProperty]
    private string _createPlaylistName = string.Empty;

    [ObservableProperty]
    private bool _createPlaylistIsPrivate;

    [ObservableProperty]
    private bool _isSavingPlaylist;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreatePlaylistError))]
    private string _createPlaylistErrorText = string.Empty;

    public bool HasCreatePlaylistError => !string.IsNullOrWhiteSpace(CreatePlaylistErrorText);

    public MainViewModel()
    {
        _ = LyricsService.Instance;
        if (!MusicService.DisclaimerAccepted)
        {
            IsFirstLaunchDisclaimerDialogOpen = true;
        }

        Player.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PlayerService.CurrentSong) or nameof(PlayerService.HasSong))
            {
                OnPropertyChanged(nameof(ShowMiniPlayer));
                OnPropertyChanged(nameof(ShowCompactMiniPlayer));
                OnPropertyChanged(nameof(ShowAppFooter));
                OnPropertyChanged(nameof(PageBottomInset));
            }
        };
        ShellNavigationService.Instance.QueueToggleRequested += ToggleQueue;
    }

    [RelayCommand]
    private void AcceptFirstLaunchDisclaimer()
    {
        MusicService.DisclaimerAccepted = true;
        IsFirstLaunchDisclaimerDialogOpen = false;
    }

    [RelayCommand]
    private void DeclineFirstLaunchDisclaimer()
    {
        if (PlatformApplicationService.TryExitApplication())
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }

    partial void OnActiveNavigationKeyChanged(string value)
    {
        IsQueueOpen = false;
        if (IsCompactLayout)
        {
            IsShellMenuOpen = false;
        }

        if (value is "NavDiscover" or "NavPlaylists" or "NavArtists" or "NavRankings" or "NavSearch" or "NavSettings")
        {
            SelectedRootNavigationKey = value;
        }
    }

    partial void OnSelectedRootNavigationKeyChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value != ActiveNavigationKey)
        {
            Navigate(value);
        }
    }

    partial void OnIsCompactLayoutChanged(bool value)
    {
        IsShellMenuOpen = false;
    }

    public object ResolveRootNavigationPage(string value)
    {
        return value switch
        {
            "NavDiscover" => _discoverViewModel,
            "NavPlaylists" => _playlistsViewModel,
            "NavArtists" => _artistsViewModel,
            "NavRankings" => _rankingsViewModel,
            "NavSearch" => _searchViewModel,
            "NavSettings" => RefreshSettingsPage(),
            _ => _discoverViewModel
        };
    }

    public object? ResolveInnerNavigationPage(string value)
    {
        return value switch
        {
            "NavHistory" => _historyViewModel,
            "NavCloud" => RefreshCloudPage(),
            _ => null
        };
    }

    private Thickness ComputePageContentPadding(bool isHeadered)
    {
        if (ActiveNavigationKey is "NavNowPlaying" or "NavLyrics")
            return default;

        var top = IsCompactLayout
            ? (isHeadered ? 0 : 8)
            : (isHeadered ? 4 : 20);

        return new Thickness(0, top, 0, 0);
    }

    private object RefreshCloudPage()
    {
        _cloudViewModel.RefreshCommand.Execute(null);
        return _cloudViewModel;
    }

    private object RefreshSettingsPage()
    {
        _ = _settingsViewModel.ActivateAsync();
        return _settingsViewModel;
    }

    [RelayCommand]
    private void Navigate(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            ActiveNavigationKey = key;
        }
    }

    [RelayCommand]
    private void OpenNowPlaying()
    {
        ShellNavigationService.Instance.OpenNowPlaying();
    }

    [RelayCommand]
    private void OpenLyrics()
    {
        ShellNavigationService.Instance.OpenLyrics();
    }

    [RelayCommand]
    private void OpenMy()
    {
        ShellNavigationService.Instance.Navigate("NavSettings");
    }

    [RelayCommand]
    private void ToggleQueue()
    {
        IsQueueOpen = !IsQueueOpen;
    }

    [RelayCommand]
    private void CloseQueue()
    {
        IsQueueOpen = false;
    }

    [RelayCommand]
    private async Task PlayQueueSongAsync(KugouSong song)
    {
        if (PlayerService.IsSameSong(song, Player.CurrentSong))
        {
            Player.TogglePlayPause();
            return;
        }
        await Player.PlayQueueSongAsync(song);
    }

    [RelayCommand]
    private async Task RemoveQueueSongAsync(KugouSong song)
    {
        await Player.RemoveFromQueueAsync(song);
    }

    [RelayCommand]
    private async Task DownloadSongAsync(KugouSong? song)
    {
        await Player.DownloadAsync(song);
    }

    [RelayCommand]
    private void ClearQueue()
    {
        Player.ClearQueue();
    }

    [RelayCommand]
    private void OpenCreatePlaylistDialog()
    {
        if (Player.Queue.Count == 0)
        {
            ShellNavigationService.Instance.ShowToast("当前播放队列为空，无法生成歌单");
            return;
        }

        CreatePlaylistName = $"播放队列 {DateTime.Now:yyyy-MM-dd HH:mm}";
        CreatePlaylistIsPrivate = false;
        CreatePlaylistErrorText = string.Empty;
        IsCreatePlaylistDialogOpen = true;
    }

    [RelayCommand]
    private void CloseCreatePlaylistDialog()
    {
        IsCreatePlaylistDialogOpen = false;
        IsSavingPlaylist = false;
        CreatePlaylistErrorText = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmCreatePlaylistAsync()
    {
        var name = CreatePlaylistName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CreatePlaylistErrorText = "请输入歌单名称";
            return;
        }

        if (!MusicService.IsLoggedIn)
        {
            CreatePlaylistErrorText = "请先在「设置」中登录酷狗账号";
            return;
        }

        var queueSongs = Player.Queue.ToList();
        if (queueSongs.Count == 0)
        {
            CreatePlaylistErrorText = "播放队列为空";
            return;
        }

        IsSavingPlaylist = true;
        CreatePlaylistErrorText = string.Empty;

        try
        {
            var response = await MusicService.CreatePlaylistAsync(name, CreatePlaylistIsPrivate);
            if (MusicService.TryGetResponseError(response, out var errorMessage, out _, out _))
            {
                CreatePlaylistErrorText = $"创建歌单失败：{errorMessage}";
                return;
            }

            int listId = 0;
            if (!MusicService.TryParsePlaylistId(response, out listId) || listId <= 0)
            {
                await Task.Delay(300);
                var userPlaylists = await MusicService.Client.GetUserPlaylistsTypedAsync(page: 1, pageSize: 40);
                var found = userPlaylists.Items.FirstOrDefault(p => p.Name == name);
                listId = found?.Listid ?? found?.OriginalId ?? found?.Id ?? 0;
            }

            if (listId > 0 && queueSongs.Count > 0)
            {
                var addResponse = await MusicService.AddSongsToPlaylistAsync(listId, queueSongs);
                if (MusicService.TryGetResponseError(addResponse, out var addErrorMessage, out _, out _))
                {
                    CreatePlaylistErrorText = $"歌单创建成功，但添加歌曲失败：{addErrorMessage}";
                    return;
                }
            }
            else if (listId <= 0)
            {
                CreatePlaylistErrorText = "歌单已创建，但未能解析到歌单 ID，请在歌单列表中手动刷新查看";
                return;
            }

            IsCreatePlaylistDialogOpen = false;
            ShellNavigationService.Instance.ShowToast($"已成功生成歌单「{name}」，共 {queueSongs.Count} 首歌曲");

            _ = _playlistsViewModel.LoadUserPlaylistsAsync();
            _ = _settingsViewModel.RefreshUserDataAsync();
            ShellNavigationService.Instance.RefreshPlaylists();
        }
        catch (Exception ex)
        {
            CreatePlaylistErrorText = $"生成歌单出错：{ex.Message}";
        }
        finally
        {
            IsSavingPlaylist = false;
        }
    }

    [RelayCommand]
    private void TogglePlay()
    {
        Player.TogglePlayPause();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        await Player.ToggleCurrentFavoriteAsync();
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        Player.ToggleShuffle();
    }

    [RelayCommand]
    private void CyclePlayMode()
    {
        Player.CyclePlayMode();
    }

    [RelayCommand]
    private void CycleRepeatMode()
    {
        Player.CyclePlayMode();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        await Player.SkipNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        await Player.SkipPreviousAsync();
    }
}
