#if NET9_0_OR_GREATER
using System.Buffers;
using System.Buffers.Text;
#endif

namespace TEC.Core.Polyfills;

/// <summary>
/// Codificações usadas pela biblioteca com o mesmo resultado em <c>net8.0</c> e <c>net10.0</c>.
/// No .NET 9+ delega para as APIs nativas (<c>Convert.ToHexStringLower</c>, <c>Convert.FromHexString(…, Span)</c>
/// e <c>Base64Url</c>); no .NET 8 usa implementações equivalentes. Os testes conferem os dois caminhos.
/// </summary>
internal static class EncodingCompat
{
    /// <summary>Bytes em hexadecimal minúsculo (equivalente a <c>Convert.ToHexStringLower</c>).</summary>
    public static string ToHexLower(byte[] bytes)
    {
#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(bytes);
#else
        return string.Create(bytes.Length * 2, bytes, static (chars, source) =>
        {
            const string digits = "0123456789abcdef";
            for (int i = 0; i < source.Length; i++)
            {
                chars[2 * i] = digits[source[i] >> 4];
                chars[2 * i + 1] = digits[source[i] & 0xF];
            }
        });
#endif
    }

    /// <summary>
    /// Decodifica hexadecimal (maiúsculas ou minúsculas) sem lançar exceção.
    /// Retorna <c>false</c> se o tamanho for ímpar ou houver caractere fora de 0-9/a-f/A-F.
    /// </summary>
    public static bool TryFromHex(string hex, out byte[] bytes)
    {
        bytes = [];
        if (hex.Length % 2 != 0)
            return false;

        var buffer = new byte[hex.Length / 2];
#if NET9_0_OR_GREATER
        if (Convert.FromHexString(hex, buffer, out _, out _) != OperationStatus.Done)
            return false;
#else
        for (int i = 0; i < buffer.Length; i++)
        {
            int high = HexValue(hex[2 * i]);
            int low = HexValue(hex[2 * i + 1]);
            if ((high | low) < 0)
                return false;
            buffer[i] = (byte)((high << 4) | low);
        }
#endif
        bytes = buffer;
        return true;
    }

    /// <summary>Base64 seguro para URL, sem preenchimento (RFC 4648 §5; equivalente a <c>Base64Url.EncodeToString</c>).</summary>
    public static string ToBase64Url(byte[] bytes)
    {
#if NET9_0_OR_GREATER
        return Base64Url.EncodeToString(bytes);
#else
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
#endif
    }

#if !NET9_0_OR_GREATER
    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };
#endif
}
