using System;
using System.Buffers.Binary;
using System.Text;

namespace KuGouLiteSdk.Crypto
{
    public static class KugouTeaCrypto
    {
        private static readonly byte[] _v2Magic = [0x51, 0x51, 0x4d, 0x75, 0x73, 0x69, 0x63, 0x20, 0x45, 0x6e, 0x63, 0x56, 0x32, 0x2c, 0x4b, 0x65, 0x79, 0x3a]; // "QQMusic EncV2,Key:"
        private static readonly byte[] _v2TeaKey1 = [0x33, 0x38, 0x36, 0x5a, 0x4a, 0x59, 0x21, 0x40, 0x23, 0x2a, 0x24, 0x25, 0x5e, 0x26, 0x29, 0x28]; // "386ZJY!@#*$%^&)("
        private static readonly byte[] _v2TeaKey2 = [0x2a, 0x2a, 0x23, 0x21, 0x28, 0x23, 0x24, 0x25, 0x26, 0x5e, 0x61, 0x31, 0x63, 0x5a, 0x2c, 0x54]; // "**#!(#$%&^a1cZ,T"
        private static readonly byte[] _legacyV2TeaKey1 = [0x38, 0x64, 0x36, 0x34, 0x61, 0x38, 0x36, 0x33, 0x34, 0x31, 0x31, 0x63, 0x62, 0x61, 0x61, 0x37]; // "8d64a863411cbaa7"
        private static readonly byte[] _legacyV2TeaKey2 = [0x65, 0x38, 0x32, 0x36, 0x64, 0x35, 0x34, 0x39, 0x63, 0x35, 0x32, 0x33, 0x39, 0x63, 0x33, 0x61]; // "e826d549c5239c3a"

        public static byte[] DecryptEKey(string base64EKey)
        {
            var keyBytes = Convert.FromBase64String(base64EKey.TrimEnd('\0'));
            var key = keyBytes.AsSpan();
            var length = key.Length;

            if (key.Length >= 18 && key[..18].SequenceEqual(_v2Magic))
            {
                var payload = key[18..];
                if (!TryDecryptV2Key(payload, _v2TeaKey1, _v2TeaKey2, out keyBytes) &&
                    !TryDecryptV2Key(payload, _legacyV2TeaKey1, _legacyV2TeaKey2, out keyBytes))
                {
                    throw new InvalidOperationException("Failed to decrypt EncV2 ekey.");
                }

                key = keyBytes.AsSpan();
                length = key.Length;

                if (length < 8) throw new InvalidOperationException("Key length invalid after v2 decryption.");
            }

            if (length % 8 != 0) throw new InvalidOperationException("Key length must be multiple of 8.");

            var teaKey = new byte[16];
            for (int i = 0; i < 8; i++)
            {
                teaKey[2 * i] = (byte)(Math.Abs(Math.Tan(106 + i * 0.1)) * 100);
                teaKey[2 * i + 1] = key[i];
            }

            DecryptTeaCbc(teaKey, key[8..], out length);
            return key[..(length + 8)].ToArray();
        }

        private static bool TryDecryptV2Key(
            ReadOnlySpan<byte> payload,
            ReadOnlySpan<byte> teaKey1,
            ReadOnlySpan<byte> teaKey2,
            out byte[] key)
        {
            var buffer = payload.ToArray();

            try
            {
                DecryptTeaCbc(teaKey1, buffer, out var length);
                DecryptTeaCbc(teaKey2, buffer.AsSpan(0, length), out length);

                key = Convert.FromBase64String(Encoding.ASCII.GetString(buffer.AsSpan(0, length)));
                return key.Length >= 8 && key.Length % 8 == 0;
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
            {
                key = [];
                return false;
            }
        }

        private static void DecryptTeaCbc(ReadOnlySpan<byte> teaKey, Span<byte> buffer, out int length)
        {
            const int saltLength = 2;
            const int zeroLength = 7;

            length = buffer.Length;
            if (length % 8 != 0) throw new InvalidOperationException("Key length invalid for TEA CBC.");

            var raw = new byte[length];
            buffer.CopyTo(raw);
            
            var res = new byte[length];
            buffer.CopyTo(res);

            var tea = new Tea(teaKey, 32);

            tea.DecryptBlock(res.AsSpan(0, 8));
            int padLength = res[0] & 0x7;

            for (int i = 8; i < length; i += 8)
            {
                var cur = res.AsSpan(i, 8);
                var pre = res.AsSpan(i - 8, 8);

                for (int j = 0; j < 8; j++)
                {
                    cur[j] ^= pre[j];
                }

                tea.DecryptBlock(cur);
            }

            for (int i = 8; i < length; i += 8)
            {
                var window = res.AsSpan(i, 8);
                var s = raw.AsSpan(i - 8, 8);

                for (int j = 0; j < 8; j++)
                {
                    window[j] ^= s[j];
                }
            }

            for (int i = length - zeroLength; i < length; i++)
            {
                if (res[i] != 0) throw new InvalidOperationException("Zero check result failed in TEA CBC.");
            }

            var data = res.AsSpan(1 + padLength + saltLength, length - zeroLength - (1 + padLength + saltLength));
            data.CopyTo(buffer);

            length = data.Length;
        }

        private readonly struct Tea
        {
            private const int _sizeBlock = 8;
            private const int _sizeKey = 16;
            private const uint _delta = 0x9e3779b9;

            private readonly uint[] _key;
            private readonly uint _rounds;

            internal Tea(ReadOnlySpan<byte> key, uint rounds = 64)
            {
                if (key.Length != _sizeKey)
                    throw new ArgumentException($"Key length should be {_sizeKey}. (got {key.Length})", nameof(key));
                else if (rounds % 1 != 0)
                    throw new ArgumentException($"Round count should be even. (got {rounds})", nameof(rounds));

                _key = new[]
                {
                    BinaryPrimitives.ReadUInt32BigEndian(key[..4]),
                    BinaryPrimitives.ReadUInt32BigEndian(key[4..8]),
                    BinaryPrimitives.ReadUInt32BigEndian(key[8..12]),
                    BinaryPrimitives.ReadUInt32BigEndian(key[12..]),
                };
                _rounds = rounds;
            }

            internal readonly void DecryptBlock(Span<byte> buffer)
            {
                if (buffer.Length < _sizeBlock)
                    throw new ArgumentException($"Decryption buffer size should be {_sizeBlock} at least.", nameof(buffer));

                uint vl = BinaryPrimitives.ReadUInt32BigEndian(buffer[..4]);
                uint vh = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..8]);

                uint sum = _delta * (_rounds / 2);

                for (int i = 0; i < _rounds / 2; i++)
                {
                    vh -= ((vl << 4) + _key[2]) ^ (vl + sum) ^ ((vl >> 5) + _key[3]);
                    vl -= ((vh << 4) + _key[0]) ^ (vh + sum) ^ ((vh >> 5) + _key[1]);
                    sum -= _delta;
                }

                BinaryPrimitives.WriteUInt32BigEndian(buffer[..4], vl);
                BinaryPrimitives.WriteUInt32BigEndian(buffer[4..8], vh);
            }
        }
    }
}
