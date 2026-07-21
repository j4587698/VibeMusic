using System;
using System.IO;
using System.Threading;
using Avalonia;
using KuGouMusicAvalonia.Services;
using LuminaUI.Diagnostics;

namespace KuGouMusicAvalonia.Desktop;

sealed class Program
{
    private const string SingleInstanceMutexName = "KuGouMusicAvalonia.VibeMusic.SingleInstance";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            WriteCrashLog(e.ExceptionObject?.ToString() ?? "Unknown exception");
        };

        var isRestart = args is ["--restart"];

        using var singleInstanceMutex = new Mutex(false, SingleInstanceMutexName);
        var hasSingleInstance = false;

        if (isRestart)
        {
            for (var i = 0; i < 50; i++)
            {
                hasSingleInstance = TryAcquireSingleInstance(singleInstanceMutex);
                if (hasSingleInstance)
                {
                    break;
                }
                Thread.Sleep(100);
            }
        }
        else
        {
            hasSingleInstance = TryAcquireSingleInstance(singleInstanceMutex);
        }

        if (!hasSingleInstance)
        {
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex.ToString());
            throw;
        }
        finally
        {
            if (hasSingleInstance)
            {
                singleInstanceMutex.ReleaseMutex();
            }
        }
    }

    private static bool TryAcquireSingleInstance(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    private static void WriteCrashLog(string content)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VibeMusic");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "crash.txt"), content);
        }
        catch
        {
            // Never let crash logging hide the original startup failure.
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        PlatformAudioStorage.Initialize(new FileSystemAudioStorage(() => MusicService.DownloadDirectory));
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .UseLuminaUIDiagnostics()
#endif
            .WithInterFont()
            .LogToTrace();
    }

}
