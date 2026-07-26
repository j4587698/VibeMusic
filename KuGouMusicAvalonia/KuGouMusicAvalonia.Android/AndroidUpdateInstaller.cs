using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using KuGouMusicAvalonia.Services.Update;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Android;

/// <summary>
/// Android 安装策略：APK 无法自我覆盖，只能把下载好的包交给系统安装器。
/// 覆盖安装的前提是签名一致，CI 使用固定 keystore，因此该前提成立。
/// </summary>
internal sealed class AndroidUpdateInstaller(Activity activity) : IUpdateInstaller
{
    private const int RequestInstallPermissionCode = 1010;

    private readonly Activity _activity = activity;

    public bool CanInstallAutomatically => true;

    public Task<bool> InstallAsync(UpdateInstallRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Android 8.0 起「未知来源安装」是按应用授权的，未授权时先把用户送到设置页。
        if (OperatingSystem.IsAndroidVersionAtLeast(26)
            && _activity.PackageManager?.CanRequestPackageInstalls() != true)
        {
            RequestInstallPermission();
            return Task.FromResult(false);
        }

        // 即便签名不同的 APK 本来就装不上，也要提前拦截，避免把可疑文件留在设备上并弹出安装框。
        if (!HasMatchingSignature(request.DownloadedFilePath))
        {
            TryDelete(request.DownloadedFilePath);
            throw new InvalidOperationException("更新包签名与当前应用不一致，已拒绝安装。");
        }

        LaunchInstaller(request.DownloadedFilePath);
        return Task.FromResult(true);
    }

    private void RequestInstallPermission()
    {
        var intent = new Intent(
            Settings.ActionManageUnknownAppSources,
            global::Android.Net.Uri.Parse("package:" + _activity.PackageName));
        _activity.StartActivityForResult(intent, RequestInstallPermissionCode);
    }

    private void LaunchInstaller(string apkPath)
    {
        // API 24 起不能再用 file:// 传递给其他应用，必须走 FileProvider 的 content:// URI。
        var uri = FileProvider.GetUriForFile(
            _activity,
            _activity.PackageName + ".fileprovider",
            new Java.IO.File(apkPath));

        var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, "application/vnd.android.package-archive");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
        _activity.StartActivity(intent);
    }

    private bool HasMatchingSignature(string apkPath)
    {
        try
        {
            var packageManager = _activity.PackageManager;
            if (packageManager is null)
            {
                return false;
            }

            // SigningInfo 需要 API 28+，更低版本只能用已废弃的 Signatures。
            var flags = OperatingSystem.IsAndroidVersionAtLeast(28)
                ? PackageInfoFlags.SigningCertificates
                : PackageInfoFlags.Signatures;

            var current = GetSignatureHashes(packageManager.GetPackageInfo(_activity.PackageName!, flags));
            var candidate = GetSignatureHashes(packageManager.GetPackageArchiveInfo(apkPath, flags));

            return current.Length > 0
                && candidate.Length > 0
                && candidate.All(hash => current.Contains(hash, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string[] GetSignatureHashes(PackageInfo? packageInfo)
    {
        if (packageInfo is null)
        {
            return [];
        }

        IEnumerable<Signature?>? signatures = null;

        if (OperatingSystem.IsAndroidVersionAtLeast(28) && packageInfo.SigningInfo is { } signingInfo)
        {
            signatures = signingInfo.HasMultipleSigners
                ? signingInfo.GetApkContentsSigners()
                : signingInfo.GetSigningCertificateHistory();
        }

#pragma warning disable CA1422 // 低版本 Android 只能读取已废弃的 Signatures
        signatures ??= packageInfo.Signatures;
#pragma warning restore CA1422

        if (signatures is null)
        {
            return [];
        }

        return signatures
            .Where(signature => signature is not null)
            .Select(signature => Convert.ToHexString(SHA256.HashData(signature!.ToByteArray()!)))
            .ToArray();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
