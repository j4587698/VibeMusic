using Android.Content;
using Android.Media;
using Android.OS;
using Android.Provider;
using KuGouMusicAvalonia.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AndroidUri = Android.Net.Uri;

namespace KuGouMusicAvalonia.Android;

internal sealed class AndroidMediaStoreAudioStorage : IPlatformAudioStorage
{
    private const string AlbumDirectoryName = "VibeMusic";
    private readonly Context _context;
    private readonly ContentResolver _contentResolver;

    public AndroidMediaStoreAudioStorage(Context context)
    {
        var applicationContext = context.ApplicationContext ?? context;
        _context = applicationContext;
        _contentResolver = applicationContext.ContentResolver
            ?? throw new InvalidOperationException("无法获取 Android ContentResolver。");
    }

    public string DisplayDirectory => "Music/VibeMusic";

    public bool Exists(string location)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            return File.Exists(location);
        }

        var uri = ParseUri(location);
        using var descriptor = _contentResolver.OpenAssetFileDescriptor(uri, "r");
        return descriptor is not null;
    }

    public long? GetLength(string location)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            return File.Exists(location) ? new FileInfo(location).Length : null;
        }

        var uri = ParseUri(location);
        using var cursor = _contentResolver.Query(
            uri,
            new[] { MediaStore.IMediaColumns.Size },
            null,
            null,
            null);

        if (cursor is null || !cursor.MoveToFirst())
        {
            return null;
        }

        var columnIndex = cursor.GetColumnIndex(MediaStore.IMediaColumns.Size);
        return columnIndex >= 0 && !cursor.IsNull(columnIndex)
            ? cursor.GetLong(columnIndex)
            : null;
    }

    public Stream OpenRead(string location)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            return File.OpenRead(location);
        }

        return _contentResolver.OpenInputStream(ParseUri(location))
            ?? throw new FileNotFoundException("无法打开已下载的音频文件。", location);
    }

    public bool TryDelete(string location)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
        {
            if (!File.Exists(location))
            {
                return false;
            }

            File.Delete(location);
            return true;
        }

        return _contentResolver.Delete(ParseUri(location), null, null) > 0;
    }

    public Task<string> PublishAsync(
        string sourceFilePath,
        string displayName,
        string mimeType,
        CancellationToken cancellationToken)
    {
        return Build.VERSION.SdkInt >= BuildVersionCodes.Q
            ? PublishWithMediaStoreAsync(sourceFilePath, displayName, mimeType, cancellationToken)
            : PublishLegacyAsync(sourceFilePath, displayName, mimeType, cancellationToken);
    }

    private async Task<string> PublishWithMediaStoreAsync(
        string sourceFilePath,
        string displayName,
        string mimeType,
        CancellationToken cancellationToken)
    {
        var collection = MediaStore.Audio.Media.ExternalContentUri
            ?? throw new InvalidOperationException("无法访问 Android 公共音乐目录。");
        var relativePath = $"{global::Android.OS.Environment.DirectoryMusic}/{AlbumDirectoryName}/";
        var uniqueDisplayName = GetUniqueDisplayName(collection, relativePath, displayName);

        using var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, uniqueDisplayName);
        values.Put(MediaStore.IMediaColumns.MimeType, mimeType);
        values.Put(MediaStore.IMediaColumns.RelativePath, relativePath);
        values.Put(MediaStore.IMediaColumns.IsPending, 1);

        var uri = _contentResolver.Insert(collection, values)
            ?? throw new IOException("无法在 Android 公共音乐目录中创建文件。");

        try
        {
            await using (var source = File.OpenRead(sourceFilePath))
            await using (var destination = _contentResolver.OpenOutputStream(uri, "w")
                ?? throw new IOException("无法写入 Android 公共音乐文件。"))
            {
                await source.CopyToAsync(destination, 128 * 1024, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            using var completedValues = new ContentValues();
            completedValues.Put(MediaStore.IMediaColumns.IsPending, 0);
            _contentResolver.Update(uri, completedValues, null, null);
            return uri.ToString() ?? throw new IOException("Android MediaStore 未返回有效文件地址。");
        }
        catch
        {
            _contentResolver.Delete(uri, null, null);
            throw;
        }
    }

    private async Task<string> PublishLegacyAsync(
        string sourceFilePath,
        string displayName,
        string mimeType,
        CancellationToken cancellationToken)
    {
        var musicDirectory = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
            global::Android.OS.Environment.DirectoryMusic)
            ?? throw new IOException("无法访问 Android 公共音乐目录。");
        var targetDirectory = Path.Combine(musicDirectory.AbsolutePath, AlbumDirectoryName);
        Directory.CreateDirectory(targetDirectory);
        var targetPath = GetUniqueFilePath(targetDirectory, displayName);

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
            MediaScannerConnection.ScanFile(_context, new[] { targetPath }, new[] { mimeType }, null);
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

    private string GetUniqueDisplayName(AndroidUri collection, string relativePath, string displayName)
    {
        var fileName = Path.GetFileNameWithoutExtension(displayName);
        var extension = Path.GetExtension(displayName);
        for (var index = 1; ; index++)
        {
            var suffix = index == 1 ? string.Empty : $" ({index})";
            var candidate = fileName + suffix + extension;
            if (!DisplayNameExists(collection, relativePath, candidate))
            {
                return candidate;
            }
        }
    }

    private bool DisplayNameExists(AndroidUri collection, string relativePath, string displayName)
    {
        var selection = $"{MediaStore.IMediaColumns.RelativePath} = ? AND {MediaStore.IMediaColumns.DisplayName} = ?";
        using var cursor = _contentResolver.Query(
            collection,
            new[] { MediaStore.MediaColumns.Id },
            selection,
            new[] { relativePath, displayName },
            null);
        return cursor?.MoveToFirst() == true;
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

    private static AndroidUri ParseUri(string location)
    {
        return AndroidUri.Parse(location)
            ?? throw new InvalidDataException($"无效的 Android 媒体地址：{location}");
    }

}
