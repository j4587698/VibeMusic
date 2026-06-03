using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KuGou.Lite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services;

public sealed partial class LyricsService : ObservableObject
{
    public static LyricsService Instance { get; } = new();

    private readonly PlayerService _player = PlayerService.Instance;
    private CancellationTokenSource? _loadCts;
    private int _activeLineIndex = -1;
    private const int FocusedLineCount = 9;
    private const int FocusedLineCenterIndex = FocusedLineCount / 2;
    private readonly DispatcherTimer _interpolationTimer;
    private readonly System.Diagnostics.Stopwatch _playbackClock = new();
    private double _clockAnchorSeconds;
    private int _wordHighlightSubscribers;

    public int ActiveLineIndex => _activeLineIndex;

    public LyricLine? ActiveLine => _activeLineIndex >= 0 && _activeLineIndex < Lines.Count ? Lines[_activeLineIndex] : null;

    public ObservableCollection<LyricLine> Lines { get; } = new();

    public ObservableCollection<LyricLine> FocusedLines { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "播放歌曲后自动加载歌词";

    [ObservableProperty]
    private string _currentLineText = "暂无歌词";

    [ObservableProperty]
    private string _nextLineText = string.Empty;

    // 当前播放位置（秒），由高分辨率时钟驱动，供逐字歌词控件按帧裁剪。
    [ObservableProperty]
    private double _wordPlaybackPosition;

    private LyricsService()
    {
        _player.PropertyChanged += OnPlayerPropertyChanged;
        _interpolationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 fps
        };
        _interpolationTimer.Tick += OnInterpolationTick;
    }

