using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services.Update;

/// <summary>更新清单与产物的下载地址。</summary>
public static class UpdateEndpoints
{
    /// <summary>
    /// 国内可访问的镜像源（对象存储 / CDN）根地址，不含尾部斜杠。
    /// 当前指向 deploy/mirror-worker 部署的 Cloudflare Worker，实时回源 GitHub Release。
    /// </summary>
    private const string MirrorBaseUrl = "https://update.4587698.xyz";

    /// <summary>GitHub 仓库，形如 owner/repo。作为镜像不可用时的兜底源。</summary>
    private const string GitHubRepository = "j4587698/VibeMusic";

    private const string ManifestFileName = "latest.json";

    /// <summary>按优先级排列的清单地址。国内镜像在前，GitHub 兜底。</summary>
    public static IReadOnlyList<string> ManifestSources { get; } = BuildManifestSources();

    /// <summary>清单签名文件地址，与 <see cref="ManifestSources"/> 一一对应。</summary>
    public static IReadOnlyList<string> SignatureSources { get; } = BuildSignatureSources();

    /// <summary>是否已配置任何可用的更新源。</summary>
    public static bool IsConfigured => ManifestSources.Count > 0;

    /// <summary>用户手动下载时打开的页面。</summary>
    public static string? ReleasePageUrl =>
        GitHubRepository.Length > 0
            ? $"https://github.com/{GitHubRepository}/releases/latest"
            : MirrorBaseUrl.Length > 0 ? MirrorBaseUrl : null;

    private static List<string> BuildManifestSources()
    {
        var sources = new List<string>(2);
        if (MirrorBaseUrl.Length > 0)
        {
            sources.Add($"{MirrorBaseUrl}/{ManifestFileName}");
        }

        if (GitHubRepository.Length > 0)
        {
            sources.Add($"https://github.com/{GitHubRepository}/releases/latest/download/{ManifestFileName}");
        }

        return sources;
    }

    private static List<string> BuildSignatureSources()
    {
        var sources = new List<string>(ManifestSources.Count);
        foreach (var source in ManifestSources)
        {
            sources.Add(source + ".sig");
        }

        return sources;
    }
}

/// <summary>
/// 应用更新的共享逻辑：多源拉取清单 → 验签 → 比版本 → 多源下载 → SHA256 校验 → 交给平台安装器。
/// 全程不触碰任何平台 API。
/// </summary>
public sealed class UpdateService
{
    /// <summary>拒绝体积异常的产物，防止解压炸弹与磁盘耗尽。</summary>
    private const long MaxAssetBytes = 512L * 1024 * 1024;

    /// <summary>单个清单源的等待上限，超时后换下一个源。</summary>
    private static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(8);

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static UpdateService Instance { get; } = new();

    private int _lastSuccessfulSourceIndex;

    private UpdateService()
    {
    }

    public string CurrentVersion => UpdatePlatform.CurrentVersion;

    /// <summary>是否具备完整的应用内更新能力（源已配置 + 公钥已配置 + 平台安装器已注册）。</summary>
    public bool CanUpdateInApp =>
        UpdateEndpoints.IsConfigured && UpdateSigning.IsConfigured && PlatformUpdateInstaller.CanInstallAutomatically;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = UpdatePlatform.CurrentVersion;

