using System.Security.Cryptography;
using System.Text;

namespace MedFlow.Infrastructure.Identity;

/// <summary>
/// RFC 6238 TOTP (HMAC-SHA1, 30 second step, 6 digits) over a base32 key as produced by Identity's authenticator key.
/// Unlike Identity's own provider it compares in constant time and reports the matched step so replay can be rejected.
/// </summary>
public static class TotpVerifier
{
    public const int StepSeconds = 30;
    public const int Digits = 6;
    /// <summary>Accepted drift in steps either side of now.</summary>
    public const int Window = 1;

    public static long CurrentStep(DateTimeOffset now) => now.ToUnixTimeSeconds() / StepSeconds;

    public static string Compute(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--) { counter[i] = (byte)(step & 0xFF); step >>= 8; }
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, counter, hash);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString().PadLeft(Digits, '0');
    }

    /// <summary>Returns the matched step, or null. Always evaluates every step in the window.</summary>
    public static long? Verify(string base32Key, string? code, DateTimeOffset now)
    {
        var normalized = Normalize(code);
        var key = DecodeBase32(base32Key);
        if (key == null || normalized == null) return null;
        var given = Encoding.ASCII.GetBytes(normalized);
        var current = CurrentStep(now);
        long? matched = null;
        for (var d = -Window; d <= Window; d++)
        {
            var step = current + d;
            var expected = Encoding.ASCII.GetBytes(Compute(key, step));
            if (CryptographicOperations.FixedTimeEquals(expected, given)) matched = step;
        }
        return matched;
    }

    /// <summary>Six digits, spaces and dashes tolerated; anything else is not a code.</summary>
    public static string? Normalize(string? code)
    {
        if (code == null) return null;
        var digits = new string(code.Where(c => c is not (' ' or '-')).ToArray());
        return digits.Length == Digits && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    public static byte[]? DecodeBase32(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = s.Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(cleaned.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var ch in cleaned)
        {
            var v = alphabet.IndexOf(ch);
            if (v < 0) return null;
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8) { bits -= 8; bytes.Add((byte)((buffer >> bits) & 0xFF)); }
        }
        return bytes.ToArray();
    }
}
