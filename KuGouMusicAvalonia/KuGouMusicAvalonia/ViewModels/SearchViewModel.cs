using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using SimpleAudioPlayer;
using SimpleAudioPlayer.Enums;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public enum SearchType
{
    Song,
    Playlist,
    Artist
}

public sealed class SearchHeaderSection
{
    public static SearchHeaderSection Instance { get; } = new();

    private SearchHeaderSection()
    {
    }
}

public partial class SearchViewModel : ViewModelBase
{
    private const uint AudioMatchSampleRate = 8000;
    private const int AudioMatchMinimumSeconds = 2;

    [ObservableProperty]
    private string _keyword = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<object> _searchResults = new();

    public ObservableCollection<object> PageItems { get; } = new();

    [ObservableProperty]
    private SearchType _currentSearchType = SearchType.Song;

    [ObservableProperty]
    private ObservableCollection<string> _hotKeywords = new()
    {
        "周杰伦",
        "新歌速递",
        "说唱巅峰",
        "深夜电台",
        "Eason Chan",
        "冥想空间"
    };

    [ObservableProperty]
    private string _statusMessage = "输入关键词开始搜索";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAudioMatchStatus))]
    [NotifyPropertyChangedFor(nameof(IsAudioMatching))]
    private string _audioMatchStatus = string.Empty;

    [ObservableProperty]
    private bool _isAudioMatching;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isAudioMatchUIVisible;

    public bool HasAudioMatchStatus => !string.IsNullOrWhiteSpace(AudioMatchStatus);

    private AudioRecorder? _recorder;
    private MemoryStream? _recordStream;

    [ObservableProperty]
    private ObservableCollection<string> _searchHistories = new();

    public SearchViewModel()
    {
        var state = AppStateStore.Load();
        if (state.SearchHistories != null)
        {
            foreach (var h in state.SearchHistories)
            {
                SearchHistories.Add(h);
            }
        }
        RebuildPageItems();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Keyword)) return;
        
        if (IsLoading) return;
        IsLoading = true;

        var trimmed = Keyword.Trim();
        if (SearchHistories.Contains(trimmed))
        {
            SearchHistories.Remove(trimmed);
        }
        SearchHistories.Insert(0, trimmed);
        if (SearchHistories.Count > 15)
        {
            SearchHistories.RemoveAt(SearchHistories.Count - 1);
        }
        var state = AppStateStore.Load();
        state.SearchHistories = SearchHistories.ToList();
        AppStateStore.Save(state);

        try
        {
            SearchResults.Clear();
            RebuildPageItems();

            if (CurrentSearchType == SearchType.Song)
            {
                var result = await MusicService.Client.SearchSongsTypedAsync(Keyword, page: 1, pageSize: 60);
                if (result?.Items != null) SearchResults = new ObservableCollection<object>(result.Items);
            }
            else if (CurrentSearchType == SearchType.Playlist)
            {
                var result = await MusicService.Client.SearchPlaylistsTypedAsync(Keyword, page: 1, pageSize: 60);
                if (result?.Items != null) SearchResults = new ObservableCollection<object>(result.Items);
            }
            else if (CurrentSearchType == SearchType.Artist)
            {
                var result = await MusicService.Client.SearchArtistsTypedAsync(Keyword, page: 1, pageSize: 60);
                if (result?.Items != null) SearchResults = new ObservableCollection<object>(result.Items);
            }

            RebuildPageItems();

            if (SearchResults.Count == 0)
            {
                StatusMessage = "没有找到相关内容";
                return;
            }

            StatusMessage = $"找到 {SearchResults.Count} 个结果";
        }
        catch (System.Exception ex)
        {
            SearchResults.Clear();
            RebuildPageItems();
            StatusMessage = $"搜索失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlaySongAsync(KugouSong song)
    {
        if (song == null) return;
        if (PlayerService.IsSameSong(song, PlayerService.Instance.CurrentSong))
        {
            PlayerService.Instance.TogglePlayPause();
            return;
        }
        var songs = SearchResults.OfType<KugouSong>().ToList();
        var index = songs.IndexOf(song);
        await PlayerService.Instance.PlayQueueAsync(songs, index < 0 ? 0 : index, $"搜索：{Keyword}", replaceQueue: true);
    }

    [RelayCommand]
    private async Task DownloadSongAsync(KugouSong song)
    {
        await PlayerService.Instance.DownloadAsync(song);
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        var songs = SearchResults.OfType<KugouSong>().ToList();
        if (songs.Count == 0) return;
        await PlayerService.Instance.PlayQueueAsync(songs, 0, $"搜索：{Keyword}", replaceQueue: true);
    }

    [RelayCommand]
    private void QueueAll()
    {
        var songs = SearchResults.OfType<KugouSong>().ToList();
        if (songs.Count == 0) return;
        var added = PlayerService.Instance.AppendToQueue(songs, $"搜索：{Keyword}");
        StatusMessage = added > 0 ? $"已加入 {added} 首到播放队列" : "这些歌曲已在播放队列中";
    }

    [RelayCommand]
    private async Task UseKeywordAsync(string keyword)
    {
        Keyword = keyword;
        await SearchAsync();
    }

    public bool IsSongSearch => CurrentSearchType == SearchType.Song;

    [RelayCommand]
    private void OpenAudioMatchUI()
    {
        AudioMatchStatus = "长按麦克风进行录音";
        IsAudioMatching = false;
        IsRecording = false;
        IsAudioMatchUIVisible = true;
    }

    [RelayCommand]
    private void CloseAudioMatchUI()
    {
        IsAudioMatchUIVisible = false;
        if (IsRecording)
        {
            try
            {
                _recorder?.Stop();
                _recorder?.Dispose();
            }
            catch { }
            IsRecording = false;
        }
        _recorder = null;
        _recordStream?.Dispose();
        _recordStream = null;
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        if (IsRecording || IsAudioMatching) return;

        try
        {
            _recordStream = new MemoryStream();
            _recorder = new AudioRecorder(SampleFormat.S16, channels: 1, sampleRate: AudioMatchSampleRate);

            if (!_recorder.Start(_recordStream, RecordingFileFormat.Pcm))
            {
                AudioMatchStatus = "录音启动失败，请检查麦克风权限";
                _recorder.Dispose();
                _recorder = null;
                _recordStream.Dispose();
                _recordStream = null;
                return;
            }

            IsRecording = true;
            AudioMatchStatus = "正在倾听...松开以识别";

            // Fallback timeout just in case it doesn't stop naturally
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(15));
                if (IsRecording)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(StopRecordingAndMatchAsync);
                }
            });
        }
        catch (Exception ex)
        {
            AudioMatchStatus = $"录音启动失败：{ex.Message}";
            IsRecording = false;
            _recorder?.Dispose();
            _recorder = null;
            _recordStream?.Dispose();
            _recordStream = null;
        }
    }

    [RelayCommand]
    private async Task StopRecordingAndMatchAsync()
    {
        if (!IsRecording) return;
        IsRecording = false;

        try
        {
            var stopped = _recorder?.Stop() == true;
            var capturedFrames = _recorder?.CapturedFrames ?? 0;
            _recorder?.Dispose();

            if (!stopped)
            {
                AudioMatchStatus = "录音停止失败，请重试";
                return;
            }

            if (capturedFrames < AudioMatchSampleRate * AudioMatchMinimumSeconds)
            {
                AudioMatchStatus = $"录音时间太短，请至少录制 {AudioMatchMinimumSeconds} 秒";
                return;
            }

            IsAudioMatching = true;
            AudioMatchStatus = "正在识别音频...";

            _recordStream!.Position = 0;
            var audioBytes = _recordStream.ToArray();

            var result = await MusicService.Client.AudioMatchTypedAsync(audioBytes);
            if (KugouLiteClient.IsAudioMatchNoResult(result.Raw))
            {
                AudioMatchStatus = "未能识别出歌曲，请靠近音源并延长录音时间";
            }
            else if (MusicService.TryGetResponseError(result.Raw, out var errorMessage))
            {
                AudioMatchStatus = $"识别失败：{errorMessage}";
            }
            else if (result.Items.Count > 0)
            {
                AudioMatchStatus = $"识别到 {result.Items.Count} 首歌曲";
                SearchResults = new ObservableCollection<object>(result.Items.Cast<object>());
                RebuildPageItems();

                // Delay a little before closing so user sees the message
                await Task.Delay(1000);
                IsAudioMatchUIVisible = false;
            }
            else
            {
                AudioMatchStatus = "未能识别出歌曲，请靠近音源并延长录音时间";
            }
        }
        catch (Exception ex)
        {
            AudioMatchStatus = $"听歌识曲失败：{ex.Message}";
        }
        finally
        {
            _recorder?.Dispose();
            _recorder = null;
            _recordStream?.Dispose();
            _recordStream = null;
            IsAudioMatching = false;
        }
    }

    [RelayCommand]
    private async Task SetSearchTypeAsync(SearchType type)
    {
        if (CurrentSearchType == type) return;
        CurrentSearchType = type;
        OnPropertyChanged(nameof(IsSongSearch));
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            await SearchAsync();
        }
    }

    [RelayCommand]
    private void OpenPlaylist(KugouPlaylist playlist)
    {
        if (playlist != null) ShellNavigationService.Instance.OpenPlaylistDetail(playlist);
    }

    [RelayCommand]
    private void OpenArtist(KugouArtist artist)
    {
        if (artist != null) ShellNavigationService.Instance.OpenArtistDetail(artist);
    }

    [RelayCommand]
    private void ClearHistory()
    {
        SearchHistories.Clear();
        var state = AppStateStore.Load();
        state.SearchHistories.Clear();
        AppStateStore.Save(state);
    }

    private void RebuildPageItems()
    {
        PageItems.Clear();
        PageItems.Add(SearchHeaderSection.Instance);
        foreach (var song in SearchResults)
        {
            PageItems.Add(song);
        }
    }
}
