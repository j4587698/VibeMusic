using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KuGouMusicAvalonia.Services.Update;

/// <summary>更新资产的分发形态。</summary>
public static class UpdateAssetKinds
{
    /// <summary>桌面便携 zip 包，解压后整目录替换。</summary>
    public const string Zip = "zip";

    /// <summary>macOS 的 .app bundle zip，需要保留符号链接与权限。</summary>
    public const string AppZip = "app-zip";

    /// <summary>Android 安装包，交由系统安装器覆盖安装。</summary>
    public const string Apk = "apk";
}

/// <summary>清单中的单个平台产物。</summary>
public sealed class UpdateAsset
{
    /// <summary>桌面为 RID（win-x64 等），Android 为 ABI（arm64-v8a 等）。</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>见 <see cref="UpdateAssetKinds"/>。</summary>
    public string Kind { get; set; } = string.Empty;

    public string? FileName { get; set; }

    /// <summary>
    /// 按顺序尝试的下载地址，第一个可用的生效。国内镜像应排在前面，GitHub 作为兜底。
    /// 所有地址都在签名保护的清单内，因此镜像源无法被中途插入。
    /// </summary>
    public List<string> Urls { get; set; } = [];

    public long Size { get; set; }

    /// <summary>产物的 SHA256（十六进制，大小写不敏感）。</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>桌面端替换完成后用于重启的可执行文件相对路径。</summary>
    public string? Executable { get; set; }
}

/// <summary>更新清单（latest.json）。</summary>
public sealed class UpdateManifest
{
    public int Schema { get; set; } = 1;

    public string Version { get; set; } = string.Empty;

    /// <summary>Android versionCode，桌面端忽略。</summary>
    public long VersionCode { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>为 true 时不允许用户跳过本次更新。</summary>
    public bool Mandatory { get; set; }

    /// <summary>低于该版本时无法自动升级，需引导用户手动重装。</summary>
    public string? MinSupportedVersion { get; set; }

    public string? ReleaseNotes { get; set; }

    public List<UpdateAsset> Assets { get; set; } = [];

    /// <summary>挑选与当前运行平台匹配的产物，找不到时返回 null。</summary>
    public UpdateAsset? FindAsset(IReadOnlyList<string> platformCandidates)
    {
        foreach (var candidate in platformCandidates)
        {
            foreach (var asset in Assets)
            {
                if (string.Equals(asset.Platform, candidate, StringComparison.OrdinalIgnoreCase)
                    && asset.Urls.Count > 0)
                {
                    return asset;
                }
            }
        }

        return null;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UpdateManifest))]
internal sealed partial class UpdateManifestJsonContext : JsonSerializerContext;
