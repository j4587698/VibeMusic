using Android.Content;

namespace KuGouMusicAvalonia.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(new[]
{
    AndroidMediaControlManager.ActionTogglePlayPause,
    AndroidMediaControlManager.ActionNext,
    AndroidMediaControlManager.ActionPrevious
})]
internal sealed class MediaControlBroadcastReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        AndroidMediaControlManager.Instance.Initialize(context ?? Android.App.Application.Context);
        AndroidMediaControlManager.Instance.HandleAction(intent?.Action);
    }
}
