using System.Globalization;
using System.Collections;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KuGou.Lite;

internal sealed record KugouAesResult(string CipherHex, string Key);
internal sealed record KugouPlaylistAesResult(string CipherBase64, string Key);

internal static class KugouCrypto
{
    public static string RandomString(int len = 16)
    {
        const string chars = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Span<byte> bytes = stackalloc byte[len];
        RandomNumberGenerator.Fill(bytes);
        var builder = new StringBuilder(len);
        foreach (var value in bytes)
        {
            builder.Append(chars[value % chars.Length]);
        }

        return builder.ToString();
    }

    public static string NewGuidString() => Guid.NewGuid().ToString("D");

    public static string Md5Hex(object? data)
    {
        var bytes = data switch
        {
            null => Array.Empty<byte>(),
            string s => Encoding.UTF8.GetBytes(s),
            byte[] b => b,
            _ => Encoding.UTF8.GetBytes(ToJson(data))
        };

        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }

    public static string CalculateMid(string value)
    {
        var digest = Md5Hex(value);
        return BigInteger.Parse("0" + digest, NumberStyles.HexNumber, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    }

    public static string ToJson(object data)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteJsonValue(writer, data);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;
            case byte number:
                writer.WriteNumberValue(number);
                return;
            case sbyte number:
                writer.WriteNumberValue(number);
                return;
            case short number:
                writer.WriteNumberValue(number);
                return;
            case ushort number:
                writer.WriteNumberValue(number);
                return;
            case int number:
                writer.WriteNumberValue(number);
                return;
            case uint number:
                writer.WriteNumberValue(number);
                return;
            case long number:
                writer.WriteNumberValue(number);
                return;
            case ulong number:
                writer.WriteNumberValue(number);
                return;
            case float number:
                writer.WriteNumberValue(number);
                return;
            case double number:
                writer.WriteNumberValue(number);
                return;
            case decimal number:
                writer.WriteNumberValue(number);
                return;
            case JsonElement element:
                element.WriteTo(writer);
                return;
            case JsonNode node:
                node.WriteTo(writer);
                return;
            case byte[] bytes:
                writer.WriteBase64StringValue(bytes);
                return;
            case IDictionary<string, object?> objectDictionary:
                WriteStringObjectDictionary(writer, objectDictionary);
                return;
            case IDictionary<string, string?> stringDictionary:
                WriteStringStringDictionary(writer, stringDictionary);
                return;
            case IDictionary dictionary:
                WriteDictionary(writer, dictionary);
                return;
            case IEnumerable enumerable when value is not string:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    WriteJsonValue(writer, item);
                }

