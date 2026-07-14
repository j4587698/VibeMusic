using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KuGou.Lite;

public sealed class KugouRegisterDeviceOptions
{
    public long AvailableRamSize { get; set; } = 4_983_533_568;
    public long AvailableRomSize { get; set; } = 48_114_719;
    public long AvailableSdSize { get; set; } = 48_114_717;
    public string BasebandVersion { get; set; } = string.Empty;
    public int BatteryLevel { get; set; } = 100;
    public int BatteryStatus { get; set; } = 3;
    public string Brand { get; set; } = "Redmi";
    public string BuildSerial { get; set; } = "unknown";
    public string Device { get; set; } = "marble";
    public string Imsi { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = "Xiaomi";
}

public partial class KugouLiteClient
{
    public Task<KugouResponse> RawGatewayAsync(
        string path,
        HttpMethod method,
        IDictionary<string, object?>? parameters = null,
        object? body = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = path,
            Method = method,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        Copy(parameters, request.Params);
        Copy(headers, request.Headers);
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> SendCaptchaAsync(string mobile, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            BaseUri = new Uri("http://login.user.kugou.com"),
            Path = "/v7/send_mobile_code",
            Method = HttpMethod.Post,
            Body = D(("businessid", 5), ("mobile", mobile), ("plat", 3)),
            EncryptType = KugouEncryptType.Android
        };
        return SendAsync(request, cancellationToken);
    }

    public async Task<KugouResponse> LoginByCellphoneAsync(string mobile, string code, string? userId = null, CancellationToken cancellationToken = default)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var encrypt = KugouCrypto.AesEncryptHex(D(("mobile", mobile), ("code", code)));
        var t2 = KugouCrypto.AesEncryptHex(
            $"{CookieStore.Get("KUGOU_API_GUID")}|0f607264fc6318a92b9e13c65db7cd3c|{CookieStore.Get("KUGOU_API_MAC")}|{CookieStore.Get("KUGOU_API_DEV")}|{nowMs}",
            "fd14b35e3f81af3817a20ae7adae7020",
            "17a20ae7adae7020");
        var t1 = KugouCrypto.AesEncryptHex($"|{nowMs}", "5e4ef500e9597fe004bd09a46d8add98", "04bd09a46d8add98");
        var dfid = CookieStore.Get("dfid") ?? KugouCrypto.RandomString(24);

        var body = D(
            ("plat", 1),
            ("support_multi", 1),
            ("t1", t1),
            ("t2", t2),
            ("clienttime_ms", nowMs),
            ("mobile", MaskMobile(mobile)),
            ("key", KugouCrypto.SignParamsKey(nowMs)),
            ("pk", KugouCrypto.RsaRawEncryptHex(D(("clienttime_ms", nowMs), ("key", encrypt.Key))).ToUpperInvariant()),
            ("params", encrypt.CipherHex),
            ("dfid", dfid),
            ("dev", CookieStore.Get("KUGOU_API_DEV")),
            ("gitversion", "5f0b7c4"));

        if (!string.IsNullOrWhiteSpace(userId))
        {
            body["userid"] = userId;
        }

        var request = new KugouRequest
        {
            BaseUri = new Uri("https://loginserviceretry.kugou.com"),
            Path = "/v7/login_by_verifycode",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        request.Headers["support-calm"] = "1";
        request.Headers["User-Agent"] = "Android16-1070-11440-130-0-LOGIN-wifi";

        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        ApplyLoginCookies(response, encrypt.Key);
        return response;
    }

    public async Task<KugouLoginQrSession> CreateLoginQrSessionAsync(string? type = null, CancellationToken cancellationToken = default)
    {
        var keyResponse = await GetLoginQrKeyAsync(type, cancellationToken).ConfigureAwait(false);
        keyResponse.EnsureSuccessStatusCode();

        var key = TryExtractLoginQrKey(keyResponse);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"KuGou QR key response does not contain a qrcode/key field: {keyResponse.BodyText}");
        }

