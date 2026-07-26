using Android.App;
using Android;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using Android.Content;
using KuGouMusicAvalonia.Services;
using KuGouMusicAvalonia.Services.Update;
using System;
using System.Linq;
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
    private const int StoragePermissionRequestCode = 1002;
    private bool _isBound;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        PlatformStoragePaths.ExternalDownloadsDirectory = "Music/VibeMusic";
        PlatformAudioStorage.Initialize(new AndroidMediaStoreAudioStorage(this));
        InitializeUpdateSupport();

        base.OnCreate(savedInstanceState);
        PlatformApplicationService.ExitApplication = FinishAndRemoveTask;

        EnsureNotificationPermission();
        EnsureLegacyStoragePermission();
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

    private void EnsureLegacyStoragePermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            return;
        }

        if (CheckSelfPermission(Manifest.Permission.WriteExternalStorage) != Permission.Granted)
        {
            RequestPermissions(new[] { Manifest.Permission.WriteExternalStorage }, StoragePermissionRequestCode);
        }
    }

    /// <summary>
    /// 注入 ABI 与 versionCode 供更新清单匹配，并注册 Android 安装器。
    /// ABI 顺序决定了下载哪个变体：64 位设备优先 arm64-v8a，失败时回退 armeabi-v7a。
    /// </summary>
    private void InitializeUpdateSupport()
    {
        var abis = Build.SupportedAbis?.Where(abi => !string.IsNullOrWhiteSpace(abi)).ToArray();
        if (abis is not { Length: > 0 })
        {
            abis = [Build.CpuAbi ?? "armeabi-v7a"];
        }

        UpdatePlatform.Initialize(abis!, ResolveVersionCode());
        PlatformUpdateInstaller.Initialize(new AndroidUpdateInstaller(this));
    }

    private long ResolveVersionCode()
    {
        try
        {
            var info = PackageManager?.GetPackageInfo(PackageName!, PackageInfoFlags.MetaData);
            if (info is null)
            {
                return 0;
            }

#pragma warning disable CA1422 // VersionCode 在 API 28 以下是唯一可用的读取方式
            return OperatingSystem.IsAndroidVersionAtLeast(28) ? info.LongVersionCode : info.VersionCode;
#pragma warning restore CA1422
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
