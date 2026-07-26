using System;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services.Update;

public enum UpdateCheckStatus
{
    /// <summary>已是最新版本。</summary>
    UpToDate,

    /// <summary>发现新版本。</summary>
    UpdateAvailable,

    /// <summary>清单里没有当前平台的产物，或当前平台不支持自更新（如 iOS）。</summary>
    Unsupported,

    /// <summary>所有源都拉取失败，或清单验签失败。</summary>
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    string CurrentVersion,
    string? LatestVersion = null,
    string? ReleaseNotes = null,
    bool IsMandatory = false,
    UpdateAsset? Asset = null,
    string? Message = null)
{
    public bool HasUpdate => Status == UpdateCheckStatus.UpdateAvailable && Asset is not null;
}

public sealed record UpdateInstallRequest(
    UpdateAsset Asset,
    string DownloadedFilePath,
    string Version);

/// <summary>平台相关的安装动作。共享层只负责下载与校验，落地方式由各平台实现。</summary>
public interface IUpdateInstaller
{
    /// <summary>该平台能否在应用内完成安装。iOS 恒为 false。</summary>
    bool CanInstallAutomatically { get; }

    /// <summary>
    /// 执行安装。传入的文件已完成 SHA256 校验。
    /// 桌面端会启动外部 updater 并请求退出应用；Android 端会拉起系统安装器。
    /// </summary>
    Task<bool> InstallAsync(UpdateInstallRequest request, CancellationToken cancellationToken);
}

/// <summary>平台安装器注册点，沿用 <see cref="PlatformAudioStorage"/> 的注入模式。</summary>
public static class PlatformUpdateInstaller
{
    private static IUpdateInstaller? _current;

    /// <summary>是否具备应用内安装能力。未注册实现时为 false。</summary>
    public static bool CanInstallAutomatically => _current?.CanInstallAutomatically ?? false;

    public static void Initialize(IUpdateInstaller installer)
    {
        _current = installer ?? throw new ArgumentNullException(nameof(installer));
    }

    public static Task<bool> InstallAsync(UpdateInstallRequest request, CancellationToken cancellationToken)
    {
        var installer = _current;
        return installer is null
            ? Task.FromResult(false)
            : installer.InstallAsync(request, cancellationToken);
    }
}
