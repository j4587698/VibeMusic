using KuGou.Lite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services;

public readonly record struct DownloadProgressInfo(long ReceivedBytes, long? TotalBytes)
{
    public double Progress =>
        TotalBytes.HasValue && TotalBytes.Value > 0
            ? Math.Clamp((double)ReceivedBytes / TotalBytes.Value, 0, 1)
            : 0;
}

public sealed class AudioCacheService
{
    public static AudioCacheService Instance { get; } = new();

    private static readonly HttpClient HttpClient = new();
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".wav"
    };

    private readonly object _downloadGate = new();
    private readonly HashSet<string> _activeDownloads = new(StringComparer.OrdinalIgnoreCase);

    private AudioCacheService()
    {
    }

    public string? FindCachedFile(KugouSong song)
    {
        var indexedFile = LocalMusicStore.Instance.FindCompletedDownload(song);
        if (!string.IsNullOrWhiteSpace(indexedFile))
        {
            return indexedFile;
        }

        var directory = MusicService.DownloadDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        foreach (var hash in GetKnownHashes(song))
        {
            var match = EnumerateFilesSafe(directory, $"*{hash}*", SearchOption.AllDirectories)
                .FirstOrDefault(IsSupportedAudioFile);
            if (match is not null)
            {
                LocalMusicStore.Instance.SaveDiscoveredDownload(song, match);
                return match;
            }
        }

        foreach (var baseName in GetCandidateBaseNames(song))
        {
            foreach (var extension in SupportedExtensions)
            {
                var exact = Path.Combine(directory, baseName + extension);
                if (File.Exists(exact))
                {
                    LocalMusicStore.Instance.SaveDiscoveredDownload(song, exact);
                    return exact;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 立即将远程歌曲下载到本地（不受"边播边下"开关限制）。返回本地文件路径，失败时抛出异常。
    /// </summary>
    public async Task<string> DownloadSourceAsync(
        KugouSong song,
        string url,
        string? quality,
        CancellationToken cancellationToken = default,
        IProgress<DownloadProgressInfo>? progress = null)
    {
        var existing = FindCachedFile(song);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        if (!IsHttpUrl(url))
        {
            throw new InvalidOperationException("当前音源不是可下载的网络地址。");
        }

        var path = await DownloadCoreAsync(song, url, quality, cancellationToken, progress).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("下载失败，未生成本地文件。");
        }

        return path;
    }

    public string? GetProgressiveCacheTargetPath(KugouSong song, string url, string? quality)
    {
        if (!MusicService.StreamWhileDownloading || !IsHttpUrl(url) || FindCachedFile(song) is not null)
        {
            return null;
        }

        var directory = MusicService.DownloadDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        Directory.CreateDirectory(directory);
        return BuildTargetPath(directory, song, url, quality);
    }

    public void MarkProgressiveCacheCompleted(KugouSong song, string filePath, string? quality, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        LocalMusicStore.Instance.MarkDownloadCompleted(song, filePath, quality, sourceUrl);
    }

    public void MarkProgressiveCacheFailed(KugouSong song, string filePath, string? quality, string sourceUrl, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        LocalMusicStore.Instance.MarkDownloadFailed(song, filePath, quality, sourceUrl, exception);
    }

    private async Task<string?> DownloadCoreAsync(
        KugouSong song,
        string url,
        string? quality,
        CancellationToken cancellationToken,
        IProgress<DownloadProgressInfo>? progress)
    {
        var directory = MusicService.DownloadDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var targetPath = BuildTargetPath(directory, song, url, quality);
        var tempPath = targetPath + ".download";
        var hasExistingActiveDownload = false;
        lock (_downloadGate)
        {
            if (!_activeDownloads.Add(targetPath))
            {
                hasExistingActiveDownload = true;
            }
        }

        if (hasExistingActiveDownload)
        {
            return await WaitForExistingDownloadAsync(song, targetPath, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(targetPath))
            {
                LocalMusicStore.Instance.MarkDownloadCompleted(song, targetPath, quality, url);
                progress?.Report(new DownloadProgressInfo(new FileInfo(targetPath).Length, new FileInfo(targetPath).Length));
                return targetPath;
            }

            LocalMusicStore.Instance.MarkDownloadStarted(song, targetPath, quality, url);

            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            var receivedBytes = 0L;
            progress?.Report(new DownloadProgressInfo(receivedBytes, totalBytes));

            await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var local = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 128 * 1024, useAsync: true))
            {
                var buffer = new byte[128 * 1024];
                while (true)
                {
                    var read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    receivedBytes += read;
                    progress?.Report(new DownloadProgressInfo(receivedBytes, totalBytes));
                }

                await local.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (receivedBytes <= 0)
            {
                throw new IOException("下载内容为空，未写入有效音频数据。");
            }

            if (totalBytes.HasValue && totalBytes.Value > 0 && receivedBytes < totalBytes.Value)
            {
                throw new IOException($"下载不完整：{receivedBytes}/{totalBytes.Value} 字节。");
            }

            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
            }

            LocalMusicStore.Instance.MarkDownloadCompleted(song, targetPath, quality, url);
            progress?.Report(new DownloadProgressInfo(receivedBytes, totalBytes ?? receivedBytes));
            return targetPath;
        }
        catch (Exception ex)
        {
            LocalMusicStore.Instance.MarkDownloadFailed(song, targetPath, quality, url, ex);
            TryDelete(tempPath);
            throw;
        }
        finally
        {
            lock (_downloadGate)
            {
                _activeDownloads.Remove(targetPath);
            }
        }
    }

    private async Task<string?> WaitForExistingDownloadAsync(KugouSong song, string targetPath, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(targetPath))
            {
                LocalMusicStore.Instance.MarkDownloadCompleted(song, targetPath, null, targetPath);
                return targetPath;
            }

            var stillActive = false;
            lock (_downloadGate)
            {
                stillActive = _activeDownloads.Contains(targetPath);
            }

            if (!stillActive)
            {
                return null;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildTargetPath(string directory, KugouSong song, string url, string? quality)
    {
        var baseName = GetCandidateBaseNames(song).FirstOrDefault() ?? "KuGou Music";
        var extension = ResolveExtension(url, quality);
        return BuildUniqueTargetPath(directory, baseName, extension);
    }

    private static string BuildUniqueTargetPath(string directory, string baseName, string extension)
    {
        var index = 1;
        while (true)
        {
            var suffix = index <= 1 ? string.Empty : $" ({index})";
            var candidate = Path.Combine(directory, baseName + suffix + extension);
            if (!File.Exists(candidate) &&
                !File.Exists(candidate + ".download") &&
                !File.Exists(candidate + ".part"))
            {
                return candidate;
            }

            index++;
        }
    }

    private static IEnumerable<string> GetCandidateBaseNames(KugouSong song)
    {
        var title = SanitizeFileName(FirstNonEmpty(song.Title, song.Name, song.Id, "KuGou Music"));
        var artist = SanitizeFileName(song.Artist);
        if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
        {
            yield return $"{artist} - {title}";
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            yield return title;
        }
    }

    private static IEnumerable<string> GetKnownHashes(KugouSong song)
    {
        if (!string.IsNullOrWhiteSpace(song.Hash))
        {
            yield return song.Hash;
        }

        foreach (var hash in song.RelateGoods.Select(item => item.Hash).Where(hash => !string.IsNullOrWhiteSpace(hash)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return hash!;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string directory, string pattern, SearchOption searchOption)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, searchOption);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsSupportedAudioFile(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string ResolveExtension(string url, string? quality)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (extension is ".mgg" or ".mgg1" or ".mggl")
            {
                return ".ogg";
            }

            if (extension == ".mflac")
            {
                return ".flac";
            }

            if (SupportedExtensions.Contains(extension))
            {
                return extension;
            }
        }

        return string.Equals(quality, "flac", StringComparison.OrdinalIgnoreCase) ? ".flac" : ".mp3";
    }

    private static string SanitizeFileName(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "KuGou Music" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            text = text.Replace(invalid, '_');
        }

        return text.Length > 120 ? text[..120].Trim() : text;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
