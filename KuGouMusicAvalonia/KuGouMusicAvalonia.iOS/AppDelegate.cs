using Foundation;
using UIKit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Media;
using KuGouMusicAvalonia.Services;
using System.IO;

namespace KuGouMusicAvalonia.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        PlatformStoragePaths.ExternalDownloadsDirectory = Path.Combine(docPath, "VibeMusic");
        Directory.CreateDirectory(PlatformStoragePaths.ExternalDownloadsDirectory);
        PlatformAudioStorage.Initialize(new FileSystemAudioStorage(() => MusicService.DownloadDirectory));

        var result = base.FinishedLaunching(application, launchOptions);
        PlatformApplicationService.ExitApplication = () => Environment.Exit(0);
        IosMediaControlManager.Instance.Initialize();
        return result;
    }

    public override void WillTerminate(UIApplication application)
    {
        PlatformApplicationService.ExitApplication = null;
        IosMediaControlManager.Instance.Dispose();
        base.WillTerminate(application);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
