using KuGou.Lite;
using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace KuGouMusicAvalonia.Services;

public enum AuthVerifyResult
{
    /// <summary>未登录，无需校验。</summary>
    NotLoggedIn,
    /// <summary>服务端确认登录有效（刷新 token 成功）。</summary>
    Valid,
    /// <summary>服务端确认登录已失效，本地登录态已清除。</summary>
    Expired,
    /// <summary>无法确认（网络异常、接口返回未知错误等），保留本地登录态。</summary>
    Unknown
}

public sealed class AuthSessionExpiredEventArgs : EventArgs
{
    public AuthSessionExpiredEventArgs(string message)
    {
        Message = message;
    }

    public string Message { get; }
}

/// <summary>
/// 统一管理登录态的有效性：
/// 1. 观察所有 SDK 响应，发现疑似登录失效的错误后，用刷新 token 接口向服务端确认；
/// 2. 确认失效后清除本地登录态，并在 UI 线程触发 <see cref="SessionExpired"/>；
/// 3. 提供启动校验和带冷却时间的 <see cref="VerifyAsync"/>，避免反复刷新 token。
/// </summary>
public sealed class AuthSessionService
{
    private const int MaxInspectedBodyLength = 32 * 1024;
    private static readonly TimeSpan VerifyCooldown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SuspectCooldown = TimeSpan.FromSeconds(30);

    public static AuthSessionService Instance { get; } = new();

    private readonly object _gate = new();
    private Task<AuthVerifyResult>? _inflightVerify;
    private string? _lastValidToken;
    private DateTimeOffset _lastValidAt;
    private DateTimeOffset _lastSuspectAt;
    private string? _expiredToken;

    private AuthSessionService()
    {
    }

    /// <summary>登录失效时触发（总在 UI 线程上）。</summary>
    public event EventHandler<AuthSessionExpiredEventArgs>? SessionExpired;

    /// <summary>最近一次确认失效的提示，用于页面显示；重新登录后清空。</summary>
    public string? LastExpiredMessage { get; private set; }

    public void Attach(KugouLiteClient client)
    {
        client.ResponseReceived += OnResponseReceived;
    }

    public void Detach(KugouLiteClient client)
    {
        client.ResponseReceived -= OnResponseReceived;
    }

    /// <summary>登录成功后调用：清除失效标记并记录一次有效校验。</summary>
    public void MarkSessionValid()
    {
        lock (_gate)
        {
            _expiredToken = null;
            _lastValidToken = CurrentToken();
            _lastValidAt = DateTimeOffset.UtcNow;
        }

        LastExpiredMessage = null;
    }