        var qrCodeResponse = await CreateLoginQrCodeAsync(key, cancellationToken).ConfigureAwait(false);
        return new KugouLoginQrSession(key, BuildLoginQrUrl(key), keyResponse, qrCodeResponse);
    }

    public Task<KugouResponse> GetLoginQrKeyAsync(string? type = null, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            BaseUri = new Uri("https://login-user.kugou.com"),
            Path = "/v2/qrcode",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Web
        };
        request.Params["appid"] = string.Equals(type, "web", StringComparison.OrdinalIgnoreCase) ? KugouConstants.QrLoginWebAppId : KugouConstants.QrLoginAppId;
        request.Params["type"] = 1;
        request.Params["plat"] = 4;
        request.Params["qrcode_txt"] = $"https://h5.kugou.com/apps/loginQRCode/html/index.html?appid={KugouConstants.LiteAppId}&";
        request.Params["srcappid"] = KugouConstants.SourceAppId;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> CreateLoginQrCodeAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = BuildLoginQrUrl(key);
        var body = KugouCrypto.ToJson(D(
            ("code", 200),
            ("status", 200),
            ("data", D(
                ("url", url),
                ("base64", string.Empty)))));

        return Task.FromResult(new KugouResponse(
            HttpStatusCode.OK,
            Encoding.UTF8.GetBytes(body),
            Array.Empty<string>(),
            new Dictionary<string, string>()));
    }

    public async Task<KugouResponse> CheckLoginQrAsync(string key, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            BaseUri = new Uri("https://login-user.kugou.com"),
            Path = "/v2/get_userinfo_qrcode",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Web
        };
        request.Params["plat"] = 4;
        request.Params["appid"] = KugouConstants.LiteAppId;
        request.Params["srcappid"] = KugouConstants.SourceAppId;
        request.Params["qrcode"] = key;

        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        ApplyLoginQrCookies(response);
        return response;
    }

    public async Task<KugouResponse> WaitForLoginQrAsync(
        string key,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var actualTimeout = timeout ?? TimeSpan.FromMinutes(2);
        var actualPollInterval = pollInterval ?? TimeSpan.FromSeconds(3);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await CheckLoginQrAsync(key, cancellationToken).ConfigureAwait(false);
            var status = GetLoginQrStatus(response);
            if (status is KugouLoginQrStatus.Success or KugouLoginQrStatus.Expired)
            {
                return response;
            }

            if (DateTimeOffset.UtcNow - startedAt >= actualTimeout)
            {
                throw new TimeoutException($"KuGou QR login timed out after {actualTimeout}.");
            }

            await Task.Delay(actualPollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string BuildLoginQrUrl(string key) =>
        $"https://h5.kugou.com/apps/loginQRCode/html/index.html?qrcode={Uri.EscapeDataString(key)}";

    public static KugouLoginQrStatus GetLoginQrStatus(KugouResponse response)
    {
        var status = TryExtractLoginQrStatusCode(response);
        return status switch
        {
            0 => KugouLoginQrStatus.Expired,
            1 => KugouLoginQrStatus.WaitingScan,
            2 => KugouLoginQrStatus.WaitingConfirm,
            4 => KugouLoginQrStatus.Success,
            _ => KugouLoginQrStatus.Unknown
        };
    }

    public async Task<KugouResponse> RefreshTokenAsync(string? token = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        token ??= CookieStore.Get("token") ?? string.Empty;
        userId ??= CookieStore.Get("userid") ?? "0";

        var p3 = KugouCrypto.AesEncryptHex(
            D(("clienttime", DateTimeOffset.UtcNow.ToUnixTimeSeconds()), ("token", token)),
            "c24f74ca2820225badc01946dba4fdf7",
            "adc01946dba4fdf7");
        var encryptParams = KugouCrypto.AesEncryptHex(D());
        var t2 = KugouCrypto.AesEncryptHex(
            $"{CookieStore.Get("KUGOU_API_GUID")}|0f607264fc6318a92b9e13c65db7cd3c|{CookieStore.Get("KUGOU_API_MAC")}|{CookieStore.Get("KUGOU_API_DEV")}|{nowMs}",
            "fd14b35e3f81af3817a20ae7adae7020",
            "17a20ae7adae7020");
        var t1Prefix = CookieStore.Get("t1") ?? string.Empty;
        var t1 = KugouCrypto.AesEncryptHex($"{t1Prefix}|{nowMs}", "5e4ef500e9597fe004bd09a46d8add98", "04bd09a46d8add98");

        var body = D(
            ("dfid", CookieStore.Get("dfid") ?? "-"),
            ("p3", p3),
            ("plat", 1),
            ("t1", t1),
            ("t2", t2),
            ("t3", "MCwwLDAsMCwwLDAsMCwwLDA="),
            ("pk", KugouCrypto.RsaRawEncryptHex(D(("clienttime_ms", nowMs), ("key", encryptParams.Key)))),
            ("params", encryptParams.CipherHex),
            ("userid", userId),
            ("clienttime_ms", nowMs),
            ("dev", CookieStore.Get("KUGOU_API_DEV")));

        var request = new KugouRequest
        {
            BaseUri = new Uri("http://login.user.kugou.com"),
            Path = "/v5/login_by_token",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };

        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        ApplyLoginCookies(response, encryptParams.Key);
        return response;
    }

    public async Task<KugouResponse> RegisterDeviceAsync(KugouRegisterDeviceOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new KugouRegisterDeviceOptions();
        var userId = CookieStore.Get("userid") ?? "0";
        var token = CookieStore.Get("token") ?? string.Empty;
        var guid = CookieStore.Get("KUGOU_API_GUID") ?? string.Empty;

        var deviceInfo = D(
            ("availableRamSize", options.AvailableRamSize),
            ("availableRomSize", options.AvailableRomSize),
            ("availableSDSize", options.AvailableSdSize),
            ("basebandVer", options.BasebandVersion),
            ("batteryLevel", options.BatteryLevel),
            ("batteryStatus", options.BatteryStatus),
            ("brand", options.Brand),
            ("buildSerial", options.BuildSerial),
            ("device", options.Device),
            ("imei", guid),
            ("imsi", options.Imsi),
            ("manufacturer", options.Manufacturer),
            ("uuid", guid),
            ("accelerometer", false),
            ("accelerometerValue", string.Empty),
            ("gravity", false),
            ("gravityValue", string.Empty),
            ("gyroscope", false),
            ("gyroscopeValue", string.Empty),
            ("light", false),
            ("lightValue", string.Empty),
            ("magnetic", false),
            ("magneticValue", string.Empty),
            ("orientation", false),
            ("orientationValue", string.Empty),
            ("pressure", false),
            ("pressureValue", string.Empty),
            ("step_counter", false),
            ("step_counterValue", string.Empty),
            ("temperature", false),
            ("temperatureValue", string.Empty));

        var aes = KugouCrypto.PlaylistAesEncrypt(deviceInfo);
        var p = KugouCrypto.RsaPkcs1EncryptHex(D(("aes", aes.Key), ("uid", userId), ("token", token)));
        var request = new KugouRequest
        {
            BaseUri = new Uri("https://userservice.kugou.com"),
            Path = "/risk/v2/r_register_dev",
            Method = HttpMethod.Post,
            Body = aes.CipherBase64,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["part"] = 1;
        request.Params["platid"] = 1;
        request.Params["p"] = p;

        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            var decrypted = KugouCrypto.PlaylistAesDecrypt(Convert.ToBase64String(response.RawBody), aes.Key);
            ApplyDfidCookie(decrypted);
            return response.WithBodyText(decrypted);
        }
        catch
        {
            return response;
        }
    }

    public async Task<string?> EnsureDeviceRegisteredAsync(CancellationToken cancellationToken = default)
    {
        var dfid = CookieStore.Get("dfid");
        if (IsValidDfid(dfid))
        {
            return dfid;
        }

        await RegisterDeviceAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        dfid = CookieStore.Get("dfid");
        return IsValidDfid(dfid) ? dfid : null;
    }

    public Task<KugouResponse> GetUserDetailAsync(CancellationToken cancellationToken = default)
    {
        var clientTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var request = new KugouRequest
        {
            Path = "/v3/get_my_info",
            Method = HttpMethod.Post,
            Body = D(
                ("visit_time", clientTime),
                ("usertype", 1),
                ("p", KugouCrypto.RsaRawEncryptHex(D(("token", CookieStore.Get("token") ?? string.Empty), ("clienttime", clientTime))).ToUpperInvariant()),
                ("userid", long.TryParse(CookieStore.Get("userid"), out var userId) ? userId : 0)),
            EncryptType = KugouEncryptType.Android
        };
        request.Params["plat"] = 1;
        request.Headers["x-router"] = "usercenter.kugou.com";
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> SearchAsync(string keywords, KugouSearchType type = KugouSearchType.Song, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var typeName = SearchTypeToWireName(type);
        var version = type == KugouSearchType.Song ? "v3" : "v1";
        var request = new KugouRequest
        {
            Path = $"/{version}/search/{typeName}",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["albumhide"] = 0;
        request.Params["iscorrection"] = 1;
        request.Params["keyword"] = keywords;
        request.Params["nocollect"] = 0;
        request.Params["page"] = page;
        request.Params["pagesize"] = pageSize;
        request.Params["platform"] = "AndroidFilter";
        request.Headers["x-router"] = "complexsearch.kugou.com";
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> SearchPublicSongsAsync(string keywords, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            BaseUri = new Uri("http://mobilecdn.kugou.com"),
            Path = "/api/v3/search/song",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android,
            ClearDefaultParams = true,
            NotSignature = true
        };
        request.Params["format"] = "json";
        request.Params["keyword"] = keywords;
        request.Params["page"] = page;
        request.Params["pagesize"] = pageSize;
        request.Params["showtype"] = 1;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> SearchMixedAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var request = new KugouRequest
        {
            Path = "/v3/search/mixed",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["ab_tag"] = 0;
        request.Params["ability"] = 511;
        request.Params["albumhide"] = 0;
        request.Params["apiver"] = 22;
        request.Params["area_code"] = 1;
        request.Params["clientver"] = 20125;
        request.Params["cursor"] = 0;
        request.Params["is_gpay"] = 0;
        request.Params["iscorrection"] = 1;
        request.Params["keyword"] = keyword;
        request.Params["nocollect"] = 0;
        request.Params["osversion"] = "16.5";
        request.Params["platform"] = "IOSFilter";
        request.Params["recver"] = 2;
        request.Params["req_ai"] = 1;
        request.Params["requestid"] = $"{KugouCrypto.Md5Hex($"bdaa53d04e7475feb9024164a47032f9{time}")}_0";
        request.Params["search_ability"] = 3;
        request.Params["sec_aggre"] = 1;
        request.Params["sec_aggre_bitmap"] = 0;
        request.Params["style_type"] = 3;
        request.Params["tag"] = "em";
        request.Headers["x-router"] = "complexsearch.kugou.com";
        request.Headers["kg-clienttimems"] = time.ToString();
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> SearchLyricAsync(string? keywords = null, string? hash = null, long albumAudioId = 0, string man = "no", long duration = 0, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            BaseUri = new Uri("https://lyrics.kugou.com"),
            Path = "/v1/search",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android,
            ClearDefaultParams = true
        };
        request.Params["album_audio_id"] = albumAudioId;
        request.Params["appid"] = KugouConstants.LiteAppId;
        request.Params["clientver"] = KugouConstants.LiteClientVersion;
        request.Params["duration"] = duration;
        request.Params["hash"] = hash ?? string.Empty;
        request.Params["keyword"] = keywords ?? string.Empty;
        request.Params["lrctxt"] = 1;
        request.Params["man"] = man;
        return SendAsync(request, cancellationToken);
    }

    public async Task<KugouResponse> GetLyricAsync(string id, string accessKey, string fmt = "krc", bool decode = false, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            BaseUri = new Uri("https://lyrics.kugou.com"),
            Path = "/download",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["ver"] = 1;
        request.Params["client"] = "android";
        request.Params["id"] = id;
        request.Params["accesskey"] = accessKey;
        request.Params["fmt"] = fmt;
        request.Params["charset"] = "utf8";

        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return decode ? TryAppendDecodedLyric(response, fmt) : response;
    }

    public async Task<KugouResponse> GetSongUrlAsync(string hash, long albumId = 0, long albumAudioId = 0, string quality = "128", bool freePart = false, string? ppageId = null, CancellationToken cancellationToken = default)
    {
        var dfid = await EnsureDeviceRegisteredAsync(cancellationToken).ConfigureAwait(false) ?? KugouCrypto.RandomString(24);
        var normalizedQuality = string.IsNullOrWhiteSpace(quality)
            ? "128"
            : quality is "piano" or "acappella" or "subwoofer" or "ancient" or "dj" or "surnay"
                ? $"magic_{quality}"
                : quality;
        var request = new KugouRequest
        {
            Path = "/v5/url",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android,
            EncryptKey = true
        };
        request.Params["album_id"] = albumId;
        request.Params["area_code"] = 1;
        request.Params["hash"] = hash.ToLowerInvariant();
        request.Params["ssa_flag"] = "is_fromtrack";
        request.Params["version"] = 11430;
        request.Params["page_id"] = 967177915;
        request.Params["quality"] = normalizedQuality;
        request.Params["album_audio_id"] = albumAudioId;
        request.Params["behavior"] = "play";
        request.Params["pid"] = 411;
        request.Params["cmd"] = 26;
        request.Params["pidversion"] = 3001;
        request.Params["IsFreePart"] = freePart ? 1 : 0;
        request.Params["ppage_id"] = ppageId ?? "356753938,823673182,967485191";
        request.Params["cdnBackup"] = 1;
        request.Params["module"] = string.Empty;
        request.Params["clientver"] = 11430;
        request.Cookies["dfid"] = dfid;
        request.Headers["x-router"] = "trackercdn.kugou.com";
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<KugouResponse> GetSongUrlNewAsync(string hash, long albumAudioId, string[] qualities, bool freePart = false, CancellationToken cancellationToken = default)
    {
        var userId = CookieStore.Get("userid") ?? "0";
        var mid = CookieStore.Get("KUGOU_API_MID") ?? string.Empty;
        var clientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dfid = await EnsureDeviceRegisteredAsync(cancellationToken).ConfigureAwait(false) ?? KugouCrypto.RandomString(24);

        var body = D(
            ("area_code", "1"),
            ("behavior", "play"),
            ("qualities", qualities),
            ("resource", D(
                ("album_audio_id", albumAudioId.ToString()),
                ("collect_list_id", "3"),
                ("collect_time", clientTimeMs),
                ("hash", hash),
                ("id", 0),
                ("page_id", 1),
                ("type", "audio"))),
            ("token", CookieStore.Get("token") ?? string.Empty),
            ("tracker_param", D(
                ("all_m", 1),
                ("auth", string.Empty),
                ("is_free_part", freePart ? 1 : 0),
                ("key", KugouCrypto.Md5Hex($"{hash}{KugouConstants.LiteSignKeySalt}{KugouConstants.LiteAppId}{mid}{userId}")),
                ("module_id", 0),
                ("need_climax", 1),
                ("need_xcdn", 1),
                ("open_time", string.Empty),
                ("pid", "411"),
                ("pidversion", "3001"),
                ("priv_vip_type", "6"),
                ("viptoken", CookieStore.Get("vip_token") ?? string.Empty))),
            ("userid", userId),
            ("vip", int.TryParse(CookieStore.Get("vip_type"), out var vipType) ? vipType : 0));

        var request = new KugouRequest
        {
            BaseUri = new Uri("http://tracker.kugou.com"),
            Path = "/v6/priv_url",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        request.Cookies["dfid"] = dfid;
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<KugouResponse> GetPublicSongInfoAsync(string hash, long albumId = 0, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            BaseUri = new Uri("http://m.kugou.com"),
            Path = "/app/i/getSongInfo.php",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android,
            ClearDefaultParams = true,
            NotSignature = true
        };
        request.Params["cmd"] = "playInfo";
        request.Params["hash"] = hash;
        if (albumId > 0)
        {
            request.Params["album_id"] = albumId;
        }

        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> GetAlbumDetailAsync(long albumId, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/kmr/v2/albums",
            Method = HttpMethod.Post,
            Body = D(
                ("data", new[] { D(("album_id", albumId)) }),
                ("is_buy", 0),
                ("fields", "album_id,album_name,publish_date,sizable_cover,intro,language,is_publish,heat,type,quality,authors,exclusive,author_name,trans_param")),
            EncryptType = KugouEncryptType.Android
        };
        request.Headers["x-router"] = "openapi.kugou.com";
        request.Headers["kg-tid"] = "255";
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> GetAlbumSongsAsync(long albumId, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/v1/album_audio/lite",
            Method = HttpMethod.Post,
            Body = D(("album_id", albumId), ("is_buy", string.Empty), ("page", page), ("pagesize", pageSize)),
            EncryptType = KugouEncryptType.Android
        };
        request.Headers["x-router"] = "openapi.kugou.com";
        request.Headers["kg-tid"] = "255";
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> GetPlaylistTracksAsync(string globalCollectionId, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/pubsongs/v2/get_other_list_file_nofilt",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["area_code"] = 1;
        request.Params["begin_idx"] = (page - 1) * pageSize;
        request.Params["plat"] = 1;
        request.Params["type"] = 1;
        request.Params["mode"] = 1;
        request.Params["personal_switch"] = 1;
        request.Params["extend_fields"] = "abtags,hot_cmt,popularization";
        request.Params["pagesize"] = pageSize;
        request.Params["global_collection_id"] = globalCollectionId;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> GetRankAudioAsync(long rankId, long rankCid = 0, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/openapi/kmr/v2/rank/audio",
            Method = HttpMethod.Post,
            Body = D(
                ("show_portrait_mv", 1),
                ("show_type_total", 1),
                ("filter_original_remarks", 1),
                ("area_code", 1),
                ("pagesize", pageSize),
                ("rank_cid", rankCid),
                ("type", 1),
                ("page", page),
                ("rank_id", rankId)),
            EncryptType = KugouEncryptType.Android
        };
        request.Headers["kg-tid"] = "369";
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> GetServerNowAsync(CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/v1/server_now",
            Method = HttpMethod.Post,
            Body = D(),
            EncryptType = KugouEncryptType.Android
        };
        request.Headers["x-router"] = "usercenter.kugou.com";
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> GetConceptVipDetailAsync(CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            BaseUri = new Uri("https://kugouvip.kugou.com"),
            Path = "/v1/get_union_vip",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["busi_type"] = "concept";
        request.Params["opt_product_types"] = "dvip,qvip";
        request.Params["product_type"] = "svip";
        return SendAsync(request, cancellationToken);
    }

    public async Task<KugouResponse> ClaimDayVipAsync(string? receiveDay = null, CancellationToken cancellationToken = default)
    {
        receiveDay ??= await GetServerTodayAsync(cancellationToken).ConfigureAwait(false);
        var request = new KugouRequest
        {
            Path = "/youth/v1/recharge/receive_vip_listen_song",
            Method = HttpMethod.Post,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["source_id"] = 90139;
        request.Params["receive_day"] = receiveDay;
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<KugouResponse> UpgradeDayVipAsync(CancellationToken cancellationToken = default)
    {
        var userId = long.TryParse(CookieStore.Get("userid"), out var parsedUserId) ? parsedUserId : 0;
        var request = new KugouRequest
        {
            Path = "/youth/v1/listen_song/upgrade_vip_reward",
            Method = HttpMethod.Post,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["kugouid"] = userId;
        request.Params["ad_type"] = 1;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> GetVipMonthRecordAsync(CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v1/activity/get_month_vip_record",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        return SendAsync(request, cancellationToken);
    }

    public async Task<KugouConceptVipEnsureResult> EnsureConceptVipAsync(CancellationToken cancellationToken = default)
    {
        var receiveDay = await GetServerTodayAsync(cancellationToken).ConfigureAwait(false);
        if (!HasLoginState())
        {
            return new KugouConceptVipEnsureResult(false, false, false, false, false, false, false, receiveDay, null, null, null, null);
        }

        var before = await GetConceptVipDetailAsync(cancellationToken).ConfigureAwait(false);
        var isVipBefore = HasActiveConceptVip(before);
        if (isVipBefore)
        {
            return new KugouConceptVipEnsureResult(true, true, false, false, false, false, true, receiveDay, before, null, null, before);
        }

        var claim = await ClaimDayVipAsync(receiveDay, cancellationToken).ConfigureAwait(false);
        var claimSucceeded = IsKugouActionSuccess(claim);

        var upgrade = await UpgradeDayVipAsync(cancellationToken).ConfigureAwait(false);
        var upgradeSucceeded = IsKugouActionSuccess(upgrade, 297002);

        var after = await GetConceptVipDetailAsync(cancellationToken).ConfigureAwait(false);
        var isVipAfter = HasActiveConceptVip(after);

        return new KugouConceptVipEnsureResult(
            true,
            false,
            true,
            claimSucceeded,
            true,
            upgradeSucceeded,
            isVipAfter,
            receiveDay,
            before,
            claim,
            upgrade,
            after);
    }

    public async Task<string> GetServerTodayAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await GetServerNowAsync(cancellationToken).ConfigureAwait(false);
            using var doc = response.TryParseJson();
            if (doc is not null)
            {
                var root = doc.RootElement;
                var source = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                    ? data
                    : root;

                if (TryGetTimestamp(source, out var timestamp))
                {
                    var ms = timestamp > 1_000_000_000_000 ? timestamp : timestamp * 1000;
                    return FormatBeijingDate(ms);
                }
            }
        }
        catch
        {
            // Fall back to local clock below.
        }

        return FormatBeijingDate(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private static Dictionary<string, object?> D(params (string Key, object? Value)[] items)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            result[item.Key] = item.Value;
        }

        return result;
    }

    private static void Copy<TValue>(IDictionary<string, TValue>? from, IDictionary<string, TValue> to)
    {
        if (from is null)
        {
            return;
        }

        foreach (var item in from)
        {
            to[item.Key] = item.Value;
        }
    }

    private static string MaskMobile(string mobile)
    {
        return mobile.Length >= 11 ? $"{mobile[..2]}*****{mobile.Substring(10, 1)}" : mobile;
    }

    private static string SearchTypeToWireName(KugouSearchType type)
    {
        return type switch
        {
            KugouSearchType.Special => "special",
            KugouSearchType.Lyric => "lyric",
            KugouSearchType.Album => "album",
            KugouSearchType.Author => "author",
            KugouSearchType.Mv => "mv",
            _ => "song"
        };
    }

    private void ApplyDfidCookie(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("status", out var status) && status.GetInt32() == 1 &&
            doc.RootElement.TryGetProperty("data", out var data) &&
            data.TryGetProperty("dfid", out var dfid))
        {
            CookieStore.Set("dfid", dfid.GetString());
        }
    }

    private static bool IsValidDfid(string? dfid) =>
        !string.IsNullOrWhiteSpace(dfid) && !string.Equals(dfid, "-", StringComparison.Ordinal);

    private bool HasLoginState() =>
        !string.IsNullOrWhiteSpace(CookieStore.Get("token")) &&
        !string.IsNullOrWhiteSpace(CookieStore.Get("userid")) &&
        !string.Equals(CookieStore.Get("userid"), "0", StringComparison.Ordinal);

    private static bool HasActiveConceptVip(KugouResponse response)
    {
        using var doc = response.TryParseJson();
        return doc is not null && HasActiveConceptVip(doc.RootElement);
    }

    private static bool HasActiveConceptVip(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsActiveConceptVipObject(element))
                {
                    return true;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (HasActiveConceptVip(property.Value))
                    {
                        return true;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (HasActiveConceptVip(item))
                    {
                        return true;
                    }
                }

                break;
        }

        return false;
    }

    private static bool IsActiveConceptVipObject(JsonElement element)
    {
        var productType = ReadString(element, "product_type") ?? ReadString(element, "productType") ?? string.Empty;
        if (!productType.Equals("svip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryReadInt(element, "is_vip", out var isVip) && !TryReadInt(element, "isVip", out isVip))
        {
            return false;
        }

        if (isVip != 1)
        {
            return false;
        }

        if (TryReadVipEndTime(element, out var endTime) && endTime <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        return true;
    }

    private static bool IsKugouActionSuccess(KugouResponse response, params int[] acceptedErrorCodes)
    {
        using var doc = response.TryParseJson();
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (TryReadInt(doc.RootElement, "status", out var status) && status == 1)
        {
            return true;
        }

        return acceptedErrorCodes.Length > 0 &&
            TryReadInt(doc.RootElement, "error_code", out var errorCode) &&
            acceptedErrorCodes.Contains(errorCode);
    }

    private static bool TryGetTimestamp(JsonElement source, out long timestamp)
    {
        foreach (var name in new[] { "now", "time", "timestamp", "server_time", "serverTime" })
        {
            if (TryReadLong(source, name, out timestamp) && timestamp > 0)
            {
                return true;
            }
        }

        timestamp = 0;
        return false;
    }

    private static bool TryReadVipEndTime(JsonElement source, out DateTimeOffset endTime)
    {
        if (source.TryGetProperty("vip_end_time", out var value) || source.TryGetProperty("vipEndTime", out value))
        {
            if (TryReadLong(value, out var numeric) && numeric > 0)
            {
                var ms = numeric > 1_000_000_000_000 ? numeric : numeric * 1000;
                endTime = DateTimeOffset.FromUnixTimeMilliseconds(ms);
                return true;
            }

            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out endTime))
            {
                return true;
            }
        }

        endTime = default;
        return false;
    }

    private static string FormatBeijingDate(long unixTimeMilliseconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds)
            .ToOffset(TimeSpan.FromHours(8))
            .ToString("yyyy-MM-dd");

    private static string? ReadString(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static bool TryReadInt(JsonElement source, string name, out int value)
    {
        if (source.TryGetProperty(name, out var element) && TryReadLong(element, out var parsed))
        {
            value = (int)parsed;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadLong(JsonElement source, string name, out long value)
    {
        if (source.TryGetProperty(name, out var element))
        {
            return TryReadLong(element, out value);
        }

        value = 0;
        return false;
    }

    private static bool TryReadLong(JsonElement element, out long value)
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

    private void ApplyLoginQrCookies(KugouResponse response)
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

        if (TryReadInt32(data, "status") != 4)
        {
            return;
        }

        SetCookieFromJson(data, "token");
        SetCookieFromJson(data, "userid");
        SetCookieFromJson(data, "t1");
        SetCookieFromJson(data, "vip_type");
        SetCookieFromJson(data, "vip_token");
        ApplyTokenExpirationMetadata(data, issuedAt);
    }

    private static string? TryExtractLoginQrKey(KugouResponse response)
    {
        using var doc = response.TryParseJson();
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            return TryReadString(data, "qrcode") ??
                   TryReadString(data, "key") ??
                   TryReadString(data, "qrkey") ??
                   TryReadString(data, "qrcode_key");
        }

        return TryReadString(doc.RootElement, "qrcode") ??
               TryReadString(doc.RootElement, "key") ??
               TryReadString(doc.RootElement, "qrkey") ??
               TryReadString(doc.RootElement, "qrcode_key");
    }

    private static int? TryExtractLoginQrStatusCode(KugouResponse response)
    {
        using var doc = response.TryParseJson();
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            return TryReadInt32(data, "status");
        }

        return TryReadInt32(doc.RootElement, "status");
    }

    private static string? TryReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static int? TryReadInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static KugouResponse TryAppendDecodedLyric(KugouResponse response, string fmt)
    {
        try
        {
            var node = JsonNode.Parse(response.BodyText)?.AsObject();
            var content = node?["content"]?.GetValue<string>();
            if (string.IsNullOrEmpty(content))
            {
                return response;
            }

            var contentType = node?["contenttype"]?.GetValue<int>() ?? 0;
            node!["decodeContent"] = KugouLyricDecoder.Decode(content!, fmt.Equals("lrc", StringComparison.OrdinalIgnoreCase) || contentType != 0);
            return response.WithBodyText(node.ToJsonString());
        }
        catch
        {
            return response;
        }
    }

    public Task<KugouResponse> LoginByPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var dateNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var encrypt = KugouCrypto.AesEncryptHex(D(("pwd", password), ("code", string.Empty), ("clienttime_ms", dateNow)));
        var body = D(
            ("plat", 1),
            ("support_multi", 1),
            ("clienttime_ms", dateNow),
            ("t1", "562a6f12a6e803453647d16a08f5f0c2ff7eee692cba2ab74cc4c8ab47fc467561a7c6b586ce7dc46a63613b246737c03a1dc8f8d162d8ce1d2c71893d19f1d4b797685a4c6d3d81341cbde65e488c4829a9b4d42ef2df470eb102979fa5adcdd9b4eecfea8b909ff7599abeb49867640f10c3c70fc444effca9d15db44a9a6c907731e2bb0f22cd9b3536380169995693e5f0e2424e3378097d3813186e3fe96bbe7023808a0981b4e2b6135a76faac"),
            ("t2", "31c4daf4cf480169ccea1cb7d4a209295865a9d2b788510301694db229b87807469ea0d41b4d4b9173c2151da7294aeebfc9738df154bbdf11a4e117bb5dff6a3af8ce5ce333e681c1f29a44038f27567d58992eb81283e080778ac77db1400fdf49b7cf7e26be2e5af4da7830cc3be4"),
            ("t3", "MCwwLDAsMCwwLDAsMCwwLDA="),
            ("username", username),
            ("params", encrypt.CipherHex),
            ("pk", KugouCrypto.RsaRawEncryptHex(D(("clienttime_ms", dateNow), ("key", encrypt.Key))).ToUpperInvariant()));
        var request = new KugouRequest
        {
            Path = "/v9/login_by_pwd",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        request.Headers["x-router"] = "login.user.kugou.com";
        return SendAndApplyLoginCookiesAsync(request, encrypt.Key, cancellationToken);
    }

    public async Task<KugouResponse> GetVerifyInfoAsync(string eventId, int platId = 2, CancellationToken cancellationToken = default)
    {
        var userid = CookieStore.Get("userid") ?? "0";
        var body = D(
            ("eventid", eventId),
            ("userid", userid),
            ("platid", platId),
            ("rtype", 1),
            ("wasm", 1),
            ("i", string.Empty),
            ("sid", string.Empty),
            ("edt", string.Empty));
        var request = new KugouRequest
        {
            Path = "/verifyservice/v3/get_verify_info",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<KugouResponse> SubmitVerifyCodeAsync(string eventId, string verifyCode, int platId = 2, int vType = 23, CancellationToken cancellationToken = default)
    {
        var (sid, edt) = KugouSimulateGenerator.Generate(
            CookieStore.Get("KUGOU_API_MID") ?? "0",
            CookieStore.Get("userid") ?? "0",
            CookieStore.Get("dfid") ?? "0",
            CookieStore.Get("KUGOU_API_WEBGL"));

        var userid = CookieStore.Get("userid") ?? "0";
        var dataMap = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["eventid"] = eventId,
            ["userid"] = userid,
            ["platid"] = platId,
            ["v_type"] = vType,
            ["wasm"] = 1,
            ["i"] = string.Empty,
            ["sid"] = sid,
            ["edt"] = edt,
        };

        if (vType == 23)
        {
            var encrypt = KugouCrypto.AesEncryptHex(D());
            dataMap["verifycode"] = verifyCode;
            dataMap["pk"] = KugouCrypto.RsaRawEncryptHex(D(("key", encrypt.Key)));
            dataMap["params"] = encrypt.CipherHex;
        }
        else if (vType == 32)
        {
            var encrypt = KugouCrypto.AesEncryptHex(D(("code", verifyCode)));
            dataMap["code"] = verifyCode;
            dataMap["pk"] = KugouCrypto.RsaRawEncryptHex(D(("key", encrypt.Key)));
            dataMap["params"] = encrypt.CipherHex;
        }

        var request = new KugouRequest
        {
            BaseUri = new Uri("https://verifyservice.kugou.com"),
            Path = "/v4/verify_user_info",
            Method = HttpMethod.Post,
            Body = dataMap,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["clientver"] = 11510;
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<KugouResponse> AudioMatchAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var request = new KugouRequest
        {
            Path = "/fingerprint.service/v1/music_trackid_mulit",
            Method = HttpMethod.Post,
            Body = audioData,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["fpid"] = now;
        request.Params["area_code"] = 1;
        request.Params["include_unpublish"] = 1;
        request.Params["useid"] = CookieStore.Get("userid") ?? "0";
        request.Params["multi_result"] = 1;
        request.Headers["content-type"] = "application/octet-stream";
        request.Headers["user-agent"] = "KuGou/11490 (Android)";
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> LoginDeviceListAsync(CancellationToken cancellationToken = default)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var encrypt = KugouCrypto.AesEncryptHex(D(("token", CookieStore.Get("token") ?? string.Empty)));
        var body = D(
            ("plat", 1),
            ("userid", CookieStore.Get("userid") ?? "0"),
            ("clienttime_ms", nowMs),
            ("pk", KugouCrypto.RsaRawEncryptHex(D(("clienttime_ms", nowMs), ("key", encrypt.Key))).ToUpperInvariant()),
            ("params", encrypt.CipherHex));
        var request = new KugouRequest
        {
            BaseUri = new Uri("https://userinfoservice.kugou.com"),
            Path = "/v2/get_dev",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        return SendAsync(request, cancellationToken);
    }

    public async Task<KugouResponse> LoginDeviceKickAsync(string uuid, string? mid = null, string? dfid = null, CancellationToken cancellationToken = default)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var encrypt = KugouCrypto.AesEncryptHex(D(("token", CookieStore.Get("token") ?? string.Empty)));
        var guid = CookieStore.Get("KUGOU_API_GUID") ?? string.Empty;
        var resolvedMid = mid ?? CookieStore.Get("KUGOU_API_MID") ?? KugouCrypto.CalculateMid(guid);
        var resolvedDfid = dfid ?? CookieStore.Get("dfid") ?? "-";
        var userid = CookieStore.Get("userid") ?? "0";

        var body = D(
            ("appid", KugouConstants.LiteAppId),
            ("clientver", KugouConstants.LiteClientVersion),
            ("clienttime", nowMs),
            ("mid", resolvedMid),
            ("uuid", uuid),
            ("dfid", resolvedDfid),
            ("plat", 1),
            ("userid", userid),
            ("token", encrypt.CipherHex),
            ("t_mid", guid),
            ("t", nowMs),
            ("t_appid", 3116),
            ("t_clientver", 10597),
            ("srcappid", KugouConstants.SourceAppId),
            ("signature", KugouCrypto.SignParamsKey(nowMs)));
        var request = new KugouRequest
        {
            Path = "/loginservice/v1/dev_logout",
            Method = HttpMethod.Get,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        request.Headers["Host"] = "gateway.kugou.com";
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<KugouResponse> TopCardYouthTagAsync(CancellationToken cancellationToken = default)
    {
        var body = D(("tagid", string.Empty), ("u_info", string.Empty), ("source_mixsong", string.Empty));
        var request = new KugouRequest
        {
            Path = "/youth/v1/song/tag_card_recommend",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["ver"] = "v2";
        request.Params["area_code"] = 1;
        request.Params["platform"] = "ios";
        request.Params["module_id"] = 1;
        request.Params["clientver"] = 11490;
        return SendAsync(request, cancellationToken);
    }

    private async Task<KugouResponse> SendAndApplyLoginCookiesAsync(KugouRequest request, string encryptKey, CancellationToken cancellationToken)
    {
        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        ApplyLoginCookies(response, encryptKey);
        return response;
    }

    public async Task<KugouResponse> LoginByOpenPlatAsync(string code, CancellationToken cancellationToken = default)
    {
        using var wxHttp = new HttpClient();
        var wxResponse = await wxHttp.PostAsync(
            $"https://api.weixin.qq.com/sns/oauth2/access_token?appid={KugouConstants.WxLiteAppId}&secret={KugouConstants.WxLiteSecret}&code={Uri.EscapeDataString(code)}&grant_type=authorization_code",
            null, cancellationToken).ConfigureAwait(false);
        wxResponse.EnsureSuccessStatusCode();
        var wxBody = await wxResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var wxDoc = JsonDocument.Parse(wxBody);
        var wxRoot = wxDoc.RootElement;

        if (!wxRoot.TryGetProperty("access_token", out var accessTokenProp) ||
            !wxRoot.TryGetProperty("openid", out var openIdProp))
        {
            throw new InvalidOperationException($"WeChat access_token response is missing required fields: {wxBody}");
        }

        var accessToken = accessTokenProp.GetString()!;
        var openId = openIdProp.GetString()!;
        var dateNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var encrypt = KugouCrypto.AesEncryptHex(D(("access_token", accessToken)));
        var pk = KugouCrypto.RsaRawEncryptHex(D(("clienttime_ms", dateNow), ("key", encrypt.Key))).ToUpperInvariant();
        var guid = CookieStore.Get("KUGOU_API_GUID") ?? string.Empty;
        var dev = CookieStore.Get("KUGOU_API_DEV") ?? string.Empty;
        var mac = CookieStore.Get("KUGOU_API_MAC") ?? "02:00:00:00:00:00";
        var t2 = KugouCrypto.AesEncryptHex(
            $"{guid}|0f607264fc6318a92b9e13c65db7cd3c|{mac}|{dev}|{dateNow}",
            "fd14b35e3f81af3817a20ae7adae7020",
            "17a20ae7adae7020");
        var t1 = KugouCrypto.AesEncryptHex($"|{dateNow}", "5e4ef500e9597fe004bd09a46d8add98", "04bd09a46d8add98");

        var body = D(
            ("dev", dev),
            ("force_login", 1),
            ("partnerid", 36),
            ("clienttime_ms", dateNow),
            ("t1", t1),
            ("t2", t2),
            ("t3", "MCwwLDAsMCwwLDAsMCwwLDA="),
            ("openid", openId),
            ("params", encrypt.CipherHex),
            ("pk", pk));

        var request = new KugouRequest
        {
            Path = "/v6/login_by_openplat",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        request.Headers["x-router"] = "login.user.kugou.com";

        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        ApplyLoginOpenPlatCookies(response, encrypt.Key);
        return response;
    }

    public async Task<KugouResponse> CreateWeChatLoginQrAsync(CancellationToken cancellationToken = default)
    {
        using var wxHttp = new HttpClient();
        var tokenResponse = await wxHttp.GetAsync(
            $"https://api.weixin.qq.com/cgi-bin/token?appid={KugouConstants.WxLiteAppId}&secret={KugouConstants.WxLiteSecret}&grant_type=client_credential",
            cancellationToken).ConfigureAwait(false);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var tokenDoc = JsonDocument.Parse(tokenBody);
        if (!tokenDoc.RootElement.TryGetProperty("access_token", out var accessTokenProp))
        {
            throw new InvalidOperationException($"WeChat token response missing access_token: {tokenBody}");
        }

        var accessToken = accessTokenProp.GetString()!;
        var ticketResponse = await wxHttp.GetAsync(
            $"https://api.weixin.qq.com/cgi-bin/ticket/getticket?access_token={Uri.EscapeDataString(accessToken)}&type=2",
            cancellationToken).ConfigureAwait(false);
        ticketResponse.EnsureSuccessStatusCode();
        var ticketBody = await ticketResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var ticketDoc = JsonDocument.Parse(ticketBody);
        if (!ticketDoc.RootElement.TryGetProperty("ticket", out var ticketProp) ||
            ticketDoc.RootElement.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() != 0)
        {
            throw new InvalidOperationException($"WeChat ticket response error: {ticketBody}");
        }

        var ticket = ticketProp.GetString()!;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nonceStr = KugouCrypto.Md5Hex(KugouCrypto.RandomString());
        var signatureParams = $"appid={KugouConstants.WxLiteAppId}&noncestr={nonceStr}&sdk_ticket={ticket}&timestamp={timestamp}";
        var signature = KugouCrypto.Sha1Hex(signatureParams);
        var qrResponse = await wxHttp.GetAsync(
            $"https://open.weixin.qq.com/connect/sdk/qrconnect?appid={KugouConstants.WxLiteAppId}&noncestr={nonceStr}&timestamp={timestamp}&scope=snsapi_userinfo&signature={signature}",
            cancellationToken).ConfigureAwait(false);
        qrResponse.EnsureSuccessStatusCode();
        var qrBody = await qrResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var qrDoc = JsonDocument.Parse(qrBody);
        var cloned = JsonNode.Parse(qrBody)!.AsObject();
        if (cloned.TryGetPropertyValue("uuid", out var uuid) && uuid is not null)
        {
            if (cloned["qrcode"] is not JsonObject qrcode)
            {
                qrcode = new JsonObject();
                cloned["qrcode"] = qrcode;
            }

            qrcode["qrcodeurl"] = $"https://open.weixin.qq.com/connect/confirm?uuid={uuid}";
        }

        return new KugouResponse(
            System.Net.HttpStatusCode.OK,
            Encoding.UTF8.GetBytes(cloned.ToJsonString()),
            Array.Empty<string>(),
            new Dictionary<string, string>());
    }

    public async Task<KugouResponse> CheckWeChatLoginQrAsync(string uuid, CancellationToken cancellationToken = default)
    {
        using var wxHttp = new HttpClient();
        var response = await wxHttp.GetAsync(
            $"https://long.open.weixin.qq.com/connect/l/qrconnect?f=json&uuid={Uri.EscapeDataString(uuid)}",
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new KugouResponse(
            System.Net.HttpStatusCode.OK,
            Encoding.UTF8.GetBytes(body),
            Array.Empty<string>(),
            new Dictionary<string, string>());
    }

    public Task<KugouResponse> TopCardYouthAsync(int cardId = 3005, int pageSize = 30, string? tagId = null, string? sourceMixSong = null, CancellationToken cancellationToken = default)
    {
        var body = D(
            ("tagid", tagId ?? string.Empty),
            ("u_info", string.Empty),
            ("source_mixsong", sourceMixSong ?? string.Empty));
        var request = new KugouRequest
        {
            Path = "youth/v1/song/single_card_recommend",
            Method = HttpMethod.Post,
            Body = body,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["card_id"] = cardId;
        request.Params["area_code"] = 1;
        request.Params["platform"] = "ios";
        request.Params["module_id"] = 1;
        request.Params["ver"] = "v2";
        request.Params["pagesize"] = pageSize;
        request.Params["clientver"] = 11490;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> ReportListenSongAsync(long mixSongId = 666075191, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v2/report/listen_song",
            Method = HttpMethod.Post,
            Body = D(("mixsongid", mixSongId)),
            EncryptType = KugouEncryptType.Android
        };
        request.Params["clientver"] = 10566;
        request.Headers["user-agent"] = "Android13-1070-10566-201-0-ReportPlaySongToServerProtocol-wifi";
        request.Headers["content-type"] = "application/json; charset=utf-8";
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthChannelAllAsync(int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v2/channel/channel_all_list",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["page"] = page;
        request.Params["pagesize"] = pageSize;
        request.Params["type"] = 1;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthChannelDetailAsync(string globalCollectionId, CancellationToken cancellationToken = default)
    {
        var ids = globalCollectionId
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => (object)D(("global_collection_id", s)))
            .ToArray();
        var request = new KugouRequest
        {
            Path = "/youth/api/channel/v1/channel_list_by_id",
            Method = HttpMethod.Post,
            Body = D(("data", ids)),
            EncryptType = KugouEncryptType.Android
        };
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthChannelSongAsync(string globalCollectionId, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/api/channel/v1/channel_get_song_audit_passed",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["global_collection_id"] = globalCollectionId;
        request.Params["pagesize"] = pageSize;
        request.Params["page"] = page;
        request.Params["is_filter"] = 0;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthChannelSongDetailAsync(string globalCollectionId, string fileId, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v2/post/get_song_detail",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["global_collection_id"] = globalCollectionId;
        request.Params["fileid"] = fileId;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthChannelSimilarAsync(string channelId, int? vipType = null, CancellationToken cancellationToken = default)
    {
        var resolvedVipType = vipType ?? (int.TryParse(CookieStore.Get("vip_type"), out var storedVipType) ? storedVipType : 0);
        var request = new KugouRequest
        {
            Path = "/youth/v1/channel/get_friendly_channel",
            Method = HttpMethod.Post,
            Body = D(
                ("area_code", 1),
                ("playlist_ver", 2),
                ("vip_type", resolvedVipType),
                ("platform", "ios")),
            EncryptType = KugouEncryptType.Android
        };
        request.Params["channel_id"] = channelId;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthChannelSubscribeAsync(string globalCollectionId, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v1/channel_subscribe",
            Method = HttpMethod.Post,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["global_collection_id"] = globalCollectionId;
        request.Params["source"] = 1;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthChannelUnsubscribeAsync(string globalCollectionId, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v1/channel_un_subscribe",
            Method = HttpMethod.Delete,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["global_collection_id"] = globalCollectionId;
        request.Params["source"] = 1;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthChannelAmwayAsync(string globalCollectionId, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/api/amway/v2/index",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["global_collection_id"] = globalCollectionId;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthDynamicAsync(CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v3/user/get_dynamic",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthDynamicRecentAsync(CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v3/user/recent_dynamic",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthUserSongAsync(long userId, int type = 0, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var request = new KugouRequest
        {
            Path = "/youth/v1/get_user_song_public",
            Method = HttpMethod.Get,
            EncryptType = KugouEncryptType.Android
        };
        request.Params["filter_video"] = 0;
        request.Params["type"] = type;
        request.Params["userid"] = userId;
        request.Params["pagesize"] = pageSize;
        request.Params["page"] = page;
        request.Params["is_filter"] = 0;
        return SendAsync(request, cancellationToken);
    }

    public Task<KugouResponse> YouthVipPlayReportAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var request = new KugouRequest
        {
            Path = "/youth/v1/ad/play_report",
            Method = HttpMethod.Post,
            Body = D(
                ("ad_id", 12307537187L),
                ("play_end", now),
                ("play_start", now - 30000)),
            EncryptType = KugouEncryptType.Android
        };
        return SendAsync(request, cancellationToken);
    }

    private void ApplyLoginOpenPlatCookies(KugouResponse response, string encryptKey)
    {
        using var doc = response.TryParseJson();
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
            return;

        var root = doc.RootElement;
        if (!root.TryGetProperty("status", out var status) || status.GetInt32() != 1)
            return;

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return;

        var issuedAt = DateTimeOffset.UtcNow;
        if (data.TryGetProperty("secu_params", out var secuParams) && secuParams.ValueKind == JsonValueKind.String)
        {
            var encrypted = secuParams.GetString();
            if (!string.IsNullOrWhiteSpace(encrypted))
            {
                try
                {
                    ApplyJsonCookies(KugouCrypto.AesDecryptHex(encrypted!, encryptKey), issuedAt);
                }
                catch
                {
                }
            }
        }

        SetCookieFromJson(data, "t1");
        SetCookieFromJson(data, "userid");
        SetCookieFromJson(data, "vip_type");
        SetCookieFromJson(data, "vip_token");
    }
}
