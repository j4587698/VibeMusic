using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace KuGouMusicAvalonia.Android;

[Service(Enabled = true, Exported = false, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
internal sealed class PlaybackNotificationService : Service
{
    public class PlaybackBinder : Binder
    {
        public PlaybackNotificationService Service { get; }
        public PlaybackBinder(PlaybackNotificationService service)
        {
            Service = service;
        }
    }

    public override IBinder? OnBind(Intent? intent) => new PlaybackBinder(this);

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

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            StartForeground(
                AndroidMediaControlManager.NotificationId,
                notification,
                ForegroundService.TypeMediaPlayback);
        }
        else
        {
            StartForeground(AndroidMediaControlManager.NotificationId, notification);
        }

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
