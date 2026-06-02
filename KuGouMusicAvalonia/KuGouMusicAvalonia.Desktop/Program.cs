using System;
using Avalonia;

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
            System.IO.File.WriteAllText(@"C:\Users\j4587698\.gemini\antigravity\brain\647d1f64-1d56-4bc9-95ce-d037914ad6f8\scratch\crash.txt", e.ExceptionObject?.ToString() ?? "Unknown exception");
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(@"C:\Users\j4587698\.gemini\antigravity\brain\647d1f64-1d56-4bc9-95ce-d037914ad6f8\scratch\crash.txt", ex.ToString());
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
