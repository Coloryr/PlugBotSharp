using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace PlugBotSharp.Core.Crypto;

/// <summary>
/// Ed25519 签名算法（RFC 8032），只用于小数据签名，不追求常量时间
/// </summary>
public static class Ed25519
{
    /// <summary>
    /// 私钥种子长度
    /// </summary>
    public const int SeedSize = 32;
    /// <summary>
    /// 公钥长度
    /// </summary>
    public const int PublicKeySize = 32;
    /// <summary>
    /// 签名长度
    /// </summary>
    public const int SignatureSize = 64;

    /// <summary>
    /// 域参数 p = 2^255 - 19
    /// </summary>
    private static readonly BigInteger P = (BigInteger.One << 255) - 19;

    /// <summary>
    /// 子群阶 L = 2^252 + 27742317777372353535851937790883648493
    /// </summary>
    private static readonly BigInteger L = (BigInteger.One << 252)
        + BigInteger.Parse("27742317777372353535851937790883648493", CultureInfo.InvariantCulture);

    /// <summary>
    /// 曲线参数 d = -121665 / 121666
    /// </summary>
    private static readonly BigInteger D = Mod(-121665 * ModInverse(121666), P);

    /// <summary>
    /// 2d，点加公式里会用到
    /// </summary>
    private static readonly BigInteger D2 = Mod(D * 2, P);

    /// <summary>
    /// sqrt(-1)，用于开方失败时回退
    /// </summary>
    private static readonly BigInteger SqrtM1 = BigInteger.ModPow(2, (P - 1) / 4, P);

    /// <summary>
    /// 基点 B
    /// </summary>
    private static readonly Point BasePoint = new(
        BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202", CultureInfo.InvariantCulture),
        BigInteger.Parse("46316835694926478169428394003475163141307993866256225615783033603165251855960", CultureInfo.InvariantCulture),
        BigInteger.One,
        Mod(
            BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202", CultureInfo.InvariantCulture)
            * BigInteger.Parse("46316835694926478169428394003475163141307993866256225615783033603165251855960", CultureInfo.InvariantCulture),
            P));

    /// <summary>
    /// 单位元
    /// </summary>
    private static readonly Point Identity = new(BigInteger.Zero, BigInteger.One, BigInteger.One, BigInteger.Zero);

    /// <summary>
    /// 由种子计算公钥
    /// </summary>
    /// <param name="seed">32 字节私钥种子</param>
    /// <returns>32 字节公钥</returns>
    public static byte[] PublicKeyFromSeed(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != SeedSize)
        {
            throw new ArgumentException("seed must be 32 bytes", nameof(seed));
        }

        var (scalar, _) = ExpandSeed(seed);

