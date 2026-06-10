using Android.App;
using Android;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using Android.Content;

namespace KuGouMusicAvalonia.Android;

[Activity(
    Label = "VibeMusic",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity, IServiceConnection
{
    private bool _isBound;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        EnsureNotificationPermission();
        AndroidMediaControlManager.Instance.Initialize(this);
        AndroidFloatingLyricsController.Instance.Initialize(this);

        var intent = new Intent(this, typeof(PlaybackNotificationService));
        BindService(intent, this, Bind.AutoCreate);
    }

    protected override void OnResume()
    {
        base.OnResume();
        AndroidFloatingLyricsController.Instance.RefreshOverlayPermission();
    }

    protected override void OnDestroy()
    {
        if (_isBound)
        {
            UnbindService(this);
            _isBound = false;
        }
        AndroidFloatingLyricsController.Instance.Dispose();
        AndroidMediaControlManager.Instance.Dispose();
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
