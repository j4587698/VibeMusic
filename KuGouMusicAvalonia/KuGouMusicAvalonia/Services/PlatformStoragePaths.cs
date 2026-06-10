using System;
using System.IO;

namespace KuGouMusicAvalonia.Services;

internal static class PlatformStoragePaths
{
    public static string DefaultDownloadDirectory =>
        UsesPrivateDownloadDirectory
            ? Path.Combine(AppStateStore.AppDirectory, "Downloads")
            : GetDesktopMusicDirectory();

    public static string NormalizeDownloadDirectory(string? directory)
    {
        if (UsesPrivateDownloadDirectory || string.IsNullOrWhiteSpace(directory))
        {
            return DefaultDownloadDirectory;
        }

        return directory;
    }

    private static bool UsesPrivateDownloadDirectory =>
        OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

    private static string GetDesktopMusicDirectory()
    {
        var musicDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        return string.IsNullOrWhiteSpace(musicDirectory)
            ? Path.Combine(AppStateStore.AppDirectory, "Downloads")
            : musicDirectory;
    }
}