        return Encode(ScalarMul(BasePoint, scalar));
    }

    /// <summary>
    /// 生成签名
    /// </summary>
    /// <param name="seed">32 字节私钥种子</param>
    /// <param name="message">待签名数据</param>
    /// <returns>64 字节签名，前 32 字节是 R，后 32 字节是 S</returns>
    public static byte[] Sign(ReadOnlySpan<byte> seed, ReadOnlySpan<byte> message)
    {
        if (seed.Length != SeedSize)
        {
            throw new ArgumentException("seed must be 32 bytes", nameof(seed));
        }

        var (scalar, prefix) = ExpandSeed(seed);
        var publicKey = Encode(ScalarMul(BasePoint, scalar));

        // r = H(prefix || M) mod L
        var buffer = new byte[SeedSize + message.Length];
        prefix.CopyTo(buffer, 0);
        message.CopyTo(buffer.AsSpan(SeedSize));
        var r = Mod(FromLittleEndian(SHA512.HashData(buffer)), L);
        var encodedR = Encode(ScalarMul(BasePoint, r));

        // k = H(R || A || M) mod L
        var hashBuffer = new byte[SignatureSize + message.Length];
        encodedR.CopyTo(hashBuffer, 0);
        publicKey.CopyTo(hashBuffer, PublicKeySize);
        message.CopyTo(hashBuffer.AsSpan(SignatureSize));
        var k = Mod(FromLittleEndian(SHA512.HashData(hashBuffer)), L);

        // S = (r + k * a) mod L
        var s = Mod(r + k * scalar, L);

        var signature = new byte[SignatureSize];
        encodedR.CopyTo(signature, 0);
        ToLittleEndian(s, 32).CopyTo(signature, 32);

        return signature;
    }

    /// <summary>
    /// 验证签名
    /// </summary>
    /// <param name="publicKey">32 字节公钥</param>
    /// <param name="message">原文</param>
    /// <param name="signature">64 字节签名</param>
    /// <returns>是否合法</returns>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != PublicKeySize || signature.Length != SignatureSize)
        {
            return false;
        }

        var a = Decode(publicKey);
        var r = Decode(signature[..32]);
        if (a is null || r is null)
        {
            return false;
        }

        var s = FromLittleEndian(signature[32..]);
        if (s >= L)
        {
            return false;
        }

        // k = H(R || A || M) mod L
        var buffer = new byte[SignatureSize + message.Length];
        signature[..32].CopyTo(buffer);
        publicKey.CopyTo(buffer.AsSpan(PublicKeySize));
        message.CopyTo(buffer.AsSpan(SignatureSize));
        var k = Mod(FromLittleEndian(SHA512.HashData(buffer)), L);

        // 判断 [S]B 是否等于 R + [k]A
        var left = ScalarMul(BasePoint, s);
        var right = Add(r.Value, ScalarMul(a.Value, k));

        return Encode(left).AsSpan().SequenceEqual(Encode(right));
    }

    /// <summary>
    /// 由种子展开出私钥标量和签名前缀
    /// </summary>
    private static (BigInteger Scalar, byte[] Prefix) ExpandSeed(ReadOnlySpan<byte> seed)
    {
        var hash = SHA512.HashData(seed);
        var scalarBytes = hash.AsSpan(0, 32).ToArray();

        scalarBytes[0] &= 248;
        scalarBytes[31] &= 127;
        scalarBytes[31] |= 64;

        return (FromLittleEndian(scalarBytes), hash.AsSpan(32, 32).ToArray());
    }

    /// <summary>
    /// 点加
    /// </summary>
    private static Point Add(in Point p, in Point q)
    {
        var a = Mod((p.Y - p.X) * (q.Y - q.X), P);
        var b = Mod((p.Y + p.X) * (q.Y + q.X), P);
        var c = Mod(p.T * D2 * q.T, P);
        var d = Mod(2 * p.Z * q.Z, P);
        var e = b - a;
        var f = d - c;
        var g = d + c;
        var h = b + a;

        return new Point(Mod(e * f, P), Mod(g * h, P), Mod(f * g, P), Mod(e * h, P));
    }

    /// <summary>
    /// 倍点
    /// </summary>
    private static Point Double(in Point p)
    {
        var a = Mod(p.X * p.X, P);
        var b = Mod(p.Y * p.Y, P);
        var c = Mod(2 * p.Z * p.Z, P);
        // 曲线参数 a = -1
        var d = Mod(-a, P);
        var e = Mod((p.X + p.Y) * (p.X + p.Y) - a - b, P);
        var g = Mod(d + b, P);
        var f = Mod(g - c, P);
        var h = Mod(d - b, P);

        return new Point(Mod(e * f, P), Mod(g * h, P), Mod(f * g, P), Mod(e * h, P));
    }

    /// <summary>
    /// 标量乘
    /// </summary>
    private static Point ScalarMul(in Point point, BigInteger scalar)
    {
        var result = Identity;
        var addend = point;

        while (scalar > BigInteger.Zero)
        {
            if (!scalar.IsEven)
            {
                result = Add(result, addend);
            }

            addend = Double(addend);
            scalar >>= 1;
        }

        return result;
    }

    /// <summary>
    /// 点编码成 32 字节：y 的小端表示，最高位存 x 的奇偶
    /// </summary>
    private static byte[] Encode(in Point point)
    {
        var zi = ModInverse(point.Z);
        var x = Mod(point.X * zi, P);
        var y = Mod(point.Y * zi, P);

        var bytes = ToLittleEndian(y, 32);
        if (!x.IsEven)
        {
            bytes[31] |= 0x80;
        }

        return bytes;
    }

    /// <summary>
    /// 32 字节解码成点
    /// </summary>
    private static Point? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != 32)
        {
            return null;
        }

        var bytes = data.ToArray();
        var sign = (bytes[31] & 0x80) != 0;
        bytes[31] &= 0x7F;

        var x = RecoverX(FromLittleEndian(bytes), sign);
        if (x is null)
        {
            return null;
        }

        var y = FromLittleEndian(bytes);

        return new Point(x.Value, y, BigInteger.One, Mod(x.Value * y, P));
    }

    /// <summary>
    /// 由 y 和 x 的奇偶还原 x
    /// </summary>
    private static BigInteger? RecoverX(BigInteger y, bool sign)
    {
        if (y >= P)
        {
            return null;
        }

        var y2 = Mod(y * y, P);
        var u = Mod(y2 - 1, P);
        var v = Mod(D * y2 + 1, P);

        // x = u * v^3 * (u * v^7)^((p - 5) / 8)
        var v3 = Mod(v * v * v, P);
        var v7 = Mod(v3 * v3 * v, P);
        var x = Mod(u * v3 * BigInteger.ModPow(Mod(u * v7, P), (P - 5) / 8, P), P);

        var vx2 = Mod(v * x * x, P);
        if (vx2 == u)
        {
            // 开方成功
        }
        else if (vx2 == Mod(-u, P))
        {
            x = Mod(x * SqrtM1, P);
        }
        else
        {
            return null;
        }

        var odd = !x.IsEven;
        if (x.IsZero && sign)
        {
            return null;
        }

        if (odd != sign)
        {
            x = Mod(-x, P);
        }

        return x;
    }

    /// <summary>
    /// 取模，结果保证非负
    /// </summary>
    private static BigInteger Mod(BigInteger value, BigInteger modulus)
    {
        var result = value % modulus;

        return result.Sign < 0 ? result + modulus : result;
    }

    /// <summary>
    /// 模逆，模数为素数时可以直接用费马小定理
    /// </summary>
    private static BigInteger ModInverse(BigInteger value)
    {
        return BigInteger.ModPow(Mod(value, P), P - 2, P);
    }

    /// <summary>
    /// 小端字节转整数
    /// </summary>
    private static BigInteger FromLittleEndian(ReadOnlySpan<byte> data)
    {
        return new BigInteger(data, isUnsigned: true, isBigEndian: false);
    }

    /// <summary>
    /// 整数转定长小端字节
    /// </summary>
    private static byte[] ToLittleEndian(BigInteger value, int size)
    {
        var bytes = new byte[size];
        var raw = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        Array.Copy(raw, bytes, Math.Min(raw.Length, size));

        return bytes;
    }

    /// <summary>
    /// 扩展坐标点 (X:Y:Z:T)，x = X/Z，y = Y/Z，T = XY/Z
    /// </summary>
    private readonly struct Point(BigInteger x, BigInteger y, BigInteger z, BigInteger t)
    {
        public readonly BigInteger X = x;
        public readonly BigInteger Y = y;
        public readonly BigInteger Z = z;
        public readonly BigInteger T = t;
    }
}
