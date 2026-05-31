using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KuGou.Lite;

public enum KugouEncryptType
{
    Android,
    Web,
    Register
}

public enum KugouSearchType
{
    Special,
    Lyric,
    Song,
    Album,
    Author,
    Mv
}

public enum KugouLoginQrStatus
{
    Unknown = -1,
    Expired = 0,
    WaitingScan = 1,
    WaitingConfirm = 2,
    Success = 4
}

public sealed record KugouLoginQrSession(
    string Key,
    string Url,
    KugouResponse KeyResponse,
    KugouResponse QrCodeResponse);

public sealed record KugouLoginState(
    bool IsLoggedIn,
    string? UserId,
    DateTimeOffset? TokenExpiresAt,
    bool IsExpired,
    bool ShouldRefresh,
    bool ExpirationKnown,
    string Message);

public sealed class KugouLiteOptions
{
    public string? Guid { get; set; }
    public string? DeviceId { get; set; }
    public string Mac { get; set; } = "02:00:00:00:00:00";
    public string? Dfid { get; set; }
    public string UserAgent { get; set; } = KugouConstants.DefaultUserAgent;
    public Uri DefaultBaseUri { get; set; } = new("https://gateway.kugou.com");
    public bool UpdateCookieStoreFromResponses { get; set; } = true;
}

public sealed class KugouRequest
{
    public HttpMethod Method { get; set; } = HttpMethod.Get;
    public Uri? BaseUri { get; set; }
    public string Path { get; set; } = "/";
    public IDictionary<string, object?> Params { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    public object? Body { get; set; }
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, string> Cookies { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public KugouEncryptType EncryptType { get; set; } = KugouEncryptType.Android;
    public bool EncryptKey { get; set; }
    public bool ClearDefaultParams { get; set; }
    public bool NotSignature { get; set; }
    public bool IncludeCookieHeader { get; set; }
}

public sealed class KugouResponse
{
    internal KugouResponse(HttpStatusCode statusCode, byte[] rawBody, IReadOnlyList<string> cookies, IReadOnlyDictionary<string, string> headers)
    {
        StatusCode = statusCode;
        RawBody = rawBody;
        BodyText = DecodeBody(rawBody);
        Cookies = cookies;
        Headers = headers;
    }

    private KugouResponse(HttpStatusCode statusCode, byte[] rawBody, string bodyText, IReadOnlyList<string> cookies, IReadOnlyDictionary<string, string> headers)
    {
        StatusCode = statusCode;
        RawBody = rawBody;
        BodyText = bodyText;
        Cookies = cookies;
        Headers = headers;
    }

    public HttpStatusCode StatusCode { get; }
    public int StatusCodeNumber => (int)StatusCode;
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;
    public byte[] RawBody { get; }
    public string BodyText { get; }
    public IReadOnlyList<string> Cookies { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }

    public JsonDocument? TryParseJson()
    {
        try
        {
            return JsonDocument.Parse(BodyText);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public KugouResponse WithBodyText(string bodyText)
    {
        return new KugouResponse(StatusCode, System.Text.Encoding.UTF8.GetBytes(bodyText), bodyText, Cookies, Headers);
    }

    public void EnsureSuccessStatusCode()
    {
        if (!IsSuccessStatusCode)
        {
            throw new HttpRequestException($"KuGou request failed with HTTP {(int)StatusCode}: {BodyText}");
        }
    }

    private static string DecodeBody(byte[] rawBody)
    {
        if (rawBody.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return System.Text.Encoding.UTF8.GetString(rawBody);
        }
        catch
        {
            return Convert.ToBase64String(rawBody);
        }
    }
}

public sealed class KugouCookieStore
{
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    public KugouCookieStore(KugouLiteOptions? options = null)
    {
        options ??= new KugouLiteOptions();
        var guid = options.Guid ?? KugouCrypto.Md5Hex(KugouCrypto.NewGuidString());
        var dev = (options.DeviceId ?? KugouCrypto.RandomString(10)).ToUpperInvariant();
        var mid = KugouCrypto.CalculateMid(guid);

        Set("KUGOU_API_PLATFORM", "lite");
        Set("KUGOU_API_GUID", guid);
        Set("KUGOU_API_MID", mid);
        Set("KUGOU_API_DEV", dev);
        Set("KUGOU_API_MAC", options.Mac.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(options.Dfid))
        {
            Set("dfid", options.Dfid!);
        }
    }

    public string? Get(string key) => _cookies.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string? value)
    {
        if (string.IsNullOrEmpty(key) || value is null)
        {
            return;
        }

        _cookies[key] = value;
    }

    public void Remove(string key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _cookies.Remove(key);
        }
    }

    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_cookies, StringComparer.Ordinal);

    public Dictionary<string, string> Merge(IDictionary<string, string>? extra = null)
    {
        var result = new Dictionary<string, string>(_cookies, StringComparer.Ordinal);
        if (extra is not null)
        {
            foreach (var item in extra)
            {
                result[item.Key] = item.Value;
            }
        }

        return result;
    }

    public void ApplySetCookieHeaders(IEnumerable<string> setCookieHeaders)
    {
        foreach (var header in setCookieHeaders)
        {
            var first = header.Split(';', 2)[0];
            var index = first.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            Set(first[..index].Trim(), first[(index + 1)..].Trim());
        }
    }

    public string ToCookieHeader()
    {
        return string.Join("; ", _cookies.Select(item => $"{item.Key}={item.Value}"));
    }
}
