#if NET9_0_OR_GREATER
using System.Buffers.Text;
#endif

namespace TEC.Core.Text.Codecs;

/// <summary>
/// Base64 seguro para URL, sem preenchimento (RFC 4648 §5): alfabeto <c>A-Z a-z 0-9 - _</c>, sem <c>=</c>.
/// Mesmo resultado em <c>net8.0</c> e <c>net10.0</c> (no .NET 9+ delega para <c>System.Buffers.Text.Base64Url</c>).
/// </summary>
/// <remarks>
/// Use para tokens, chaves de API, identificadores e hashes que trafegam em URL, cabeçalho ou nome de arquivo.
/// A decodificação é estrita: rejeita preenchimento, espaços e caracteres fora do alfabeto, sem lançar exceção.
/// </remarks>
public static class Base64UrlEncoder
{
    /// <summary>Codifica bytes em Base64Url sem preenchimento.</summary>
    /// <param name="bytes">Bytes a codificar.</param>
    /// <returns>Texto Base64Url (vazio para entrada vazia).</returns>
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
#if NET9_0_OR_GREATER
        return Base64Url.EncodeToString(bytes);
#else
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
#endif
    }

    /// <summary>
    /// Indica se o texto é Base64Url válido e canônico, sem preenchimento: só caracteres do alfabeto, tamanho
    /// decodificável e bits finais zerados.
    /// </summary>
    /// <param name="value">Texto a conferir.</param>
    /// <returns><c>true</c> se, e somente se, <see cref="TryDecode"/> decodifica <paramref name="value"/>.</returns>
    public static bool IsValid(ReadOnlySpan<char> value)
    {
        // Resto 1 nunca ocorre numa codificação válida (6 bits não formam um byte)
        int remainder = value.Length % 4;
        if (remainder == 1)
            return false;

        foreach (char c in value)
        {
            if (SextetOf(c) < 0)
                return false;
        }

        // Forma canônica: o último caractere só carrega bits de dados (4 bits sobram com resto 2; 2 bits com resto 3)
        if (remainder != 0)
        {
            int unusedBitsMask = remainder == 2 ? 0b1111 : 0b11;
            if ((SextetOf(value[^1]) & unusedBitsMask) != 0)
                return false;
        }

        return true;
    }

    private static int SextetOf(char c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a' + 26,
        >= '0' and <= '9' => c - '0' + 52,
        '-' => 62,
        '_' => 63,
        _ => -1,
    };

    /// <summary>Decodifica Base64Url sem preenchimento, sem lançar exceção.</summary>
    /// <param name="value">Texto Base64Url.</param>
    /// <param name="bytes">Bytes decodificados; vazio quando o retorno é <c>false</c>.</param>
    /// <returns><c>false</c> se <paramref name="value"/> for nulo ou não for Base64Url válido.</returns>
    public static bool TryDecode(string? value, out byte[] bytes)
    {
        bytes = [];
        if (value is null || !IsValid(value))
            return false;

        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };

        var buffer = new byte[padded.Length / 4 * 3];
        if (!Convert.TryFromBase64String(padded, buffer, out int written))
            return false;

        bytes = written == buffer.Length ? buffer : buffer.AsSpan(0, written).ToArray();
        return true;
    }
}
