using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KuGouMusicAvalonia.Services.Update;

/// <summary>
/// 当前运行环境的版本与平台标识。桌面端可直接推导；Android 端的 ABI 与 versionCode
/// 需要平台 API，由 Android 项目在启动时通过 <see cref="Initialize"/> 注入。
/// </summary>
public static class UpdatePlatform
{
    private static IReadOnlyList<string>? _platformOverride;
    private static long _versionCodeOverride;

    /// <summary>当前应用版本，形如 1.2.0。</summary>
    public static string CurrentVersion { get; } = ResolveCurrentVersion();

    /// <summary>Android versionCode；桌面端恒为 0。</summary>
    public static long CurrentVersionCode => _versionCodeOverride;

    /// <summary>
    /// 与清单 <c>assets[].platform</c> 匹配的候选标识，按优先级排列。
    /// 例如 Android arm64 设备为 ["arm64-v8a", "armeabi-v7a"]。
    /// </summary>
    public static IReadOnlyList<string> PlatformCandidates =>
        _platformOverride ?? ResolveDesktopPlatforms();

    /// <summary>由 Android 平台项目在启动时调用，注入 ABI 列表与 versionCode。</summary>
    public static void Initialize(IReadOnlyList<string> platformCandidates, long versionCode)
    {
        ArgumentNullException.ThrowIfNull(platformCandidates);
        if (platformCandidates.Count == 0)
        {
            throw new ArgumentException("平台标识列表不能为空。", nameof(platformCandidates));
        }

        _platformOverride = platformCandidates;
        _versionCodeOverride = versionCode;
    }

    private static IReadOnlyList<string> ResolveDesktopPlatforms()
    {
        var os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : OperatingSystem.IsLinux() ? "linux"
            : null;

        if (os is null)
        {
            return [];
        }

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => null
        };

        if (arch is null)
        {
            return [];
        }

        // macOS 上 x64 产物可经 Rosetta 运行，作为 arm64 的兜底。
        return os == "osx" && arch == "arm64"
            ? ["osx-arm64", "osx-x64"]
            : [$"{os}-{arch}"];
    }

    private static string ResolveCurrentVersion()
    {
        // 使用 AssemblyName.Version 而非 InformationalVersion：前者不依赖自定义特性反射，
        // 在 NativeAOT / 裁剪后依然可靠。
        var version = typeof(UpdatePlatform).Assembly.GetName().Version;
        if (version is null)
        {
            return "0.0.0";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Major}.{Math.Max(version.Minor, 0)}.{Math.Max(version.Build, 0)}");
    }
}

/// <summary>版本号比较。仅取前三段数字，忽略预发布与构建元数据。</summary>
public static class UpdateVersion
{
    /// <summary>比较两个版本号，返回值语义同 <see cref="IComparable.CompareTo"/>。无法解析的一侧视为最小。</summary>
    public static int Compare(string? left, string? right)
    {
        var hasLeft = TryParse(left, out var leftVersion);
        var hasRight = TryParse(right, out var rightVersion);

        if (!hasLeft && !hasRight)
        {
            return 0;
        }

        if (!hasLeft)
        {
            return -1;
        }

        if (!hasRight)
        {
            return 1;
        }

        return leftVersion.CompareTo(rightVersion);
    }

    public static bool IsNewer(string? candidate, string? baseline) => Compare(candidate, baseline) > 0;

    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span.Length > 0 && (span[0] == 'v' || span[0] == 'V'))
        {
            span = span[1..];
        }

        // 截断预发布（-beta.1）与构建元数据（+abc123）
        var cut = span.IndexOfAny('-', '+');
        if (cut >= 0)
        {
            span = span[..cut];
        }

        Span<int> parts = [0, 0, 0];
        var index = 0;
        foreach (var range in span.Split('.'))
        {
            if (index >= 3)
            {
                break;
            }

            if (!int.TryParse(span[range], NumberStyles.None, CultureInfo.InvariantCulture, out var part))
            {
                return false;
            }

            parts[index++] = part;
        }

        if (index == 0)
        {
            return false;
        }

        version = new Version(parts[0], parts[1], parts[2]);
        return true;
    }
}
