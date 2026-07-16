using System;

namespace KuGouMusicAvalonia.Services;

public static class PlatformApplicationService
{
    public static Action? ExitApplication { get; set; }

    public static bool TryExitApplication()
    {
        Action? exitApplication = ExitApplication;
        if (exitApplication == null)
        {
            return false;
        }

        exitApplication();
        return true;
    }
}
