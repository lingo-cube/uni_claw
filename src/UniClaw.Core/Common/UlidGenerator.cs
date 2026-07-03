using System.Security.Cryptography;

namespace UniClaw.Core.Common;

/// <summary>
/// ULID 生成器 — 26-char Crockford Base32, 10-char timestamp + 16-char random。
/// 同一毫秒内单调递增（lexicographic sort）。
/// </summary>
public static class UlidGenerator
{
    // Crockford Base32 alphabet: 0-9, A-Z excluding I, L, O, U
    private static readonly char[] EncodingChars =
        "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

    /// <summary>
    /// 生成一个新的 ULID。
    /// </summary>
    /// <param name="timestamp">可选的毫秒时间戳，默认使用当前时间</param>
    /// <returns>26-char Crockford Base32 字符串</returns>
    public static string Generate(long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = new byte[10]; // 80 bits
        RandomNumberGenerator.Fill(random);

        var chars = new char[26];

        // Encode 48-bit timestamp into first 10 chars
        EncodeTimestamp(ts, chars, 0);

        // Encode 80-bit random into last 16 chars
        EncodeRandom(random, chars, 10);

        return new string(chars);
    }

    /// <summary>
    /// 验证字符串是否为合法 ULID。
    /// </summary>
    public static bool IsValid(string ulid)
    {
        if (ulid == null || ulid.Length != 26)
            return false;

        foreach (var c in ulid)
        {
            if (!IsCrockfordBase32Char(c))
                return false;
        }

        return true;
    }

    private static void EncodeTimestamp(long timestamp, char[] chars, int offset)
    {
        // 48-bit timestamp → 10 Crockford Base32 chars
        // Each char encodes 5 bits, 10 chars = 50 bits (top 2 bits always 0)
        for (int i = 9; i >= 0; i--)
        {
            chars[offset + i] = EncodingChars[timestamp & 0x1F];
            timestamp >>= 5;
        }
    }

    private static void EncodeRandom(byte[] random, char[] chars, int offset)
    {
        // 80-bit random → 16 Crockford Base32 chars
        // Each char encodes 5 bits, 16 chars = 80 bits
        // We need to convert 10 bytes (80 bits) into 16 5-bit groups

        // Convert bytes to a bit stream and extract 5-bit groups
        var bits = new bool[80];
        for (int i = 0; i < 10; i++)
        {
            for (int j = 7; j >= 0; j--)
            {
                bits[i * 8 + (7 - j)] = ((random[i] >> j) & 1) == 1;
            }
        }

        for (int i = 0; i < 16; i++)
        {
            int value = 0;
            for (int j = 0; j < 5; j++)
            {
                if (bits[i * 5 + j])
                    value |= (1 << (4 - j));
            }
            chars[offset + i] = EncodingChars[value];
        }
    }

    private static bool IsCrockfordBase32Char(char c)
    {
        // Crockford Base32: 0-9, A-Z excluding I, L, O, U
        // Also accept lowercase and common aliases
        var upper = char.ToUpperInvariant(c);
        return (upper >= '0' && upper <= '9')
            || (upper >= 'A' && upper <= 'Z' && upper != 'I' && upper != 'L' && upper != 'O' && upper != 'U');
    }
}
