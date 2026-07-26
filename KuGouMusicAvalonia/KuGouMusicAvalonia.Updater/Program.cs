using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace VibeMusic.Updater;

/// <summary>
/// 独立的更新落地进程。职责极窄：等待主进程退出 → 用已解压好的新版本替换安装目录 → 重启主程序。
/// 它不联网、不下载、不解析清单，因此攻击面最小。
/// </summary>
internal static class Program
{
    /// <summary>备份目录必须与安装目录同卷，否则「重命名占用中的文件」会退化为复制+删除而失败。</summary>
    private const string BackupFolderName = ".update-backup";

    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(30);

    private static string? _logPath;

    private static int Main(string[] args)
    {
        var options = UpdaterOptions.Parse(args);
        if (options is null)
        {
            return 2;
        }

        _logPath = Path.Combine(Path.GetDirectoryName(options.StagingDirectory) ?? Path.GetTempPath(), "update.log");
        Log($"updater started, pid={options.ProcessId}, target={options.TargetDirectory}");

        // updater 自身位于安装目录内，无法替换自己，先把自己搬到临时目录再重来。
        if (!options.SelfCopied && IsInsideDirectory(AppContext.BaseDirectory, options.TargetDirectory))
        {
            return RelaunchFromTempDirectory(options);
        }

        if (!WaitForProcessExit(options.ProcessId))
        {
            Log("main process did not exit in time, aborting");
            Relaunch(options, "主程序未能退出，更新已取消");
            return 3;
        }

        var backupDirectory = Path.Combine(options.TargetDirectory, BackupFolderName);

        try
        {
            if (!Directory.Exists(options.StagingDirectory)
                || !HasAnyEntry(options.StagingDirectory))
            {
                throw new InvalidOperationException("暂存目录为空。");
            }

            PrepareEmptyDirectory(backupDirectory);

            Log("moving current files to backup");
            MoveDirectoryContents(options.TargetDirectory, backupDirectory, [BackupFolderName]);

            Log("moving new files into place");
            MoveDirectoryContents(options.StagingDirectory, options.TargetDirectory, []);

            Log("update applied, cleaning backup");
            TryDeleteDirectory(backupDirectory);
            TryDeleteDirectory(options.StagingDirectory);

            Relaunch(options, null);
            return 0;
        }
        catch (Exception ex)
        {
            Log($"update failed: {ex}");
            TryRollback(options.TargetDirectory, backupDirectory);
            Relaunch(options, "更新失败，已恢复到原版本");
            return 1;
        }
    }

    private static int RelaunchFromTempDirectory(UpdaterOptions options)
    {
        try
        {
            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "VibeMusic.Updater." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("VibeMusic.Updater", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(file, Path.Combine(tempDirectory, name), overwrite: true);
                }
            }

            var executable = Path.Combine(tempDirectory, Path.GetFileName(Environment.ProcessPath ?? "VibeMusic.Updater"));
            if (!File.Exists(executable))
            {
                Log("failed to stage updater copy");
                return 4;
            }

            MakeExecutable(executable);

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = options.ToArguments(selfCopied: true),
                UseShellExecute = false
            });

            return 0;
        }
        catch (Exception ex)
        {
            Log($"self-copy failed: {ex}");
            return 4;
        }
    }

    private static bool WaitForProcessExit(int processId)
    {
        if (processId <= 0)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.WaitForExit((int)ProcessExitTimeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // 进程已经不存在，视为已退出。
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>
    /// 逐文件移动而非 <see cref="Directory.Move"/>：后者跨卷会直接抛异常，
    /// 而暂存目录与安装目录经常不在同一个卷上。
    /// </summary>
    private static void MoveDirectoryContents(string source, string destination, IReadOnlyList<string> excludedNames)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (IsExcluded(name, excludedNames))
            {
                continue;
            }

            MoveDirectoryContents(directory, Path.Combine(destination, name), []);
            TryDeleteDirectory(directory);
        }

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var name = Path.GetFileName(file);
            if (IsExcluded(name, excludedNames))
            {
                continue;
            }

            var targetPath = Path.Combine(destination, name);
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(file, targetPath);
        }
    }

    private static void TryRollback(string targetDirectory, string backupDirectory)
    {
        if (!Directory.Exists(backupDirectory))
        {
            return;
        }

        try
        {
            Log("rolling back");
            MoveDirectoryContents(backupDirectory, targetDirectory, []);
            TryDeleteDirectory(backupDirectory);
        }
        catch (Exception ex)
        {
            Log($"rollback failed: {ex}");
        }
    }

    private static void Relaunch(UpdaterOptions options, string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(options.RelaunchExecutable))
        {
            return;
        }

        var executable = Path.IsPathRooted(options.RelaunchExecutable)
            ? options.RelaunchExecutable
            : Path.Combine(options.TargetDirectory, options.RelaunchExecutable);

        try
        {
            MakeExecutable(executable);

            if (OperatingSystem.IsMacOS() && executable.Contains(".app/", StringComparison.Ordinal))
            {
                var bundle = executable[..(executable.IndexOf(".app/", StringComparison.Ordinal) + 4)];
                Process.Start(new ProcessStartInfo { FileName = "open", Arguments = $"-a \"{bundle}\"", UseShellExecute = false });
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = failureReason is null ? string.Empty : $"--update-failed \"{failureReason}\"",
                WorkingDirectory = options.TargetDirectory,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Log($"relaunch failed: {ex}");
        }
    }

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch (Exception ex)
        {
            Log($"chmod failed for {path}: {ex.Message}");
        }
    }

    private static bool IsExcluded(string name, IReadOnlyList<string> excludedNames)
    {
        foreach (var excluded in excludedNames)
        {
            if (string.Equals(name, excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideDirectory(string candidate, string directory)
    {
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

        // 必须比到分隔符，否则 "C:\App2" 会被误判为在 "C:\App" 之内。
        return normalizedCandidate.Equals(normalizedDirectory, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(
                normalizedDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyEntry(string directory)
    {
        using var entries = Directory.EnumerateFileSystemEntries(directory).GetEnumerator();
        return entries.MoveNext();
    }

    private static void PrepareEmptyDirectory(string directory)
    {
        TryDeleteDirectory(directory);
        Directory.CreateDirectory(directory);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Log(string message)
    {
        if (_logPath is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed record UpdaterOptions(
    int ProcessId,
    string StagingDirectory,
    string TargetDirectory,
    string RelaunchExecutable,
    bool SelfCopied)
{
    public static UpdaterOptions? Parse(string[] args)
    {
        var processId = 0;
        string? staging = null;
        string? target = null;
        var relaunch = string.Empty;
        var selfCopied = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pid" when i + 1 < args.Length:
                    int.TryParse(args[++i], out processId);
                    break;
                case "--staging" when i + 1 < args.Length:
                    staging = args[++i];
                    break;
                case "--target" when i + 1 < args.Length:
                    target = args[++i];
                    break;
                case "--relaunch" when i + 1 < args.Length:
                    relaunch = args[++i];
                    break;
                case "--self-copied":
                    selfCopied = true;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(staging) || string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        return new UpdaterOptions(
            processId,
            Path.GetFullPath(staging),
            Path.GetFullPath(target),
            relaunch,
            selfCopied);
    }

    public string ToArguments(bool selfCopied)
    {
        var flag = selfCopied ? " --self-copied" : string.Empty;
        return $"--pid {ProcessId} --staging \"{StagingDirectory}\" --target \"{TargetDirectory}\" --relaunch \"{RelaunchExecutable}\"{flag}";
    }
}