    public async Task LoadForCurrentSongAsync()
    {
        if (_player.CurrentSong is not KugouSong song)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Lines.Clear();
                SetActiveLineIndex(-1);
                ResetFocusedLines("暂无歌词");
                CurrentLineText = "暂无歌词";
                NextLineText = string.Empty;
                StatusMessage = "当前没有歌曲";
            });
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var cancellationToken = cts.Token;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            StatusMessage = "正在加载歌词";
            Lines.Clear();
            ResetFocusedLines("正在加载歌词");
            SetActiveLineIndex(-1);
            CurrentLineText = "正在加载歌词";
            NextLineText = string.Empty;
        });

        try
        {
            var search = await MusicService.Client.SearchLyricAsync(song.Title, song.Hash, song.MixSongId, cancellationToken: cancellationToken).ConfigureAwait(false);
            var (id, accessKey) = ReadFirstLyricCandidate(search.BodyText);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(accessKey))
            {
                await ApplyNoLyricAsync("没有匹配歌词", cancellationToken).ConfigureAwait(false);
                return;
            }

            var lyric = await MusicService.Client.GetLyricAsync(id, accessKey, "krc", decode: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            var text = ReadDecodedLyric(lyric.BodyText);
            var lines = new List<LyricLine>(ParseLines(text));
            cancellationToken.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Lines.Clear();
                foreach (var line in lines)
                {
                    Lines.Add(line);
                }

                StatusMessage = Lines.Count > 0 ? $"已加载 {Lines.Count} 行歌词" : "歌词内容为空";
                UpdateActiveLine();
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = $"歌词加载失败：{ex.Message}";
                CurrentLineText = "歌词加载失败";
                NextLineText = ex.Message;
            });
        }
        finally
        {
            var ownsLoadState = ReferenceEquals(_loadCts, cts);
            if (ReferenceEquals(_loadCts, cts))
            {
                _loadCts = null;
            }

            cts.Dispose();
            if (ownsLoadState)
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
        }
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerService.CurrentSong))
        {
            _ = LoadForCurrentSongAsync();
            return;
        }

        if (e.PropertyName == nameof(PlayerService.Progress))
        {
            // PlayerService.Progress only ticks every 500ms; re-anchor the
            // high-resolution clock so per-word interpolation stays in sync.
            AnchorPlaybackClock();
            UpdateActiveLine();
            UpdateWordProgress();
        }
        
        if (e.PropertyName == nameof(PlayerService.IsPlaying))
        {
            AnchorPlaybackClock();
            UpdateInterpolationTimerState();
        }
    }

    private void OnInterpolationTick(object? sender, EventArgs e) => UpdateWordProgress();

    private void UpdateWordProgress()
    {
        if (_activeLineIndex < 0 || _activeLineIndex >= Lines.Count)
        {
            return;
        }

        if (Lines[_activeLineIndex].Words.Count == 0)
        {
            return;
        }

        // 仅推送一个标量位置（每帧一次通知），逐字裁剪在控件 Render 内完成，
        // 避免逐字写属性产生的多次变更通知与可视树更新。
        WordPlaybackPosition = GetPlaybackPosition();
    }

    private void AnchorPlaybackClock()
    {
        _clockAnchorSeconds = _player.Progress;
        _playbackClock.Restart();
    }

    private double GetPlaybackPosition()
    {
        var position = _clockAnchorSeconds;
        if (_player.IsPlaying)
        {
            position += _playbackClock.Elapsed.TotalSeconds;
        }

        return position;
    }

    private void UpdateInterpolationTimerState()
    {
        // Only run the 30fps timer when something is actually rendering the
        // per-word karaoke effect (e.g. the desktop lyrics window is open).
        var shouldRun = _player.IsPlaying && _wordHighlightSubscribers > 0;
        if (shouldRun)
        {
            if (!_interpolationTimer.IsEnabled)
            {
                _interpolationTimer.Start();
            }
        }
        else if (_interpolationTimer.IsEnabled)
        {
            _interpolationTimer.Stop();
        }
    }

    public void BeginWordHighlight()
    {
        _wordHighlightSubscribers++;
        UpdateInterpolationTimerState();
        UpdateWordProgress();
    }

    public void EndWordHighlight()
    {
        if (_wordHighlightSubscribers > 0)
        {
            _wordHighlightSubscribers--;
        }

        UpdateInterpolationTimerState();
    }

    private async Task ApplyNoLyricAsync(string message, CancellationToken cancellationToken)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Lines.Clear();
            SetActiveLineIndex(-1);
            ResetFocusedLines(message);
            StatusMessage = message;
            CurrentLineText = message;
            NextLineText = string.Empty;
        });
    }

    private void UpdateActiveLine()
    {
        if (Lines.Count == 0)
        {
            SetActiveLineIndex(-1);
            CurrentLineText = IsLoading ? "正在加载歌词" : "暂无歌词";
            NextLineText = string.Empty;
            ResetFocusedLines(CurrentLineText);
            return;
        }

        var progress = _player.Progress + 0.25;
        var index = -1;
        for (var i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].StartTime <= progress)
            {
                index = i;
                continue;
            }

            break;
        }

        if (index < 0)
        {
            index = 0;
        }

        if (index == _activeLineIndex)
        {
            return;
        }

        if (_activeLineIndex >= 0 && _activeLineIndex < Lines.Count)
        {
            Lines[_activeLineIndex].IsActive = false;
        }

        SetActiveLineIndex(index);
        Lines[index].IsActive = true;
        CurrentLineText = Lines[index].Text;
        NextLineText = index + 1 < Lines.Count ? Lines[index + 1].Text : string.Empty;
        UpdateFocusedLines(index);
    }

    private void SetActiveLineIndex(int index)
    {
        if (_activeLineIndex == index)
        {
            return;
        }

        _activeLineIndex = index;
        OnPropertyChanged(nameof(ActiveLineIndex));
        OnPropertyChanged(nameof(ActiveLine));
    }

    private void UpdateFocusedLines(int activeIndex)
    {
        FocusedLines.Clear();
        for (var slot = 0; slot < FocusedLineCount; slot++)
        {
            var sourceIndex = activeIndex + slot - FocusedLineCenterIndex;
            FocusedLines.Add(sourceIndex >= 0 && sourceIndex < Lines.Count
                ? Lines[sourceIndex]
                : LyricLine.Placeholder);
        }
    }

    private void ResetFocusedLines(string text)
    {
        FocusedLines.Clear();
        for (var slot = 0; slot < FocusedLineCount; slot++)
        {
            FocusedLines.Add(slot == FocusedLineCenterIndex ? new LyricLine(string.Empty, text, 0) { IsActive = true } : LyricLine.Placeholder);
        }
    }

    private static (string Id, string AccessKey) ReadFirstLyricCandidate(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return (string.Empty, string.Empty);
        }

        var first = candidates[0];
        var id = first.TryGetProperty("id", out var idElement) ? idElement.ToString() : string.Empty;
        var accessKey = first.TryGetProperty("accesskey", out var accessElement) ? accessElement.GetString() ?? string.Empty : string.Empty;
        return (id, accessKey);
    }

    private static string ReadDecodedLyric(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("decodeContent", out var content) ? content.GetString() ?? string.Empty : string.Empty;
    }

    private static IEnumerable<LyricLine> ParseLines(string text)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            
            // Remove metadata tags anywhere in the line (handles cases like [00:00.00][id:$00000000])
            line = MetadataRegex().Replace(line, string.Empty).Trim();
            
            if (line.Length == 0)
            {
                continue;
            }

            var krcLineMatch = KrcLineRegex().Match(line);
            if (krcLineMatch.Success)
            {
                var lineStartTime = double.Parse(krcLineMatch.Groups["start"].Value, CultureInfo.InvariantCulture) / 1000.0;
                var lineDuration = double.Parse(krcLineMatch.Groups["duration"].Value, CultureInfo.InvariantCulture) / 1000.0;
                
                var contentMatch = KrcLineRegex().Replace(line, string.Empty);
                var wordMatches = KrcWordRegex().Matches(contentMatch);
                
                var rawText = KrcWordRegex().Replace(contentMatch, match => match.Groups["text"].Value).Trim();
                var timeText = TimeSpan.FromSeconds(lineStartTime).ToString(@"mm\:ss\.ff");
                
                var lyricLine = new LyricLine(timeText, rawText, lineStartTime, lineDuration);
                foreach (Match wordMatch in wordMatches)
                {
                    var offset = double.Parse(wordMatch.Groups["offset"].Value, CultureInfo.InvariantCulture) / 1000.0;
                    var wordDuration = double.Parse(wordMatch.Groups["duration"].Value, CultureInfo.InvariantCulture) / 1000.0;
                    var wordText = wordMatch.Groups["text"].Value;
                    lyricLine.Words.Add(new LyricWord(wordText, lineStartTime + offset, wordDuration));
                }
                
                yield return lyricLine;
                continue;
            }

            var matches = TimestampRegex().Matches(line);
            var content = TimestampRegex().Replace(line, string.Empty).Trim();
            if (content.Length == 0)
            {
                continue;
            }

            if (matches.Count == 0)
            {
                yield return new LyricLine(string.Empty, content, 0);
                continue;
            }

            foreach (Match match in matches)
            {
                var timeText = match.Value.Trim('[', ']');
                yield return new LyricLine(timeText, content, ParseTime(match));
            }
        }
    }

    [GeneratedRegex(@"\[(?<start>\d+),(?<duration>\d+)\]")]
    private static partial Regex KrcLineRegex();

    [GeneratedRegex(@"<(?<offset>\d+),(?<duration>\d+),\d+>(?<text>[^<]+)")]
    private static partial Regex KrcWordRegex();

    [GeneratedRegex(@"\[[a-zA-Z_$]+:.*?\]")]
    private static partial Regex MetadataRegex();

    private static double ParseTime(Match match)
    {
        var minutes = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        var seconds = double.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture);
        return minutes * 60 + seconds;
    }

    [GeneratedRegex(@"\[(?<minute>\d{2}):(?<second>\d{2}(?:\.\d{1,3})?)\]")]
    private static partial Regex TimestampRegex();
}

