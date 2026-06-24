using System.Security.Cryptography;
using System.Text;

namespace MutsumiPet.Services;

/// <summary>
/// 使用 Windows DPAPI 加密和解密 API Key。
/// 加密范围限定为当前 Windows 用户（CurrentUser）。
/// </summary>
public static class ApiKeyProtection
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MutsumiPet.ApiKey.v1");

    /// <summary>
    /// 使用 DPAPI 加密明文 API Key，返回 Base64 编码的密文。
    /// 空值或空白返回 null。
    /// </summary>
    public static string? Protect(string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return null;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// 使用 DPAPI 解密 Base64 编码的密文，返回明文 API Key。
    /// 空值返回 null，解密失败返回 null。
    /// </summary>
    public static string? Unprotect(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return null;
        }

        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// 安全地遮蔽 API Key 用于日志或调试输出。
    /// 只保留前 4 位和后 4 位，其余用 * 替代。
    /// </summary>
    public static string Mask(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "<empty>";
        }

        if (key.Length <= 8)
        {
            return new string('*', key.Length);
        }

        return key[..4] + new string('*', key.Length - 8) + key[^4..];
    }
}
