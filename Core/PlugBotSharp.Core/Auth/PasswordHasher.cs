using System.Security.Cryptography;
using System.Text;

namespace PlugBotSharp.Core.Auth;

/// <summary>
/// 密码哈希，PBKDF2-SHA256 + 随机盐，格式 pbkdf2-sha256$迭代$salt$hash
/// </summary>
internal static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    /// <summary>
    /// 生成密码哈希
    /// </summary>
    /// <param name="password">明文密码</param>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join('$', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    /// <summary>
    /// 校验密码，比较用固定时间，避免时间差泄露
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="hash">保存的哈希</param>
    public static bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('$');

        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
