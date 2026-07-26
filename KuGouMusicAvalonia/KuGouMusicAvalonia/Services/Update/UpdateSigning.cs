using System;
using System.Security.Cryptography;

namespace KuGouMusicAvalonia.Services.Update;

/// <summary>
/// 更新清单的签名校验。使用 ECDsa P-256 + SHA256：BCL 内置、NativeAOT 干净、
/// 全平台（含 Android/iOS）零 native 依赖。
/// </summary>
/// <remarks>
/// 信任根是内置公钥而非传输通道，因此允许清单与产物走任意镜像源：
/// 镜像站即使被入侵，篡改后的清单过不了验签，篡改后的产物过不了 SHA256。
/// </remarks>
public static class UpdateSigning
{
    /// <summary>
    /// 签名公钥（SubjectPublicKeyInfo 的 base64）。由 CI 侧私钥配对生成。
    /// 未配置时更新功能降级为「仅提示 + 手动下载」，不会执行任何自动安装。
    /// </summary>
    private const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAESke67iZPXHy0cyxt8SvBWVP6pGD47ueIwYDAJyNR4rKNzRyXdv1ca7V2sh5PkCyD8kerCpjaohkDgDEhidA02w==";

    /// <summary>是否已配置签名公钥。未配置时禁止一切自动安装路径。</summary>
    public static bool IsConfigured => PublicKeyBase64.Length > 0;

    /// <summary>校验清单原始字节的签名。未配置公钥时一律返回 false。</summary>
    public static bool Verify(ReadOnlySpan<byte> payload, string? signatureBase64)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(signatureBase64))
        {
            return false;
        }

        byte[] signature;
        byte[] publicKey;
        try
        {
            signature = Convert.FromBase64String(signatureBase64.Trim());
            publicKey = Convert.FromBase64String(PublicKeyBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
