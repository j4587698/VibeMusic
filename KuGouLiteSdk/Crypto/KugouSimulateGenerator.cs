using System.Security.Cryptography;
using System.Text;

namespace KuGou.Lite;

public static class KugouSimulateGenerator
{
    private const string Iv = "kugousecurity123";

    public static (string sid, string edt) Generate(string mid, string userid, string dfid, string? webglHash = null)
    {
        var sentinel = 0xffffffffL - Random.Shared.Next(20);

        webglHash ??= GenerateWebglHash();

        var key = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(KugouCrypto.RandomString(16))))[..16].ToLowerInvariant();

        var points = Ri(30, 60);
        var startX = Ri(200, 600);
        var startY = Ri(200, 500);
        var endX = Ri(500, 700);
        var endY = Ri(80, 150);

        var data = GenerateEdtData(startX, startY, endX, endY, points, sentinel);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var sidPlaintext = $"mid={mid};userid={userid};dfid={dfid};webgl={webglHash};webdriver=0;ts={ts};data={data}";

        var edt = KugouCrypto.AesCbcEncryptBase64(sidPlaintext, key, Iv);
        var sid = KugouCrypto.RsaOaepSha256EncryptBase64(key, KugouConstants.SimulatePublicKey);

        return (sid, edt);
    }

    private static string GenerateEdtData(int startX, int startY, int endX, int endY, int mousePoints, long sentinel)
    {
        var entries = new List<string>();
        var ts = 0L;
        var ei = 0;

        entries.Add(F5(0, 0));
        entries.Add(Fs5(0, sentinel));
        entries.Add(F5(0, 0));
        entries.Add(Fs5(0, sentinel));

        ts += Ri(5, 20);
        entries.Add(F6(ts, ei, 750, 500));
        entries.Add(Fs6(ei, 750, 500, sentinel));
        ei++;

        for (var i = 0; i < 3; i++)
        {
            ts += Ri(80, 600);
            entries.Add(F5(ts, ei));
            entries.Add(Fs5(ei, sentinel));
            ei++;
        }

        var path = BezierPath(startX, startY, endX, endY, mousePoints);
        var si = 0;
        for (var i = 0; i < path.Length; i++)
        {
            var (x, y) = path[i];
            ts += Ri(8, 50);
            entries.Add(F3(ts, si, (int)(x + 0.5), (int)(y + 0.5)));
            entries.Add(Fs3(si, (int)(x + 0.5), (int)(y + 0.5), sentinel));

            if (i > 0 && i % 12 == 0)
            {
                ts += Ri(20, 60);
                entries.Add(F5(ts, ei));
                entries.Add(Fs5(ei, sentinel));
                ei++;
            }
            si = (si + 1) % 2;
        }

        ts += Ri(5, 30);
        entries.Add(F3(ts, 1, endX + Ri(-5, 5), endY + Ri(-5, 5)));
        entries.Add(Fs3(1, endX, endY, sentinel));

        return string.Join(':', entries);
    }

    private static (double x, double y)[] BezierPath(int sx, int sy, int ex, int ey, int n)
    {
        var c1x = sx + (ex - sx) * 0.3 + Ri(-80, 80);
        var c1y = sy + (ey - sy) * 0.2 + Ri(-60, 60);
        var c2x = sx + (ex - sx) * 0.7 + Ri(-60, 60);
        var c2y = sy + (ey - sy) * 0.8 + Ri(-40, 40);

        var pts = new (double x, double y)[n + 1];
        for (var i = 0; i <= n; i++)
        {
            var t = (double)i / n;
            var u = 1.0 - t;

            var x = u * u * u * sx + 3 * u * u * t * c1x + 3 * u * t * t * c2x + t * t * t * ex;
            var y = u * u * u * sy + 3 * u * u * t * c1y + 3 * u * t * t * c2y + t * t * t * ey;

            var jitter = Math.Max(0.5, 3.0 - t * 2.5);
            pts[i] = (
                x + (Random.Shared.NextDouble() - 0.5) * jitter,
                y + (Random.Shared.NextDouble() - 0.5) * jitter
            );
        }
        return pts;
    }

    private static string F3(long t, int i, int x, int y) => $"3,{t},{i},{x},{y}";
    private static string F5(long t, int i) => $"5,{t},{i}";
    private static string F6(long t, int i, int x, int y) => $"6,{t},{i},{x},{y}";
    private static string Fs3(int i, int x, int y, long sentinel) => $"3,{sentinel},{i},{x},{y}";
    private static string Fs5(int i, long sentinel) => $"5,{sentinel},{i}";
    private static string Fs6(int i, int x, int y, long sentinel) => $"6,{sentinel},{i},{x},{y}";

    private static int Ri(int min, int max) => Random.Shared.Next(min, max + 1);

    private static string GenerateWebglHash()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
