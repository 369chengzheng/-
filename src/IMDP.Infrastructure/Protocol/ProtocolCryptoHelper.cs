// =============================================================================
// 文件名：ProtocolCryptoHelper.cs
// 功能描述：协议无关 AES 加解密共用助手（静态工具类）
//           支持 ECB/CBC/CFB/OFB 等工作模式与 PKCS7/Zeros/None 等填充，
//           密钥与 IV 由调用方传入，不绑定任何具体协议。提供 ECB 便捷重载
//           （如 Iot100002 所用的 AES-ECB）以及 PKCS7 填充/去填充辅助方法，
//           供具体协议在组帧/解帧时对数据区做加解密。
// =============================================================================
using System.Security.Cryptography;

namespace IMDP.Infrastructure.Protocol;

/// <summary>
/// 协议无关 AES 加解密共用助手（静态工具类）
/// <para>所有方法均为协议无关：密钥、IV、工作模式与填充方式均由调用方显式传入，
/// 本类不内置任何具体协议的密钥或帧结构。具体协议（如 Iot100002、Viewshine）可据此
/// 自行决定对帧中哪些字节做加解密。</para>
/// </summary>
public static class ProtocolCryptoHelper
{
    /// <summary>AES 分组大小（字节），固定为 16（128 bit）。</summary>
    public const int BlockSize = 16;

    /// <summary>
    /// AES 加密（通用，工作模式/填充/密钥/IV 由调用方指定）
    /// </summary>
    /// <param name="plain">明文数据。</param>
    /// <param name="key">密钥（长度须为 16/24/32 字节，对应 AES-128/192/256）。</param>
    /// <param name="mode">分组密码工作模式，默认 ECB。</param>
    /// <param name="padding">填充方式，默认 PKCS7。</param>
    /// <param name="iv">初始化向量（ECB 模式可空；CBC/CFB/OFB 须为 16 字节）。</param>
    /// <returns>密文数据。</returns>
    public static byte[] AesEncrypt(
        byte[] plain,
        byte[] key,
        CipherMode mode = CipherMode.ECB,
        PaddingMode padding = PaddingMode.PKCS7,
        byte[]? iv = null)
    {
        ValidateKey(key);
        using var aes = Aes.Create();
        aes.Mode = mode;
        aes.Padding = padding;
        aes.Key = key;
        if (mode != CipherMode.ECB)
        {
            if (iv == null || iv.Length != BlockSize)
                throw new ArgumentException($"工作模式 {mode} 需要提供长度为 {BlockSize} 的 IV。", nameof(iv));
            aes.IV = iv;
        }

        using var transform = aes.CreateEncryptor();
        return transform.TransformFinalBlock(plain, 0, plain.Length);
    }

    /// <summary>
    /// AES 解密（通用，工作模式/填充/密钥/IV 由调用方指定）
    /// </summary>
    /// <param name="cipher">密文数据。</param>
    /// <param name="key">密钥（长度须为 16/24/32 字节，对应 AES-128/192/256）。</param>
    /// <param name="mode">分组密码工作模式，默认 ECB。</param>
    /// <param name="padding">填充方式，默认 PKCS7。</param>
    /// <param name="iv">初始化向量（ECB 模式可空；CBC/CFB/OFB 须为 16 字节）。</param>
    /// <returns>明文数据。</returns>
    public static byte[] AesDecrypt(
        byte[] cipher,
        byte[] key,
        CipherMode mode = CipherMode.ECB,
        PaddingMode padding = PaddingMode.PKCS7,
        byte[]? iv = null)
    {
        ValidateKey(key);
        using var aes = Aes.Create();
        aes.Mode = mode;
        aes.Padding = padding;
        aes.Key = key;
        if (mode != CipherMode.ECB)
        {
            if (iv == null || iv.Length != BlockSize)
                throw new ArgumentException($"工作模式 {mode} 需要提供长度为 {BlockSize} 的 IV。", nameof(iv));
            aes.IV = iv;
        }

        using var transform = aes.CreateDecryptor();
        return transform.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    /// <summary>
    /// AES-ECB 加密便捷方法（如 Iot100002 协议所用）
    /// <para>ECB 模式无需 IV，调用方仅需提供密钥。</para>
    /// </summary>
    /// <param name="plain">明文数据。</param>
    /// <param name="key">密钥（长度须为 16/24/32 字节）。</param>
    /// <param name="padding">填充方式，默认 PKCS7。</param>
    /// <returns>密文数据。</returns>
    public static byte[] AesEcbEncrypt(byte[] plain, byte[] key, PaddingMode padding = PaddingMode.PKCS7)
        => AesEncrypt(plain, key, CipherMode.ECB, padding);

    /// <summary>
    /// AES-ECB 解密便捷方法（如 Iot100002 协议所用）
    /// </summary>
    /// <param name="cipher">密文数据。</param>
    /// <param name="key">密钥（长度须为 16/24/32 字节）。</param>
    /// <param name="padding">填充方式，默认 PKCS7。</param>
    /// <returns>明文数据。</returns>
    public static byte[] AesEcbDecrypt(byte[] cipher, byte[] key, PaddingMode padding = PaddingMode.PKCS7)
        => AesDecrypt(cipher, key, CipherMode.ECB, padding);

    /// <summary>
    /// PKCS7 填充（便于在 Padding=None 时手动构造块对齐数据）
    /// </summary>
    /// <param name="data">待填充的数据。</param>
    /// <param name="blockSize">分组大小，默认 16 字节。</param>
    /// <returns>填充后的数据（长度为 blockSize 的整数倍）。</returns>
    public static byte[] Pkcs7Pad(byte[] data, int blockSize = BlockSize)
    {
        int pad = blockSize - (data.Length % blockSize);
        if (pad == 0) pad = blockSize;
        var output = new byte[data.Length + pad];
        Array.Copy(data, output, data.Length);
        for (int i = data.Length; i < output.Length; i++)
            output[i] = (byte)pad;
        return output;
    }

    /// <summary>
    /// 去除 PKCS7 填充（用于 Padding=None 时手动还原明文）
    /// </summary>
    /// <param name="data">已填充的数据。</param>
    /// <returns>去除填充后的数据；若填充非法则返回原数据。</returns>
    public static byte[] Pkcs7Unpad(byte[] data)
    {
        if (data.Length == 0) return data;
        int pad = data[data.Length - 1];
        if (pad < 1 || pad > BlockSize || pad > data.Length)
            return data;
        for (int i = data.Length - pad; i < data.Length; i++)
        {
            if (data[i] != pad) return data;
        }
        var output = new byte[data.Length - pad];
        Array.Copy(data, output, output.Length);
        return output;
    }

    /// <summary>
    /// 校验 AES 密钥长度是否合法（16/24/32 字节）。
    /// </summary>
    /// <param name="key">密钥字节。</param>
    /// <exception cref="ArgumentException">密钥长度不合法时抛出。</exception>
    private static void ValidateKey(byte[] key)
    {
        if (key == null || (key.Length != 16 && key.Length != 24 && key.Length != 32))
            throw new ArgumentException("AES 密钥长度必须为 16/24/32 字节（对应 AES-128/192/256）。", nameof(key));
    }
}