        if (!UpdateEndpoints.IsConfigured)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.Unsupported,
                current,
                Message: "尚未配置更新源。");
        }

        UpdateManifest? manifest;
        try
        {
            manifest = await FetchManifestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.Failed,
                current,
                Message: $"检查更新失败：{FirstLine(ex.GetBaseException().Message)}");
        }

        if (manifest is null)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.Failed,
                current,
                Message: "所有更新源均不可用，请稍后重试。");
        }

        if (!UpdateVersion.IsNewer(manifest.Version, current))
        {
            return new UpdateCheckResult(UpdateCheckStatus.UpToDate, current, manifest.Version);
        }

        var asset = manifest.FindAsset(UpdatePlatform.PlatformCandidates);
        if (asset is null)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.Unsupported,
                current,
                manifest.Version,
                manifest.ReleaseNotes,
                Message: "新版本未提供适用于当前平台的安装包。");
        }

        if (asset.Size > MaxAssetBytes)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.Failed,
                current,
                manifest.Version,
                Message: "更新包体积异常，已终止。");
        }

        return new UpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            current,
            manifest.Version,
            manifest.ReleaseNotes,
            manifest.Mandatory,
            asset);
    }

    /// <summary>下载并校验产物，返回本地文件路径。任一环节失败都会清理临时文件并抛出。</summary>
    public async Task<string> DownloadAsync(
        UpdateAsset asset,
        string version,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (string.IsNullOrWhiteSpace(asset.Sha256))
        {
            throw new InvalidOperationException("更新清单缺少校验值，已拒绝下载。");
        }

        var directory = UpdatePaths.DownloadDirectory;
        Directory.CreateDirectory(directory);

        var targetPath = Path.Combine(directory, BuildFileName(asset, version));
        var tempPath = targetPath + ".part";

        Exception? lastError = null;
        foreach (var url in asset.Urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsHttpsUrl(url))
            {
                lastError = new InvalidOperationException($"更新源地址不是 HTTPS：{url}");
                continue;
            }

            try
            {
                await DownloadToFileAsync(url, tempPath, asset.Size, progress, cancellationToken).ConfigureAwait(false);

                var actual = await ComputeSha256Async(tempPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, asset.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    lastError = new InvalidOperationException("更新包校验失败，可能已损坏或被篡改。");
                    TryDelete(tempPath);
                    continue;
                }

                TryDelete(targetPath);
                File.Move(tempPath, targetPath);
                return targetPath;
            }
            catch (OperationCanceledException)
            {
                TryDelete(tempPath);
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                TryDelete(tempPath);
            }
        }

        throw new InvalidOperationException(
            $"更新包下载失败：{FirstLine(lastError?.GetBaseException().Message ?? "所有下载源均不可用")}",
            lastError);
    }

    /// <summary>清理历史下载残留。</summary>
    public void CleanupDownloads()
    {
        try
        {
            var directory = UpdatePaths.DownloadDirectory;
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                TryDelete(file);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async Task<UpdateManifest?> FetchManifestAsync(CancellationToken cancellationToken)
    {
        var sources = UpdateEndpoints.ManifestSources;
        var signatures = UpdateEndpoints.SignatureSources;

        // 从上次成功的源开始，避免每次都卡在不可达的源上。
        for (var offset = 0; offset < sources.Count; offset++)
        {
            var index = (_lastSuccessfulSourceIndex + offset) % sources.Count;
            cancellationToken.ThrowIfCancellationRequested();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ManifestTimeout);

            try
            {
                var payload = await HttpClient
                    .GetByteArrayAsync(sources[index], timeout.Token)
                    .ConfigureAwait(false);

                if (UpdateSigning.IsConfigured)
                {
                    var signature = await HttpClient
                        .GetStringAsync(signatures[index], timeout.Token)
                        .ConfigureAwait(false);

                    if (!UpdateSigning.Verify(payload, signature))
                    {
                        // 验签失败一律放弃该源，绝不降级为「不验签直接用」。
                        continue;
                    }
                }

                var manifest = JsonSerializer.Deserialize(payload, UpdateManifestJsonContext.Default.UpdateManifest);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
                {
                    continue;
                }

                _lastSuccessfulSourceIndex = index;
                return manifest;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 单个源超时，换下一个。
            }
            catch (HttpRequestException)
            {
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private static async Task DownloadToFileAsync(
        string url,
        string tempPath,
        long expectedSize,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? (expectedSize > 0 ? expectedSize : null);
        if (totalBytes > MaxAssetBytes)
        {
            throw new InvalidOperationException("更新包体积异常，已终止下载。");
        }

        var receivedBytes = 0L;
        progress?.Report(new DownloadProgressInfo(receivedBytes, totalBytes));

        await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var local = new FileStream(
            tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true);

        var buffer = new byte[128 * 1024];
        while (true)
        {
            var read = await remote.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            receivedBytes += read;
            if (receivedBytes > MaxAssetBytes)
            {
                throw new InvalidOperationException("更新包体积超出上限，已终止下载。");
            }

            await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            progress?.Report(new DownloadProgressInfo(receivedBytes, totalBytes));
        }

        await local.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (expectedSize > 0 && receivedBytes != expectedSize)
        {
            throw new InvalidOperationException("更新包大小与清单不一致。");
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static string BuildFileName(UpdateAsset asset, string version)
    {
        // 文件名一律重新拼装，不直接采信清单里的 fileName，避免路径穿越。
        var extension = asset.Kind switch
        {
            UpdateAssetKinds.Apk => ".apk",
            _ => ".zip"
        };

        var platform = SanitizeToken(asset.Platform);
        var safeVersion = SanitizeToken(version);
        return string.Create(CultureInfo.InvariantCulture, $"vibemusic-{safeVersion}-{platform}{extension}");
    }

    private static string SanitizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        Span<char> buffer = stackalloc char[Math.Min(value.Length, 64)];
        var length = 0;
        foreach (var ch in value)
        {
            if (length >= buffer.Length)
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_')
            {
                buffer[length++] = ch;
            }
        }

        return length == 0 ? "unknown" : new string(buffer[..length]);
    }

    private static bool IsHttpsUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string FirstLine(string message)
    {
        var index = message.IndexOfAny(['\r', '\n']);
        return index < 0 ? message : message[..index];
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        // 不设总超时：更新包可能有数十 MB，慢速网络下整体耗时不可预估。
        // 连接阶段由 ConnectTimeout 保护，清单请求另行加独立超时。
        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }
}

/// <summary>更新过程使用的本地目录。刻意使用本机目录而非漫游目录。</summary>
public static class UpdatePaths
{
    private static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KuGouMusicAvalonia",
        "update");

    public static string DownloadDirectory { get; } = Path.Combine(Root, "download");

    /// <summary>解压后的新版本目录，由平台安装器写入、由 updater 进程消费。</summary>
    public static string StagingDirectory { get; } = Path.Combine(Root, "staging");
}
