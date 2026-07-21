using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services;

public interface IPlatformAudioStorage
{
    string DisplayDirectory { get; }

    bool Exists(string location);

    long? GetLength(string location);

    Stream OpenRead(string location);

    bool TryDelete(string location);

    Task<string> PublishAsync(
        string sourceFilePath,
        string displayName,
        string mimeType,
        CancellationToken cancellationToken);
}

public static class PlatformAudioStorage
{
    private static IPlatformAudioStorage? _current;

    public static string DisplayDirectory => Current.DisplayDirectory;

    public static void Initialize(IPlatformAudioStorage storage)
    {
        _current = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public static bool Exists(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return false;
        }

        var storage = Current;
        try
        {
            return storage.Exists(location);
        }
        catch
        {
            return false;
        }
    }

    public static long? GetLength(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var storage = Current;
        try
        {
            return storage.GetLength(location);
        }
        catch
        {
            return null;
        }
    }

    public static Stream OpenRead(string location)
    {
        return Current.OpenRead(location);
    }

    public static bool TryDelete(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return false;
        }

        var storage = Current;
        try
        {
            return storage.TryDelete(location);
        }
        catch
        {
            return false;
        }
    }

    public static Task<string> PublishAsync(
        string sourceFilePath,
        string displayName,
        string mimeType,
        CancellationToken cancellationToken)
    {
        return Current.PublishAsync(sourceFilePath, displayName, mimeType, cancellationToken);
    }

    private static IPlatformAudioStorage Current =>
        _current ?? throw new InvalidOperationException("当前平台未初始化音频存储实现。");
}

public sealed class FileSystemAudioStorage : IPlatformAudioStorage
{
    private readonly Func<string> _directoryProvider;

    public FileSystemAudioStorage(Func<string> directoryProvider)
    {
        _directoryProvider = directoryProvider ?? throw new ArgumentNullException(nameof(directoryProvider));
    }

    public string DisplayDirectory => _directoryProvider();

    public bool Exists(string location) => File.Exists(location);

    public long? GetLength(string location) =>
        File.Exists(location) ? new FileInfo(location).Length : null;

    public Stream OpenRead(string location) => File.OpenRead(location);

    public bool TryDelete(string location)
    {
        if (!File.Exists(location))
        {
            return false;
        }

        File.Delete(location);
        return true;
    }

    public Task<string> PublishAsync(
        string sourceFilePath,
        string displayName,
        string mimeType,
        CancellationToken cancellationToken)
    {
        return PublishFileAsync(sourceFilePath, displayName, cancellationToken);
    }

    private async Task<string> PublishFileAsync(
        string sourceFilePath,
        string displayName,
        CancellationToken cancellationToken)
    {
        var directory = _directoryProvider();
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("未配置音频保存目录。");
        }

        Directory.CreateDirectory(directory);
        var targetPath = GetUniqueFilePath(directory, displayName);
        try
        {
            await using var source = File.OpenRead(sourceFilePath);
            await using var destination = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                128 * 1024,
                useAsync: true);
            await source.CopyToAsync(destination, 128 * 1024, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            return targetPath;
        }
        catch
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            throw;
        }
    }

    private static string GetUniqueFilePath(string directory, string displayName)
    {
        var fileName = Path.GetFileNameWithoutExtension(displayName);
        var extension = Path.GetExtension(displayName);
        for (var index = 1; ; index++)
        {
            var suffix = index == 1 ? string.Empty : $" ({index})";
            var candidate = Path.Combine(directory, fileName + suffix + extension);
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
