using System;
using System.IO;

namespace KuGouMusicAvalonia.Services;

internal static class PlatformStoragePaths
{
    public static string? ExternalDownloadsDirectory { get; set; }

    public static string DefaultDownloadDirectory =>
        ExternalDownloadsDirectory
        ?? DefaultFallbackDownloadDirectory;

    public static string NormalizeDownloadDirectory(string? directory)
    {
        if (UsesPrivateDownloadDirectory || string.IsNullOrWhiteSpace(directory))
        {
            return DefaultDownloadDirectory;
        }

        return directory;
    }

    private static bool UsesPrivateDownloadDirectory =>
        ExternalDownloadsDirectory is null;

    private static string DefaultFallbackDownloadDirectory =>
        OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()
            ? Path.Combine(AppStateStore.AppDirectory, "Downloads")
            : GetDesktopMusicDirectory();

    private static string GetDesktopMusicDirectory()
    {
        var musicDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        return string.IsNullOrWhiteSpace(musicDirectory)
            ? Path.Combine(AppStateStore.AppDirectory, "Downloads")
            : musicDirectory;
    }
}