public sealed partial class LyricLine : ObservableObject
{
    public static LyricLine Placeholder { get; } = new(string.Empty, string.Empty, 0) { IsPlaceholder = true };

    public LyricLine(string timeText, string text, double startTime, double duration = 0)
    {
        TimeText = timeText;
        Text = text;
        StartTime = startTime;
        Duration = duration;
    }

    public string TimeText { get; }

    public string Text { get; }

    public double StartTime { get; }
    
    public double Duration { get; }
    
    public ObservableCollection<LyricWord> Words { get; } = new();

    public bool IsPlaceholder { get; init; }

    public double DisplayOpacity => IsPlaceholder ? 0 : IsActive ? 1 : 0.5;

    public int DisplayFontSize => IsActive ? 31 : 20;

    public double ActiveMarkerOpacity => IsActive ? 1 : 0;

    public bool ShowWords => IsActive && Words.Count > 0;

    public bool ShowPlain => !ShowWords;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayOpacity))]
    [NotifyPropertyChangedFor(nameof(DisplayFontSize))]
    [NotifyPropertyChangedFor(nameof(ActiveMarkerOpacity))]
    [NotifyPropertyChangedFor(nameof(ShowWords))]
    [NotifyPropertyChangedFor(nameof(ShowPlain))]
    private bool _isActive;
}

public sealed partial class LyricWord : ObservableObject
{
    public LyricWord(string text, double startTime, double duration)
    {
        Text = text;
        StartTime = startTime;
        Duration = duration;
    }

    public string Text { get; }
    public double StartTime { get; }
    public double Duration { get; }

    [ObservableProperty]
    private double _progress;
}