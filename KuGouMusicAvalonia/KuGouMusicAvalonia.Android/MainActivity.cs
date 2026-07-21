using Android.App;
using Android;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using Android.Content;
using KuGouMusicAvalonia.Services;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("KuGouMusicAvalonia.Android")]

namespace KuGouMusicAvalonia.Android;

[Activity(
    Label = "Vibe Music",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity, IServiceConnection
{
    private const int NotificationPermissionRequestCode = 1001;
    private bool _isBound;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        PlatformApplicationService.ExitApplication = FinishAndRemoveTask;

        var musicDir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryMusic);
        if (musicDir is not null)
        {
            PlatformStoragePaths.ExternalDownloadsDirectory =
                Path.Combine(musicDir.AbsolutePath, "VibeMusic");
            Directory.CreateDirectory(PlatformStoragePaths.ExternalDownloadsDirectory);
        }

        EnsureNotificationPermission();
        AndroidMediaControlManager.Instance.Initialize(this);
        AndroidFloatingLyricsController.Instance.Initialize(this);
        FloatingLyricsService.Instance.RestorePersistedState();

        var intent = new Intent(this, typeof(PlaybackNotificationService));
        BindService(intent, this, Bind.AutoCreate);
    }

    protected override void OnResume()
    {
        base.OnResume();
        AndroidFloatingLyricsController.Instance.RefreshOverlayPermission();
        AndroidMediaControlManager.Instance.SyncNowPlayingNotification();
    }

    protected override void OnDestroy()
    {
        PlatformApplicationService.ExitApplication = null;
        if (_isBound)
        {
            UnbindService(this);
            _isBound = false;
        }
        AndroidFloatingLyricsController.Instance.Dispose();
        base.OnDestroy();
    }

    public void OnServiceConnected(ComponentName? name, IBinder? service)
    {
        _isBound = true;
    }

    public void OnServiceDisconnected(ComponentName? name)
    {
        _isBound = false;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == NotificationPermissionRequestCode
            && grantResults.Length > 0
            && grantResults[0] == Permission.Granted)
        {
            AndroidMediaControlManager.Instance.SyncNowPlayingNotification();
        }
    }

    private void EnsureNotificationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
        {
            return;
        }

        if (CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(new[] { Manifest.Permission.PostNotifications }, NotificationPermissionRequestCode);
        }
    }
}
