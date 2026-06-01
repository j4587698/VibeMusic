using Android.App;
using Android.Content;
using Android.OS;

namespace KuGouMusicAvalonia.Android;

[Service(Enabled = true, Exported = false)]
internal sealed class PlaybackNotificationService : Service
{
    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        AndroidMediaControlManager.Instance.Initialize(this);

        var action = intent?.Action;
        if (action == AndroidMediaControlManager.ActionStopForeground)
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        var notification = AndroidMediaControlManager.Instance.BuildNotificationForForegroundService();
        if (notification is null)
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        StartForeground(AndroidMediaControlManager.NotificationId, notification);
        return StartCommandResult.Sticky;
    }

    internal static void RequestSync(Context context)
    {
        var intent = new Intent(context, typeof(PlaybackNotificationService));
        intent.SetAction(AndroidMediaControlManager.ActionSyncForeground);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }

    internal static void RequestStop(Context context)
    {
        var intent = new Intent(context, typeof(PlaybackNotificationService));
        intent.SetAction(AndroidMediaControlManager.ActionStopForeground);
        context.StartService(intent);
    }
}