                writer.WriteEndArray();
                return;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
        }
    }

    private static void WriteStringObjectDictionary(Utf8JsonWriter writer, IDictionary<string, object?> dictionary)
    {
        writer.WriteStartObject();
        foreach (var item in dictionary)
        {
            writer.WritePropertyName(item.Key);
            WriteJsonValue(writer, item.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteStringStringDictionary(Utf8JsonWriter writer, IDictionary<string, string?> dictionary)
    {
        writer.WriteStartObject();
        foreach (var item in dictionary)
        {
            writer.WritePropertyName(item.Key);
            if (item.Value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(item.Value);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteDictionary(Utf8JsonWriter writer, IDictionary dictionary)
    {
        writer.WriteStartObject();
        foreach (DictionaryEntry item in dictionary)
        {
            writer.WritePropertyName(Convert.ToString(item.Key, CultureInfo.InvariantCulture) ?? string.Empty);
            WriteJsonValue(writer, item.Value);
        }

        writer.WriteEndObject();
    }

    public static KugouAesResult AesEncryptHex(object data)
    {
        var tempKey = RandomString(16).ToLowerInvariant();
        var key = Md5Hex(tempKey)[..32];
        var iv = key[^16..];
        return new KugouAesResult(AesEncryptHex(data, key, iv), tempKey);
    }

    public static string AesEncryptHex(object data, string key, string iv)
    {
        var plain = data is string s ? Encoding.UTF8.GetBytes(s) : Encoding.UTF8.GetBytes(ToJson(data));
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        using var encryptor = aes.CreateEncryptor();
        return Convert.ToHexString(encryptor.TransformFinalBlock(plain, 0, plain.Length)).ToLowerInvariant();
    }

    public static string AesDecryptHex(string cipherHex, string key, string? iv = null)
    {
        if (iv is null)
        {
            key = Md5Hex(key)[..32];
            iv = key[^16..];
        }

        var cipher = Convert.FromHexString(cipherHex);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        using var decryptor = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(cipher, 0, cipher.Length));
    }

    public static string RsaRawEncryptHex(object data)
    {
        var payload = NormalizeBuffer(data);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(KugouConstants.PublicLiteRsaKey);
        var parameters = rsa.ExportParameters(false);
        var keyLength = parameters.Modulus!.Length;
        if (payload.Length > keyLength)
        {
            throw new InvalidOperationException("RSA raw payload length exceeds key size.");
        }

        var padded = new byte[keyLength];
        Buffer.BlockCopy(payload, 0, padded, 0, payload.Length);

        var modulus = new BigInteger(parameters.Modulus, isUnsigned: true, isBigEndian: true);
        var exponent = new BigInteger(parameters.Exponent!, isUnsigned: true, isBigEndian: true);
        var message = new BigInteger(padded, isUnsigned: true, isBigEndian: true);
        var encrypted = BigInteger.ModPow(message, exponent, modulus);
        return encrypted.ToString("x", CultureInfo.InvariantCulture).PadLeft(keyLength * 2, '0');
    }

    public static string RsaPkcs1EncryptHex(object data)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(KugouConstants.PublicLiteRsaKey);
        return Convert.ToHexString(rsa.Encrypt(NormalizeBuffer(data), RSAEncryptionPadding.Pkcs1)).ToLowerInvariant();
    }

    public static KugouPlaylistAesResult PlaylistAesEncrypt(object data)
    {
        var key = RandomString(6).ToLowerInvariant();
        var md5 = Md5Hex(key);
        var encryptKey = md5[..16];
        var iv = md5[16..32];
        var plain = Encoding.UTF8.GetBytes(data is string s ? s : ToJson(data));

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(encryptKey);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        using var encryptor = aes.CreateEncryptor();
        return new KugouPlaylistAesResult(Convert.ToBase64String(encryptor.TransformFinalBlock(plain, 0, plain.Length)), key);
    }

    public static string PlaylistAesDecrypt(string cipherBase64, string key)
    {
        var md5 = Md5Hex(key);
        var encryptKey = md5[..16];
        var iv = md5[16..32];
        var cipher = Convert.FromBase64String(cipherBase64);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(encryptKey);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        using var decryptor = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(cipher, 0, cipher.Length));
    }

    public static string SignatureAndroidParams(IDictionary<string, object?> parameters, string? body)
    {
        var payload = string.Concat(parameters.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={FormatValueForSignature(item.Value)}"));
        return Md5Hex($"{KugouConstants.AndroidSignatureSalt}{payload}{body ?? string.Empty}{KugouConstants.AndroidSignatureSalt}");
    }

    public static string SignatureWebParams(IDictionary<string, object?> parameters)
    {
        var payload = string.Concat(parameters.Select(item => $"{item.Key}={item.Value}").OrderBy(item => item, StringComparer.Ordinal));
        return Md5Hex($"{KugouConstants.WebSignatureSalt}{payload}{KugouConstants.WebSignatureSalt}");
    }

    public static string SignatureRegisterParams(IDictionary<string, object?> parameters)
    {
        var payload = string.Concat(parameters.Select(item => FormatValueForSignature(item.Value)).OrderBy(item => item, StringComparer.Ordinal));
        return Md5Hex($"{KugouConstants.RegisterSignatureSalt}{payload}{KugouConstants.RegisterSignatureSalt}");
    }

    public static string SignKey(string hash, string mid, string? userid = null, int appid = KugouConstants.LiteAppId)
    {
        return Md5Hex($"{hash}{KugouConstants.LiteSignKeySalt}{appid}{mid}{userid ?? "0"}");
    }

    public static string SignParamsKey(long data, int appid = KugouConstants.LiteAppId, int clientVersion = KugouConstants.LiteClientVersion)
    {
        return Md5Hex($"{appid}{KugouConstants.AndroidSignatureSalt}{clientVersion}{data}");
    }

    public static string DecodeKrcOrBase64Lyric(string contentBase64, bool isLrcOrText)
    {
        var bytes = Convert.FromBase64String(contentBase64);
        if (isLrcOrText)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return DecodeKrc(bytes);
    }

    private static string DecodeKrc(byte[] bytes)
    {
        if (bytes.Length <= 4)
        {
            return string.Empty;
        }

        byte[] key = [64, 71, 97, 119, 94, 50, 116, 71, 81, 54, 49, 45, 206, 210, 110, 105];
        var payload = bytes[4..];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(payload[i] ^ key[i % key.Length]);
        }

        try
        {
            using var input = new MemoryStream(payload);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string FormatValueForSignature(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => s,
            JsonElement element => element.GetRawText(),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => ToJson(value)
        };
    }

    public static string FormatValueForQuery(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            string s => s,
            JsonElement element => element.GetRawText(),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => ToJson(value)
        };
    }

    private static byte[] NormalizeBuffer(object data)
    {
        return data is string s ? Encoding.UTF8.GetBytes(s) : Encoding.UTF8.GetBytes(ToJson(data));
    }
}

public static class KugouLyricDecoder
{
    public static string Decode(string contentBase64, bool isLrcOrText = false)
    {
        return KugouCrypto.DecodeKrcOrBase64Lyric(contentBase64, isLrcOrText);
    }
}
