// =============================================================================
// 文件名：PasswordHasher.cs
// 功能描述：密码哈希工具类，使用 PBKDF2 (RFC2898) 加盐哈希算法。
//           替代原 SHA256 单层哈希，抵御彩虹表与暴力破解。
//           存储格式：PBKDF2:{iterations}:{saltBase64}:{hashBase64}
//           向后兼容：识别旧 SHA256 Base64 哈希（无前缀），验证成功后由调用方触发重新哈希。
//           放置于 Domain 层，供 Application（UserService）与 Infrastructure（种子数据）共用。
// =============================================================================
using System.Security.Cryptography;
using System.Text;

namespace IMDP.Domain.Services;

/// <summary>
/// 密码哈希工具
/// <para>使用 PBKDF2-HMAC-SHA256 算法，迭代 10000 次（OWASP 推荐下限），
/// 生成 16 字节 salt + 32 字节 hash，统一编码为 Base64 并带算法前缀存储。</para>
/// <para>支持识别旧版 SHA256 哈希（无前缀），验证通过后调用方可调用 <see cref="NeedsRehash"/>
/// 判断是否需要重新哈希以升级到 PBKDF2。</para>
/// </summary>
public static class PasswordHasher
{
    /// <summary>PBKDF2 迭代次数（OWASP 2023 推荐下限）</summary>
    private const int Iterations = 10000;

    /// <summary>Salt 长度（字节）</summary>
    private const int SaltSize = 16;

    /// <summary>哈希长度（字节，对应 SHA256 输出）</summary>
    private const int HashSize = 32;

    /// <summary>算法前缀，用于识别 PBKDF2 格式</summary>
    private const string Prefix = "PBKDF2";

    /// <summary>
    /// 计算密码的 PBKDF2 加盐哈希
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>格式为 <c>PBKDF2:{iterations}:{saltBase64}:{hashBase64}</c> 的哈希字符串</returns>
    public static string HashPassword(string password)
    {
        // 生成随机 salt
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        // 使用 PBKDF2 派生密钥
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Prefix}:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// 校验明文密码与已存储哈希是否匹配
    /// <para>支持两种格式：</para>
    /// <list type="bullet">
    ///   <item>PBKDF2 格式（<c>PBKDF2:iterations:salt:hash</c>）：使用 PBKDF2 验证</item>
    ///   <item>旧 SHA256 格式（无前缀，Base64 编码）：使用 SHA256 验证（向后兼容）</item>
    /// </list>
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="storedHash">已存储的哈希字符串</param>
    /// <returns>匹配返回 true，否则 false</returns>
    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        // PBKDF2 格式：PBKDF2:iterations:salt:hash
        if (storedHash.StartsWith(Prefix + ":", StringComparison.Ordinal))
        {
            return VerifyPbkdf2(password, storedHash);
        }

        // 旧 SHA256 格式：直接 Base64 编码的 SHA256 哈希
        return VerifyLegacySha256(password, storedHash);
    }

    /// <summary>
    /// 判断已存储哈希是否需要重新计算（即是否为旧 SHA256 格式）
    /// <para>调用方在登录验证成功后调用此方法，若返回 true 应使用 <see cref="HashPassword"/>
    /// 重新生成 PBKDF2 哈希并更新数据库，以完成算法升级。</para>
    /// </summary>
    /// <param name="storedHash">已存储的哈希字符串</param>
    /// <returns>旧 SHA256 格式返回 true；PBKDF2 格式返回 false</returns>
    public static bool NeedsRehash(string storedHash)
    {
        return !storedHash.StartsWith(Prefix + ":", StringComparison.Ordinal);
    }

    /// <summary>
    /// PBKDF2 格式验证
    /// </summary>
    private static bool VerifyPbkdf2(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        // 格式：PBKDF2:iterations:salt:hash
        if (parts.Length != 4) return false;

        if (!int.TryParse(parts[1], out var iterations)) return false;
        if (iterations <= 0) return false;

        byte[] salt, expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        // 使用存储的迭代次数重新派生密钥
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        // 固定时间比较，避免计时攻击
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>
    /// 旧 SHA256 格式验证（向后兼容）
    /// </summary>
    private static bool VerifyLegacySha256(string password, string storedHash)
    {
        try
        {
            var expectedBytes = Convert.FromBase64String(storedHash);
            var actualBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
