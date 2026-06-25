using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace KuGou.Lite;

public sealed partial class KugouLiteClient : IDisposable
{
    private const string TokenIssuedAtKey = "KUGOU_SDK_TOKEN_ISSUED_AT";
    private const string TokenExpiresAtKey = "KUGOU_SDK_TOKEN_EXPIRES_AT";

    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly KugouLiteOptions _options;

    public KugouLiteClient(KugouLiteOptions? options = null, HttpClient? httpClient = null)
    {
        _options = options ?? new KugouLiteOptions();
        _httpClient = httpClient ?? new HttpClient();
        _disposeHttpClient = httpClient is null;
        CookieStore = new KugouCookieStore(_options);
    }

    public KugouCookieStore CookieStore { get; }

    public KugouLoginState GetLoginState(TimeSpan? refreshWindow = null)
    {
        refreshWindow ??= TimeSpan.FromDays(1);
        var token = CookieStore.Get("token");
        var userId = CookieStore.Get("userid");
        var isLoggedIn = !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(userId) && !string.Equals(userId, "0", StringComparison.Ordinal);
        if (!isLoggedIn)
        {
            return new KugouLoginState(false, userId, null, false, false, false, "未登录");
        }

        if (!TryGetTokenExpiresAt(out var expiresAt))
        {
            return new KugouLoginState(true, userId, null, false, true, false, "已登录，但本地没有 token 到期时间；建议调用 RefreshTokenAsync() 校准");
        }

        var now = DateTimeOffset.UtcNow;
        var isExpired = expiresAt <= now;
        var shouldRefresh = expiresAt <= now.Add(refreshWindow.Value);
        var message = isExpired
            ? "token 已过期，需要重新登录或尝试刷新"
            : shouldRefresh
                ? "token 即将过期，建议刷新"
                : "token 有效";

        return new KugouLoginState(true, userId, expiresAt, isExpired, shouldRefresh, true, message);
    }

    public bool TryGetTokenExpiresAt(out DateTimeOffset expiresAt)
    {
        if (TryReadUnixTimeCookie(TokenExpiresAtKey, out expiresAt))
        {
            return true;
        }

        var expires = CookieStore.Get("expires");
        if (!long.TryParse(expires, out var rawExpires) || rawExpires <= 0)
        {
            expiresAt = default;
            return false;
        }

        var issuedAt = TryReadUnixTimeCookie(TokenIssuedAtKey, out var parsedIssuedAt)
            ? parsedIssuedAt
            : DateTimeOffset.UtcNow;
        expiresAt = ResolveTokenExpiresAt(rawExpires, issuedAt);
        return true;
    }

    public static bool IsAuthExpiredResponse(KugouResponse response)
    {
        using var doc = response.TryParseJson();
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (TryReadInt(doc.RootElement, "error_code", out var errorCode) && errorCode == 20018)
        {
            return true;
        }

        var message = ReadString(doc.RootElement, "msg") ?? ReadString(doc.RootElement, "message") ?? ReadString(doc.RootElement, "error") ?? string.Empty;
        return message.Contains("登录已过期", StringComparison.Ordinal) ||
            message.Contains("登录信息已失效", StringComparison.Ordinal) ||
            message.Contains("token", StringComparison.OrdinalIgnoreCase) && message.Contains("过期", StringComparison.Ordinal);
    }

