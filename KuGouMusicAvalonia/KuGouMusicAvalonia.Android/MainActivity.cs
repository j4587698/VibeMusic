using Android.App;
using Android;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;

namespace KuGouMusicAvalonia.Android;

[Activity(
    Label = "VibeMusic",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        EnsureNotificationPermission();
        AndroidMediaControlManager.Instance.Initialize(this);
    }

    protected override void OnDestroy()
    {
        AndroidMediaControlManager.Instance.Dispose();
        base.OnDestroy();
    }

    private void EnsureNotificationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
        {
            return;
        }

        if (CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(new[] { Manifest.Permission.PostNotifications }, 1001);
        }
    }
}
