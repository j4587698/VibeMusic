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
}
