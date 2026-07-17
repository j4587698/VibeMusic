using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
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
    private string _activeNavigationKey = "NavDiscover";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDesktopLayout))]
    [NotifyPropertyChangedFor(nameof(ShowCompactMiniPlayer))]
    [NotifyPropertyChangedFor(nameof(ShowAppFooter))]
    [NotifyPropertyChangedFor(nameof(ShowShellChrome))]
    [NotifyPropertyChangedFor(nameof(ShowShellHeader))]
    [NotifyPropertyChangedFor(nameof(PageBottomInset))]
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
    public NowPlayingViewModel NowPlayingPage => _nowPlayingViewModel;
    public LyricsViewModel LyricsPage => _lyricsViewModel;

    public MainViewModel()
    {
        _ = LyricsService.Instance;
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
        IsShellMenuOpen = !value;
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
