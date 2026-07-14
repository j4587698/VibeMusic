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

    public const string WxLiteAppId = "wx72b795aca60ad321";
    public const string WxLiteSecret = "33e486041e5e25729a4e3d2da7502f9a";

    public const string SimulatePublicKey = """
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAoW2+Ylo8ALePSQTP0xBF
lFmEOHvBD9tS+s7DBlfKEu3RzzvZTaX1JtYbX4+AVUqj6ARz8IM+CKByqGFvbHN/
W64XxNI+q7z36ajCL3VTJ2W5G9MCJitc6oGbire4NQfhaEq0nC+hxBWQvCbIFflA
2ItrLUbSU7z1bHA/a+jlQm4OWvY+IKnTryOJTPuT1yNOVjbJ8wBLKy2DgQr9pPqW
PmEQtGpR5IM9V8Kao6PaSdKYOWGbX3i2+RzIKhvZUxxtJwdVbqPlDPlW9h4/xIBc
56Lgvr4aIl8nFtwbj4UJVUTFuGrs0tY9H/tXvZ22dUCKuGxW/gW7ZF+gXz6vHtYa
rQIDAQAB
-----END PUBLIC KEY-----
""";

    public const string PublicLiteRsaKey = """
-----BEGIN PUBLIC KEY-----
MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDECi0Np2UR87scwrvTr72L6oO01rBbbBPriSDFPxr3Z5syug0O24QyQO8bg27+0+4kBzTBTBOZ/WWU0WryL1JSXRTXLgFVxtzIY41Pe7lPOgsfTCn5kZcvKhYKJesKnnJDNr5/abvTGf+rHG3YRwsCHcQ08/q6ifSioBszvb3QiwIDAQAB
-----END PUBLIC KEY-----
""";
}
