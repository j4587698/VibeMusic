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

    private LyricsService()
    {
        _player.PropertyChanged += OnPlayerPropertyChanged;
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

            var lyric = await MusicService.Client.GetLyricAsync(id, accessKey, "lrc", decode: true, cancellationToken: cancellationToken).ConfigureAwait(false);
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
            UpdateActiveLine();
        }
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
            if (line.Length == 0 || IsMetadataLine(line))
            {
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

    private static bool IsMetadataLine(string line) =>
        line.StartsWith("[ti:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("[ar:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("[al:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("[by:", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("[offset:", StringComparison.OrdinalIgnoreCase);

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

    public LyricLine(string timeText, string text, double startTime)
    {
        TimeText = timeText;
        Text = text;
        StartTime = startTime;
    }

    public string TimeText { get; }

    public string Text { get; }

    public double StartTime { get; }

    public bool IsPlaceholder { get; init; }

    public double DisplayOpacity => IsPlaceholder ? 0 : IsActive ? 1 : 0.5;

    public int DisplayFontSize => IsActive ? 31 : 20;

    public double ActiveMarkerOpacity => IsActive ? 1 : 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayOpacity))]
    [NotifyPropertyChangedFor(nameof(DisplayFontSize))]
    [NotifyPropertyChangedFor(nameof(ActiveMarkerOpacity))]
    private bool _isActive;
}