using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DiscoverViewModel _discoverViewModel = new();
    private readonly PlaylistsViewModel _playlistsViewModel = new();
    private readonly ArtistsViewModel _artistsViewModel = new();
    private readonly RankingsViewModel _rankingsViewModel = new();
    private readonly HistoryViewModel _historyViewModel = new();
    private readonly SearchViewModel _searchViewModel = new();
    private readonly SettingsViewModel _settingsViewModel = new();
    private readonly NowPlayingViewModel _nowPlayingViewModel = new();
    private readonly LyricsViewModel _lyricsViewModel = new();
    private string _returnNavigationKey = "NavDiscover";
    private object? _returnPage;

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
    [NotifyPropertyChangedFor(nameof(ShowCompactChrome))]
    private string _activeNavigationKey = "NavDiscover";

    [ObservableProperty]
    private object _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDesktopLayout))]
    private bool _isCompactLayout;

    [ObservableProperty]
    private bool _isQueueOpen;

    [ObservableProperty]
    private double _queuePopupWidth = 420;

    public PlayerService Player => PlayerService.Instance;

    public bool IsDesktopLayout => !IsCompactLayout;

    public bool IsDiscoverActive => ActiveNavigationKey == "NavDiscover";
    public bool IsPlaylistsActive => ActiveNavigationKey == "NavPlaylists";
    public bool IsArtistsActive => ActiveNavigationKey == "NavArtists";
    public bool IsRankingsActive => ActiveNavigationKey == "NavRankings";
    public bool IsHistoryActive => ActiveNavigationKey == "NavHistory";
    public bool IsSearchActive => ActiveNavigationKey == "NavSearch";
    public bool IsSettingsActive => ActiveNavigationKey == "NavSettings";
    public bool IsLyricsActive => ActiveNavigationKey == "NavLyrics";
    public bool ShowMiniPlayer => Player.HasSong && ActiveNavigationKey is not "NavNowPlaying" and not "NavLyrics";
    public bool ShowCompactChrome => ActiveNavigationKey is not "NavNowPlaying" and not "NavLyrics";

    public bool IsDesktopLyricsOpen => FloatingLyricsService.Instance.IsOpen;
    public bool IsDesktopLyricsSupported => FloatingLyricsService.Instance.IsSupported;

    public MainViewModel()
    {
        _ = LyricsService.Instance;
        CurrentPage = _discoverViewModel;
        Player.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PlayerService.CurrentSong) or nameof(PlayerService.HasSong))
            {
                OnPropertyChanged(nameof(ShowMiniPlayer));
            }
        };
        FloatingLyricsService.Instance.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsDesktopLyricsOpen));
            OnPropertyChanged(nameof(IsDesktopLyricsSupported));
        };
        ShellNavigationService.Instance.NavigationRequested += key => Navigate(key);
        ShellNavigationService.Instance.NowPlayingCloseRequested += CloseNowPlaying;
        ShellNavigationService.Instance.QueueToggleRequested += ToggleQueue;
        ShellNavigationService.Instance.PlaylistDetailRequested += OpenPlaylistDetail;
        ShellNavigationService.Instance.RankingDetailRequested += OpenRankingDetail;
        ShellNavigationService.Instance.ArtistDetailRequested += OpenArtistDetail;
    }

    partial void OnActiveNavigationKeyChanged(string value)
    {
        IsQueueOpen = false;
        CurrentPage = ResolveNavigationPage(value);
    }

    private object ResolveNavigationPage(string value)
    {
        return value switch
        {
            "NavDiscover" => _discoverViewModel,
            "NavPlaylists" => _playlistsViewModel,
            "NavArtists" => _artistsViewModel,
            "NavRankings" => _rankingsViewModel,
            "NavHistory" => _historyViewModel,
            "NavSearch" => _searchViewModel,
            "NavSettings" => _settingsViewModel,
            "NavNowPlaying" => _nowPlayingViewModel,
            "NavLyrics" => _lyricsViewModel,
            _ => _discoverViewModel
        };
    }

    [RelayCommand]
    private void Navigate(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            if (ActiveNavigationKey == key)
            {
                CurrentPage = ResolveNavigationPage(key);
                return;
            }

            ActiveNavigationKey = key;
        }
    }

    private void OpenPlaylistDetail(KugouPlaylist playlist)
    {
        if (playlist is null) return;
        ActiveNavigationKey = "NavPlaylists";
        CurrentPage = new PlaylistDetailViewModel(playlist);
    }

    private void OpenRankingDetail(KugouRank rank)
    {
        if (rank is null) return;
        ActiveNavigationKey = "NavRankings";
        CurrentPage = new RankingDetailViewModel(rank);
    }

    private void OpenArtistDetail(KugouArtist artist)
    {
        if (artist is null) return;
        ActiveNavigationKey = "NavArtists";
        CurrentPage = new ArtistDetailViewModel(artist);
    }

    [RelayCommand]
    private void OpenNowPlaying()
    {
        if (ActiveNavigationKey != "NavNowPlaying")
        {
            _returnNavigationKey = ActiveNavigationKey;
            _returnPage = CurrentPage;
        }

        ActiveNavigationKey = "NavNowPlaying";
    }

    private void CloseNowPlaying()
    {
        var targetKey = string.IsNullOrWhiteSpace(_returnNavigationKey) || _returnNavigationKey == "NavNowPlaying"
            ? "NavDiscover"
            : _returnNavigationKey;
        var targetPage = _returnPage ?? ResolveNavigationPage(targetKey);

        if (ActiveNavigationKey != targetKey)
        {
            ActiveNavigationKey = targetKey;
        }

        CurrentPage = targetPage;
    }

    [RelayCommand]
    private void OpenLyrics()
    {
        ActiveNavigationKey = "NavLyrics";
    }

    [RelayCommand]
    private void ToggleDesktopLyrics()
    {
        FloatingLyricsService.Instance.Toggle();
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
