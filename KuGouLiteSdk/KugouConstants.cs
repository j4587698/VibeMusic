namespace KuGou.Lite;

internal static class KugouConstants
{
    public const int LiteAppId = 3116;
    public const int LiteClientVersion = 11440;
    public const int SourceAppId = 2919;
    public const int QrLoginAppId = 1001;
    public const int QrLoginWebAppId = 1014;

    // Some legacy tracker endpoints still use the normal appid when computing their nested tracker key in the JS project.
    public const int LegacyAppId = 1005;

    public const string AndroidSignatureSalt = "LnT6xpN3khm36zse0QzvmgTZ3waWdRSA";
    public const string WebSignatureSalt = "NVPh5oo715z5DIWAeQlhMDsWXXQV4hwt";
    public const string RegisterSignatureSalt = "1014";
    public const string LiteSignKeySalt = "185672dd44712f60bb1736df5a377e82";

    public const string DefaultUserAgent = "Android15-1070-11083-46-0-DiscoveryDRADProtocol-wifi";

    public const string PublicLiteRsaKey = """
-----BEGIN PUBLIC KEY-----
MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDECi0Np2UR87scwrvTr72L6oO01rBbbBPriSDFPxr3Z5syug0O24QyQO8bg27+0+4kBzTBTBOZ/WWU0WryL1JSXRTXLgFVxtzIY41Pe7lPOgsfTCn5kZcvKhYKJesKnnJDNr5/abvTGf+rHG3YRwsCHcQ08/q6ifSioBszvb3QiwIDAQAB
-----END PUBLIC KEY-----
""";
}