    public async Task<KugouResponse> SendAsync(KugouRequest request, CancellationToken cancellationToken = default)
    {
        var effectiveCookies = CookieStore.Merge(request.Cookies);
        var dfid = effectiveCookies.TryGetValue("dfid", out var cookieDfid) ? cookieDfid : "-";
        var mid = effectiveCookies.TryGetValue("KUGOU_API_MID", out var cookieMid) ? cookieMid : string.Empty;
        var token = effectiveCookies.TryGetValue("token", out var cookieToken) ? cookieToken : string.Empty;
        var userid = effectiveCookies.TryGetValue("userid", out var cookieUserid) ? cookieUserid : "0";
        var clientTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var parameters = BuildParameters(request, dfid, mid, token, userid, clientTime);
        var bodyText = SerializeBody(request.Body);
        


        if (request.EncryptKey)
        {
            var hash = parameters.TryGetValue("hash", out var hashValue) ? KugouCrypto.FormatValueForSignature(hashValue) : string.Empty;
            parameters["key"] = KugouCrypto.SignKey(hash, mid, userid);
        }

        if (!request.NotSignature && !parameters.ContainsKey("signature"))
        {
            parameters["signature"] = request.EncryptType switch
            {
                KugouEncryptType.Web => KugouCrypto.SignatureWebParams(parameters),
                KugouEncryptType.Register => KugouCrypto.SignatureRegisterParams(parameters),
                _ => KugouCrypto.SignatureAndroidParams(parameters, bodyText)
            };
        }

        using var httpRequest = new HttpRequestMessage(request.Method, BuildUri(request.BaseUri ?? _options.DefaultBaseUri, request.Path, parameters));
        var headerClientTime = parameters.TryGetValue("clienttime", out var requestClientTime)
            ? KugouCrypto.FormatValueForQuery(requestClientTime)
            : clientTime.ToString();
        ApplyHeaders(httpRequest, request, dfid, mid, headerClientTime);

        if (request.IncludeCookieHeader)
        {
            httpRequest.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", effectiveCookies
                .Where(item => !IsSdkMetadataCookie(item.Key))
                .Select(item => $"{item.Key}={item.Value}")));
        }

        if (bodyText is not null)
        {
            httpRequest.Content = request.Body is string
                ? new StringContent(bodyText, Encoding.UTF8)
                : new StringContent(bodyText, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var rawBody = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var cookies = ReadSetCookies(response);
        var headers = ReadHeaders(response);
        var result = new KugouResponse(response.StatusCode, rawBody, cookies, headers);

        if (_options.UpdateCookieStoreFromResponses)
        {
            CookieStore.ApplySetCookieHeaders(cookies);
        }

        return result;
    }

    private static Dictionary<string, object?> BuildParameters(
        KugouRequest request,
        string dfid,
        string mid,
        string token,
        string userid,
        long clientTime)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!request.ClearDefaultParams)
        {
            parameters["dfid"] = dfid;
            parameters["mid"] = mid;
            parameters["uuid"] = "-";
            parameters["appid"] = KugouConstants.LiteAppId;
            parameters["clientver"] = KugouConstants.LiteClientVersion;
            parameters["clienttime"] = clientTime;

            if (!string.IsNullOrEmpty(token))
            {
                parameters["token"] = token;
            }

            if (!string.IsNullOrEmpty(userid) && userid != "0")
            {
                parameters["userid"] = userid;
            }
        }

        foreach (var item in request.Params)
        {
            parameters[item.Key] = item.Value;
        }

        return parameters;
    }

    private void ApplyHeaders(HttpRequestMessage httpRequest, KugouRequest request, string dfid, string mid, string clientTime)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["User-Agent"] = _options.UserAgent,
            ["dfid"] = dfid,
            ["clienttime"] = clientTime,
            ["mid"] = mid,
            ["kg-rc"] = "1",
            ["kg-thash"] = "5d816a0",
            ["kg-rec"] = "1",
            ["kg-rf"] = "B9EDA08A64250DEFFBCADDEE00F8F25F"
        };

        foreach (var item in request.Headers)
        {
            headers[item.Key] = item.Value;
        }

        foreach (var item in headers)
        {
            if (item.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            httpRequest.Headers.TryAddWithoutValidation(item.Key, item.Value);
        }
    }

    private static Uri BuildUri(Uri baseUri, string path, IDictionary<string, object?> parameters)
    {
        var uri = path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? new Uri(path)
            : new Uri(baseUri, path);

        if (parameters.Count == 0)
        {
            return uri;
        }

        var query = string.Join("&", parameters.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(KugouCrypto.FormatValueForQuery(item.Value))}"));
        var builder = new UriBuilder(uri);
        if (string.IsNullOrEmpty(builder.Query))
        {
            builder.Query = query;
        }
        else
        {
            builder.Query = builder.Query.TrimStart('?') + "&" + query;
        }

        return builder.Uri;
    }

    private static string? SerializeBody(object? body)
    {
        return body switch
        {
            null => null,
            string s => s,
            _ => KugouCrypto.ToJson(body)
        };
    }

    private static IReadOnlyList<string> ReadSetCookies(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(ParseCookieString).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray()
            : Array.Empty<string>();
    }

    private static IReadOnlyDictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            result[header.Key] = string.Join(",", header.Value);
        }

        foreach (var header in response.Content.Headers)
        {
            result[header.Key] = string.Join(",", header.Value);
        }

        return result;
    }

    private static string ParseCookieString(string cookie)
    {
        var first = cookie.Split(';', 2)[0].Trim();
        return first;
    }

    internal void ApplyLoginCookies(KugouResponse response, string aesKeyForSecuParams)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        using var doc = response.TryParseJson();
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (data.TryGetProperty("secu_params", out var secuParams) && secuParams.ValueKind == JsonValueKind.String)
        {
            var encrypted = secuParams.GetString();
            if (!string.IsNullOrWhiteSpace(encrypted))
            {
                try
                {
                    ApplyJsonCookies(KugouCrypto.AesDecryptHex(encrypted!, aesKeyForSecuParams), issuedAt);
                }
                catch
                {
                    // Keep the original response even if KuGou changes secu_params format.
                }
            }
        }

        SetCookieFromJson(data, "t1");
        SetCookieFromJson(data, "token");
        SetCookieFromJson(data, "userid");
        SetCookieFromJson(data, "vip_type");
        SetCookieFromJson(data, "vip_token");
        ApplyTokenExpirationMetadata(data, issuedAt);
    }

    private void ApplyJsonCookies(string json, DateTimeOffset issuedAt)
    {
        var trimmed = json.Trim();
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
        {
            CookieStore.Set("token", json);
            SetTokenIssuedAt(issuedAt);
            CookieStore.Remove(TokenExpiresAtKey);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                CookieStore.Set("token", json);
                SetTokenIssuedAt(issuedAt);
                CookieStore.Remove(TokenExpiresAtKey);
                return;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                CookieStore.Set(property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText());
            }

            ApplyTokenExpirationMetadata(doc.RootElement, issuedAt);
        }
        catch (JsonException)
        {
            CookieStore.Set("token", json);
            SetTokenIssuedAt(issuedAt);
            CookieStore.Remove(TokenExpiresAtKey);
        }
    }

    private void SetCookieFromJson(JsonElement data, string name)
    {
        if (!data.TryGetProperty(name, out var value))
        {
            return;
        }

        CookieStore.Set(name, value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText());
    }

    private void ApplyTokenExpirationMetadata(JsonElement data, DateTimeOffset issuedAt)
    {
        if (!data.TryGetProperty("token", out _) && string.IsNullOrWhiteSpace(CookieStore.Get("token")))
        {
            return;
        }

        SetTokenIssuedAt(issuedAt);

        if (TryReadLong(data, "expires", out var expires) ||
            TryReadLong(data, "expires_in", out expires) ||
            TryReadLong(data, "expire", out expires) ||
            TryReadLong(data, "expire_time", out expires) ||
            TryReadLong(data, "expireTime", out expires) ||
            TryReadLong(data, "token_expire", out expires) ||
            TryReadLong(data, "tokenExpire", out expires))
        {
            var expiresAt = ResolveTokenExpiresAt(expires, issuedAt);
            CookieStore.Set(TokenExpiresAtKey, expiresAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        CookieStore.Remove(TokenExpiresAtKey);
    }

    private void SetTokenIssuedAt(DateTimeOffset issuedAt)
    {
        CookieStore.Set(TokenIssuedAtKey, issuedAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private bool TryReadUnixTimeCookie(string key, out DateTimeOffset value)
    {
        if (long.TryParse(CookieStore.Get(key), out var raw) && raw > 0)
        {
            value = DateTimeOffset.FromUnixTimeSeconds(raw);
            return true;
        }

        value = default;
        return false;
    }

    private static DateTimeOffset ResolveTokenExpiresAt(long rawExpires, DateTimeOffset issuedAt)
    {
        if (rawExpires > 1_000_000_000_000)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(rawExpires);
        }

        if (rawExpires > 1_000_000_000)
        {
            return DateTimeOffset.FromUnixTimeSeconds(rawExpires);
        }

        return issuedAt.AddSeconds(rawExpires);
    }

    private static bool IsSdkMetadataCookie(string key) =>
        key.StartsWith("KUGOU_SDK_", StringComparison.Ordinal);

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
