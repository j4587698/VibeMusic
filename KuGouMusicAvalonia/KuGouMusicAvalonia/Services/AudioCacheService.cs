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

    private AudioCacheService()
    {
    }

    public string? FindCachedFile(KugouSong song)
    {
        return LocalMusicStore.Instance.FindCompletedDownload(song);
    }

    /// <summary>
    /// 立即将远程歌曲下载到本地（不受"边播边下"开关限制）。返回本地文件位置，失败时抛出异常。
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

        var directory = GetWorkingDirectory();
        Directory.CreateDirectory(directory);
        return BuildTargetPath(directory, song, url, quality);
    }

    public async Task MarkProgressiveCacheCompletedAsync(KugouSong song, string filePath, string? quality, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        string? completedLocation = null;
        try
        {
            completedLocation = await PublishCompletedFileAsync(filePath, sourceUrl, quality, CancellationToken.None).ConfigureAwait(false);
            LocalMusicStore.Instance.MarkDownloadCompleted(song, completedLocation, quality, sourceUrl);
            TryDelete(filePath);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(completedLocation))
            {
                PlatformAudioStorage.TryDelete(completedLocation);
            }
            LocalMusicStore.Instance.MarkDownloadFailed(song, filePath, quality, sourceUrl, ex);
            TryDelete(filePath);
        }
    }

    public void MarkProgressiveCacheFailed(KugouSong song, string filePath, string? quality, string sourceUrl, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        LocalMusicStore.Instance.MarkDownloadFailed(song, filePath, quality, sourceUrl, exception);
        TryDelete(filePath);
    }

    private async Task<string> DownloadCoreAsync(
        KugouSong song,
        string url,
        string? quality,
        CancellationToken cancellationToken,
        IProgress<DownloadProgressInfo>? progress)
    {
        var directory = GetWorkingDirectory();
        var targetPath = BuildTargetPath(directory, song, url, quality);
        var tempPath = targetPath + ".download";
        string? completedLocation = null;

        try
        {
            Directory.CreateDirectory(directory);
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

            File.Move(tempPath, targetPath);

            completedLocation = await PublishCompletedFileAsync(targetPath, url, quality, cancellationToken).ConfigureAwait(false);
            LocalMusicStore.Instance.MarkDownloadCompleted(song, completedLocation, quality, url);
            TryDelete(targetPath);
            progress?.Report(new DownloadProgressInfo(receivedBytes, totalBytes ?? receivedBytes));
            return completedLocation;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(completedLocation))
            {
                PlatformAudioStorage.TryDelete(completedLocation);
            }
            LocalMusicStore.Instance.MarkDownloadFailed(song, targetPath, quality, url, ex);
            TryDelete(tempPath);
            TryDelete(targetPath);
            throw;
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

    private static string GetWorkingDirectory()
    {
        return Path.Combine(AppStateStore.AppDirectory, "DownloadCache");
    }

    private static async Task<string> PublishCompletedFileAsync(
        string filePath,
        string sourceUrl,
        string? quality,
        CancellationToken cancellationToken)
    {
        var displayName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(displayName);
        var mimeType = ResolveMimeType(string.IsNullOrWhiteSpace(extension)
            ? ResolveExtension(sourceUrl, quality)
            : extension);
        var publishedLocation = await PlatformAudioStorage
            .PublishAsync(filePath, displayName, mimeType, cancellationToken)
            .ConfigureAwait(false);
        return publishedLocation;
    }

    private static string ResolveMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            _ => "audio/mpeg"
        };
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
