using KuGou.Lite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.Services;

public sealed class VipPrivilegeService
{
    public static VipPrivilegeService Instance { get; } = new();

    private bool _vipEnsureAttemptedThisSession;
    private bool _listenActiveKnown;
    private bool _conceptVipActiveKnown;

    private VipPrivilegeService()
    {
    }

    public bool AutoReceiveBeforePlayback
    {
        get => MusicService.AutoReceiveVipBeforePlayback;
        set => MusicService.AutoReceiveVipBeforePlayback = value;
    }

    public void ResetSessionState()
    {
        _vipEnsureAttemptedThisSession = false;
        _listenActiveKnown = false;
        _conceptVipActiveKnown = false;
    }

    public async Task<VipPrivilegeStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!HasSavedLogin())
        {
            _listenActiveKnown = false;
            _conceptVipActiveKnown = false;
            return VipPrivilegeStatus.NotLoggedIn;
        }

        var loginFresh = await EnsureLoginFreshAsync(cancellationToken).ConfigureAwait(false);
        if (!loginFresh)
        {
            _listenActiveKnown = false;
            _conceptVipActiveKnown = false;
            return new VipPrivilegeStatus("VIP状态：登录已过期或未登录", false, false);
        }

        var response = await MusicService.Client.GetConceptVipDetailAsync(cancellationToken).ConfigureAwait(false);
        MusicService.SaveSession();
        return UpdateFromResponse(response);
    }

    public async Task<VipPrivilegeStatus> EnsureManuallyAsync(CancellationToken cancellationToken = default)
    {
        if (!HasSavedLogin())
        {
            return VipPrivilegeStatus.NotLoggedIn;
        }

        var loginFresh = await EnsureLoginFreshAsync(cancellationToken).ConfigureAwait(false);
        if (!loginFresh)
        {
            return new VipPrivilegeStatus("VIP状态：登录已过期或未登录", false, false);
        }

        var result = await MusicService.Client.EnsureConceptVipAsync(cancellationToken).ConfigureAwait(false);
        MusicService.SaveSession();
        _vipEnsureAttemptedThisSession = true;
        _conceptVipActiveKnown = result.IsVipBefore || result.IsVipAfter;

        var status = UpdateFromResponse(result.VipAfter ?? result.VipBefore);
        return status with
        {
            Message = $"今日权益：{FormatActionResult(result)}；{status.Message}",
            Detail = string.Empty
        };
    }

    public async Task<VipPrivilegeStatus> EnsureBeforePlaybackAsync(CancellationToken cancellationToken = default)
    {
        if (!AutoReceiveBeforePlayback)
        {
            return new VipPrivilegeStatus("已关闭播放前自动领取畅听/VIP", _listenActiveKnown || _conceptVipActiveKnown, true);
        }

        if (!HasSavedLogin())
        {
            return new VipPrivilegeStatus("未登录，跳过畅听/VIP自动领取", false, true);
        }

        var loginFresh = await EnsureLoginFreshAsync(cancellationToken).ConfigureAwait(false);
        if (!loginFresh)
        {
            return new VipPrivilegeStatus("登录已过期，跳过畅听/VIP自动领取", false, false);
        }

        if (_conceptVipActiveKnown)
        {
            return new VipPrivilegeStatus("畅听/VIP权益已生效", true, true);
        }

        if (_vipEnsureAttemptedThisSession)
        {
            return new VipPrivilegeStatus("本次启动已尝试过畅听/VIP自动领取，继续播放", _listenActiveKnown || _conceptVipActiveKnown, true);
        }

        _vipEnsureAttemptedThisSession = true;
        try
        {
            var result = await MusicService.Client.EnsureConceptVipAsync(cancellationToken).ConfigureAwait(false);
            MusicService.SaveSession();
            _conceptVipActiveKnown = result.IsVipBefore || result.IsVipAfter;

            var active = result.IsVipBefore || result.IsVipAfter;
            var message = result.IsVipBefore
                ? "畅听/VIP权益已生效"
                : $"已尝试自动领取畅听/VIP：{FormatActionResult(result)}";
            return new VipPrivilegeStatus(message, active, true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new VipPrivilegeStatus($"畅听/VIP自动领取失败，继续尝试播放：{ex.Message}", false, false, ex.ToString());
        }
    }

    public async Task<bool> EnsureLoginFreshAsync(CancellationToken cancellationToken = default)
    {
        var state = MusicService.Client.GetLoginState();
        if (!state.IsLoggedIn || state.IsExpired)
        {
            return false;
        }

        if (!state.ShouldRefresh)
        {
            return true;
        }

        try
        {
            var response = await MusicService.Client.RefreshTokenAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            MusicService.SaveSession();
            if (KugouLiteClient.IsAuthExpiredResponse(response))
            {
                return false;
            }

            return !MusicService.Client.GetLoginState().IsExpired;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return true;
        }
    }

    private VipPrivilegeStatus UpdateFromResponse(KugouResponse? response)
    {
        var infos = ParseVipDisplayInfos(response);
        var tvip = SelectVipInfo(infos, "tvip", "dvip", "qvip");
        var svip = SelectVipInfo(infos, "svip");
        _listenActiveKnown = IsVipInfoActive(tvip);
        _conceptVipActiveKnown = IsVipInfoActive(svip);
        var active = _listenActiveKnown || _conceptVipActiveKnown;

        return new VipPrivilegeStatus(
            $"畅听：{FormatVipInfo(tvip)}；VIP：{FormatVipInfo(svip)}",
            active,
            true);
    }

    private static bool HasSavedLogin()
    {
        var client = MusicService.Client;
        return !string.IsNullOrWhiteSpace(client.CookieStore.Get("token")) &&
            !string.IsNullOrWhiteSpace(client.CookieStore.Get("userid")) &&
            !string.Equals(client.CookieStore.Get("userid"), "0", StringComparison.Ordinal);
    }

    private static string FormatActionResult(KugouConceptVipEnsureResult result)
    {
        if (result.IsVipBefore)
        {
            return "已生效";
        }

        var claim = result.ClaimSucceeded ? "畅听已获取" : "畅听未确认";
        var upgrade = result.UpgradeSucceeded ? "VIP已获取" : "VIP未确认";
        return $"{claim}，{upgrade}";
    }

    private static IReadOnlyList<VipDisplayInfo> ParseVipDisplayInfos(KugouResponse? response)
    {
        if (response is null)
        {
            return Array.Empty<VipDisplayInfo>();
        }

        using var doc = response.TryParseJson();
        if (doc is null)
        {
            return Array.Empty<VipDisplayInfo>();
        }

        var result = new List<VipDisplayInfo>();
        CollectVipDisplayInfos(doc.RootElement, result);
        return result;
    }

    private static void CollectVipDisplayInfos(JsonElement element, List<VipDisplayInfo> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var productType = ReadJsonString(element, "product_type") ?? ReadJsonString(element, "productType");
                if (productType is not null && IsKnownVipProductType(productType))
                {
                    var isVip = TryReadJsonInt(element, "is_vip", out var isVipValue) || TryReadJsonInt(element, "isVip", out isVipValue)
                        ? isVipValue == 1
                        : false;
                    result.Add(new VipDisplayInfo(productType.ToLowerInvariant(), isVip, ReadVipExpireTime(element)));
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectVipDisplayInfos(property.Value, result);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectVipDisplayInfos(item, result);
                }
                break;
        }
    }

    private static VipDisplayInfo? SelectVipInfo(IReadOnlyList<VipDisplayInfo> infos, params string[] productTypes)
    {
        return infos
            .Where(item => productTypes.Any(productType => item.ProductType.Equals(productType, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(IsVipInfoActive)
            .ThenByDescending(item => item.ExpireTime ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    private static bool IsKnownVipProductType(string productType) =>
        productType.Equals("tvip", StringComparison.OrdinalIgnoreCase) ||
        productType.Equals("dvip", StringComparison.OrdinalIgnoreCase) ||
        productType.Equals("qvip", StringComparison.OrdinalIgnoreCase) ||
        productType.Equals("svip", StringComparison.OrdinalIgnoreCase);

    private static bool IsVipInfoActive(VipDisplayInfo? info) =>
        info is not null && info.IsVip && (info.ExpireTime is null || info.ExpireTime > DateTimeOffset.Now);

    private static string FormatVipInfo(VipDisplayInfo? info)
    {
        if (info is null)
        {
            return "未返回";
        }

        var expireText = info.ExpireTime is null
            ? "已获取"
            : info.ExpireTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        if (IsVipInfoActive(info))
        {
            return info.ExpireTime is null ? "已获取" : $"到期 {expireText}";
        }

        return info.ExpireTime is null ? "未生效" : $"未生效/已过期（{expireText}）";
    }

    private static DateTimeOffset? ReadVipExpireTime(JsonElement element)
    {
        if (!TryGetJsonProperty(element, "vip_end_time", out var value) &&
            !TryGetJsonProperty(element, "vipEndTime", out value) &&
            !TryGetJsonProperty(element, "end_time", out value) &&
            !TryGetJsonProperty(element, "endTime", out value))
        {
            return null;
        }

        if (TryReadJsonLong(value, out var numeric) && numeric > 0)
        {
            var ms = numeric > 1_000_000_000_000 ? numeric : numeric * 1000;
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return !string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var parsed)
            ? parsed.ToLocalTime()
            : null;
    }

    private static bool TryGetJsonProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? ReadJsonString(JsonElement element, string name)
    {
        if (!TryGetJsonProperty(element, name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static bool TryReadJsonInt(JsonElement element, string name, out int value)
    {
        if (TryGetJsonProperty(element, name, out var property) && TryReadJsonLong(property, out var parsed))
        {
            value = (int)parsed;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadJsonLong(JsonElement element, out long value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetInt64(out value);
            case JsonValueKind.String:
                return long.TryParse(element.GetString(), out value);
            default:
                value = 0;
                return false;
        }
    }

    private static string Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= 1600 ? value : value[..1600] + "...";
    }

    private sealed record VipDisplayInfo(string ProductType, bool IsVip, DateTimeOffset? ExpireTime);
}

public sealed record VipPrivilegeStatus(string Message, bool IsActive, bool Completed, string Detail = "")
{
    public static VipPrivilegeStatus NotLoggedIn { get; } = new("VIP状态：未登录", false, false);
}