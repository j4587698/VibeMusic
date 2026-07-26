using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using KuGouMusicAvalonia.Services;
using KuGouMusicAvalonia.Services.Update;

namespace KuGouMusicAvalonia.Desktop;

/// <summary>
/// 桌面端安装策略：把下载好的 zip 解压到暂存目录，然后交给独立的 updater 进程做目录替换。
/// 主进程无法替换自身所在目录（Windows 上运行中的 exe 与已加载的 native dll 不可删除），
/// 因此必须由外部进程在主进程退出后完成。
/// </summary>
internal sealed class DesktopUpdateInstaller : IUpdateInstaller
{
    private const string UpdaterFileName = "VibeMusic.Updater";

    /// <summary>解压后的总大小上限，防解压炸弹。</summary>
    private const long MaxExtractedBytes = 2L * 1024 * 1024 * 1024;

    public bool CanInstallAutomatically => File.Exists(UpdaterPath) && IsInstallDirectoryWritable();

    private static string InstallDirectory => AppContext.BaseDirectory;

    private static string UpdaterPath =>
        Path.Combine(InstallDirectory, OperatingSystem.IsWindows() ? UpdaterFileName + ".exe" : UpdaterFileName);

    public async Task<bool> InstallAsync(UpdateInstallRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!CanInstallAutomatically)
        {
            return false;
        }

        var staging = UpdatePaths.StagingDirectory;
        PrepareEmptyDirectory(staging);

        await Task.Run(
            () => Extract(request.DownloadedFilePath, staging, request.Asset.Kind),
            cancellationToken).ConfigureAwait(false);

        var relaunch = ResolveRelaunchTarget(request.Asset.Executable);
        if (!File.Exists(Path.Combine(staging, relaunch)) && !Directory.Exists(Path.Combine(staging, relaunch)))
        {
            throw new InvalidOperationException("更新包内容不完整，缺少主程序文件。");
        }

        var arguments =
            $"--pid {Environment.ProcessId} " +
            $"--staging \"{staging}\" " +
            $"--target \"{Path.TrimEndingDirectorySeparator(InstallDirectory)}\" " +
            $"--relaunch \"{relaunch}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = UpdaterPath,
            Arguments = arguments,
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = false
        });

        // updater 会等待本进程退出后才动手。
        PlatformApplicationService.TryExitApplication();
        return true;
    }

    private static void Extract(string archivePath, string destination, string kind)
    {
        // macOS 的 .app bundle 含符号链接与权限位，ZipFile 会破坏结构，必须交给 ditto。
        if (OperatingSystem.IsMacOS() && kind == UpdateAssetKinds.AppZip)
        {
            ExtractWithDitto(archivePath, destination);
            return;
        }

        using var archive = ZipFile.OpenRead(archivePath);
        var fullDestination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        var totalBytes = 0L;

        foreach (var entry in archive.Entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(fullDestination, entry.FullName));

            // Zip Slip 防护：解析后的路径必须仍在目标目录内。
            if (!targetPath.StartsWith(fullDestination + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("更新包包含非法路径条目，已终止解压。");
            }

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            totalBytes += entry.Length;
            if (totalBytes > MaxExtractedBytes)
            {
                throw new InvalidOperationException("更新包解压后体积异常，已终止。");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
            RestoreUnixPermissions(entry, targetPath);
        }
    }

    private static void ExtractWithDitto(string archivePath, string destination)
    {
        Directory.CreateDirectory(destination);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ditto",
            ArgumentList = { "-x", "-k", archivePath, destination },
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("无法启动 ditto 解压更新包。");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"解压更新包失败（ditto 退出码 {process.ExitCode}）。");
        }

        // 网络下载的内容带 quarantine 属性，不清理会被 Gatekeeper 判为「已损坏」。
        TryRun("xattr", ["-dr", "com.apple.quarantine", destination]);
    }

    /// <summary>
    /// ZipFile 不会还原 Unix 权限位，直接解压出来的主程序与 .so 没有执行位。
    /// 权限存放在 zip entry 外部属性的高 16 位。
    /// </summary>
    private static void RestoreUnixPermissions(ZipArchiveEntry entry, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = (entry.ExternalAttributes >> 16) & 0x1FF;
        if (mode == 0)
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(targetPath, (UnixFileMode)mode);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ResolveRelaunchTarget(string? executable)
    {
        if (!string.IsNullOrWhiteSpace(executable))
        {
            return executable;
        }

        return OperatingSystem.IsWindows() ? "VibeMusic.exe" : "VibeMusic";
    }

    private static bool IsInstallDirectoryWritable()
    {
        try
        {
            var probe = Path.Combine(InstallDirectory, $".write-probe-{Environment.ProcessId}");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void PrepareEmptyDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);
    }

    private static void TryRun(string fileName, string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo { FileName = fileName, UseShellExecute = false };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch (Exception)
        {
            // 尽力而为，失败不影响主流程。
        }
    }
}