    /// <summary>用户主动退出登录时调用。</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _expiredToken = null;
            _lastValidToken = null;
            _lastValidAt = default;
        }

        LastExpiredMessage = null;
    }

    /// <summary>启动时后台校验一次登录态。</summary>
    public void ValidateOnStartup()
    {
        if (!MusicService.IsLoggedIn)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var locallyExpired = MusicService.Client.GetLoginState().IsExpired;
                await VerifyAsync(force: true, definiteSignal: locallyExpired).ConfigureAwait(false);
            }
            catch
            {
                // 启动校验失败不影响使用。
            }
        });
    }

    /// <summary>
    /// 用刷新 token 接口向服务端确认登录态。并发调用会共享同一次请求；
    /// 非 force 模式下，最近 <see cref="VerifyCooldown"/> 内校验成功过则直接返回 Valid。
    /// </summary>
    public Task<AuthVerifyResult> VerifyAsync(bool force = false, bool definiteSignal = false, CancellationToken cancellationToken = default)
    {
        if (!MusicService.IsLoggedIn)
        {
            return Task.FromResult(AuthVerifyResult.NotLoggedIn);
        }

        lock (_gate)
        {
            var token = CurrentToken();
            if (!force &&
                string.Equals(token, _lastValidToken, StringComparison.Ordinal) &&
                DateTimeOffset.UtcNow - _lastValidAt < VerifyCooldown)
            {
                return Task.FromResult(AuthVerifyResult.Valid);
            }

            if (_inflightVerify is { IsCompleted: false })
            {
                return _inflightVerify.WaitAsync(cancellationToken);
            }

            // 共享的校验任务不绑定某个调用方的取消令牌，调用方各自通过 WaitAsync 取消等待。
            _inflightVerify = Task.Run(() => VerifyCoreAsync(definiteSignal, CancellationToken.None), CancellationToken.None);
            return _inflightVerify.WaitAsync(cancellationToken);
        }
    }

    private async Task<AuthVerifyResult> VerifyCoreAsync(bool definiteSignal, CancellationToken cancellationToken)
    {
        var client = MusicService.Client;
        KugouResponse response;
        try
        {
            response = await client.RefreshTokenAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 网络异常、超时等：无法判断，保留登录态。
            return AuthVerifyResult.Unknown;
        }

        // 校验期间用户可能已经退出或重新登录（客户端被重建），此时结果作废。
        if (!ReferenceEquals(client, MusicService.Client))
        {
            return AuthVerifyResult.Unknown;
        }

        if (IsSuccess(response) && MusicService.IsLoggedIn)
        {
            MusicService.SaveSession();
            lock (_gate)
            {
                _expiredToken = null;
                _lastValidToken = CurrentToken();
                _lastValidAt = DateTimeOffset.UtcNow;
            }

            LastExpiredMessage = null;
            return AuthVerifyResult.Valid;
        }

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized)
        {
            // 5xx / 网关错误等不能说明登录失效。
            return AuthVerifyResult.Unknown;
        }

        var refreshSaysAuthError = ClassifyResponse(response) != AuthSignal.None;
        if (refreshSaysAuthError || definiteSignal)
        {
            MusicService.TryGetResponseError(response, out var errorMessage, out _, out _);
            ExpireSession(string.IsNullOrWhiteSpace(errorMessage) ? "登录已失效" : errorMessage);
            return AuthVerifyResult.Expired;
        }

        return AuthVerifyResult.Unknown;
    }

    private void OnResponseReceived(object? sender, KugouResponseReceivedEventArgs e)
    {
        if (sender is not KugouLiteClient client || !ReferenceEquals(client, MusicService.Client))
        {
            return;
        }

        if (!MusicService.IsLoggedIn || IsLoginRequest(e.Request))
        {
            return;
        }

        var signal = ClassifyResponse(e.Response);
        if (signal == AuthSignal.None)
        {
            return;
        }

        lock (_gate)
        {
            var token = CurrentToken();
            if (string.Equals(token, _expiredToken, StringComparison.Ordinal))
            {
                return;
            }

            // 节流：避免某个接口持续返回错误时反复刷新 token。
            var now = DateTimeOffset.UtcNow;
            if (now - _lastSuspectAt < SuspectCooldown)
            {
                return;
            }

            _lastSuspectAt = now;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await VerifyAsync(force: true, definiteSignal: signal == AuthSignal.Definite).ConfigureAwait(false);
            }
            catch
            {
            }
        });
    }

    private void ExpireSession(string reason)
    {
        string? expiredToken;
        lock (_gate)
        {
            expiredToken = CurrentToken();
            _expiredToken = expiredToken;
            _lastValidToken = null;
            _lastValidAt = default;
        }

        var message = $"酷狗登录已失效（{reason}），请重新登录";
        LastExpiredMessage = message;

        Dispatcher.UIThread.Post(() =>
        {
            // 期间用户已经重新登录（token 变了），不能误清新的登录态。
            if (MusicService.IsLoggedIn && !string.Equals(CurrentToken(), expiredToken, StringComparison.Ordinal))
            {
                return;
            }

            MusicService.ClearSession();
            VipPrivilegeService.Instance.ResetSessionState();
            SessionExpired?.Invoke(this, new AuthSessionExpiredEventArgs(message));
        });
    }

    private enum AuthSignal
    {
        None,
        /// <summary>疑似失效，需要刷新确认。</summary>
        Suspect,
        /// <summary>明确失效（20018 / “登录已过期”等）。</summary>
        Definite
    }

    private static AuthSignal ClassifyResponse(KugouResponse response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return AuthSignal.Suspect;
        }

        var body = response.BodyText;
        if (string.IsNullOrEmpty(body) || body.Length > MaxInspectedBodyLength)
        {
            return AuthSignal.None;
        }

        var trimmed = body.AsSpan().TrimStart();
        if (trimmed.IsEmpty || trimmed[0] != '{')
        {
            return AuthSignal.None;
        }

        if (KugouLiteClient.IsAuthExpiredResponse(response))
        {
            return AuthSignal.Definite;
        }

        using var doc = response.TryParseJson();
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return AuthSignal.None;
        }

        var root = doc.RootElement;
        var errorCode = ReadInt(root, "error_code") ?? ReadInt(root, "errcode") ?? ReadInt(root, "err_code") ?? 0;
        if (errorCode is 20018)
        {
            return AuthSignal.Definite;
        }

        if (errorCode is 20002)
        {
            return AuthSignal.Suspect;
        }

        var message = ReadString(root, "error_msg") ?? ReadString(root, "errmsg") ?? ReadString(root, "msg") ?? ReadString(root, "message") ?? string.Empty;
        if (message.Contains("登录失效", StringComparison.Ordinal) ||
            message.Contains("登录已失效", StringComparison.Ordinal) ||
            message.Contains("重新登录", StringComparison.Ordinal) ||
            message.Contains("token无效", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("token 无效", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("token失效", StringComparison.OrdinalIgnoreCase))
        {
            return AuthSignal.Suspect;
        }

        return AuthSignal.None;
    }

    private static bool IsSuccess(KugouResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        if (MusicService.TryGetResponseError(response, out _, out _, out _))
        {
            return false;
        }

        // login_by_token 成功时 data 中会带 token。
        using var doc = response.TryParseJson();
        return doc is not null &&
            doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object;
    }

    private static bool IsLoginRequest(KugouRequest request)
    {
        var host = request.BaseUri?.Host ?? string.Empty;
        if (host.StartsWith("login.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = request.Path ?? string.Empty;
        return path.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("qrcode", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("send_mobile_code", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("dev_logout", StringComparison.OrdinalIgnoreCase);
    }

    private static string? CurrentToken() => CurrentTokenOf(MusicService.Client);

    private static string? CurrentTokenOf(KugouLiteClient client) => client.CookieStore.Get("token");

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
