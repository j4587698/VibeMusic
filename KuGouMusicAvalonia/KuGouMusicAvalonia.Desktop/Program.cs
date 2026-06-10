using System;
using System.IO;
using Avalonia;
using LuminaUI.Diagnostics;

namespace KuGouMusicAvalonia.Desktop;

sealed class Program
{
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

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex.ToString());
            throw;
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
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .UseLuminaUIDiagnostics(options => options.StartImmediately = false)
            .AfterSetup(_ => StartLuminaDiagnostics())
#endif
            .WithInterFont()
            .LogToTrace();

#if DEBUG
    private static void StartLuminaDiagnostics()
    {
        var diagnosticsHost = LuminaUIDiagnosticsExtensions.GetLuminaUIDiagnosticsHost();
        if (diagnosticsHost is { IsRunning: false })
        {
            diagnosticsHost.Start();
        }
    }
#endif
}
