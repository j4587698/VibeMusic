using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KuGouMusicAvalonia.Services;

internal sealed class MusicAppState
{
    public Dictionary<string, string> Cookies { get; set; } = new(StringComparer.Ordinal);
    public bool AutoReceiveVipBeforePlayback { get; set; } = true;
    public string ThemeMode { get; set; } = "深色";
    public bool StreamWhileDownloading { get; set; } = true;
    public string DownloadDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    public string DefaultPlaybackQuality { get; set; } = "标准 128k";
    public List<string> FavoriteSongKeys { get; set; } = new();
    public List<string> SearchHistories { get; set; } = new();
}

internal static class AppStateStore
{
    public static string AppDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KuGouMusicAvalonia");

    public static string StatePath { get; } = Path.Combine(AppDirectory, "session.json");

    public static MusicAppState Load()
    {
        if (!File.Exists(StatePath))
        {
            return new MusicAppState();
        }

        try
        {
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize(json, AppStateJsonContext.Default.MusicAppState) ?? new MusicAppState();
        }
        catch
        {
            return new MusicAppState();
        }
    }

    public static void Save(MusicAppState state)
    {
        Directory.CreateDirectory(AppDirectory);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, AppStateJsonContext.Default.MusicAppState));
    }

    public static void Delete()
    {
        if (File.Exists(StatePath))
        {
            File.Delete(StatePath);
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MusicAppState))]
internal sealed partial class AppStateJsonContext : JsonSerializerContext;