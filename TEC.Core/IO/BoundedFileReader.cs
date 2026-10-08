using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace TEC.Core.IO;

/// <summary>
/// Leitura segura de arquivos de texto pequenos (credenciais, segredos montados em volume, configuração): tamanho
/// limitado, UTF-8 estrito e buffers zerados depois do uso.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Nunca guarda em memória mais que <c>maxBytes + 1</c> bytes, mesmo quando o arquivo não informa tamanho
/// (pipe, arquivo especial que informa 0) ou cresce durante a leitura.</item>
/// <item>O BOM UTF-8 inicial (arquivo salvo no Windows) é descartado.</item>
/// <item>Conteúdo fora de UTF-8 lança <see cref="InvalidDataException"/> com mensagem sem nenhum trecho do conteúdo.</item>
/// <item>Os buffers intermediários são zerados: o conteúdo pode ser um segredo.</item>
/// </list>
/// Thread-safe: sem estado compartilhado.
/// </remarks>
public static class BoundedFileReader
{
    private const int InitialBufferBytes = 4 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Lê o arquivo inteiro como UTF-8 estrito, se ele couber no limite.</summary>
    /// <param name="path">Caminho do arquivo.</param>
    /// <param name="maxBytes">Tamanho máximo aceito, em bytes (maior que zero).</param>
    /// <param name="text">Conteúdo sem BOM; <c>null</c> quando o retorno é <c>false</c>.</param>
    /// <returns><c>false</c> se o arquivo passar de <paramref name="maxBytes"/> (nada além do limite é lido).</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> vazio.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> menor ou igual a zero.</exception>
    /// <exception cref="InvalidDataException">O conteúdo não é UTF-8 válido.</exception>
    /// <exception cref="IOException">Falha de leitura (arquivo inexistente, travado...).</exception>
    /// <exception cref="UnauthorizedAccessException">Sem permissão de leitura.</exception>
    public static bool TryReadUtf8(string path, int maxBytes, [NotNullWhen(true)] out string? text)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        text = null;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long reported = stream.CanSeek ? stream.Length : 0;
        if (reported > maxBytes)
            return false;

        // Um byte além do limite basta para saber que o arquivo passou dele
        int limit = maxBytes + 1;
        var buffer = new byte[reported > 0 ? (int)Math.Min(reported + 1, limit) : Math.Min(InitialBufferBytes, limit)];
        int total = 0;
        try
        {
            while (true)
            {
                if (total == buffer.Length)
                {
                    if (buffer.Length == limit)
                        break;
                    buffer = Grow(buffer, (int)Math.Min((long)buffer.Length * 2, limit));
                }

                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    break;
                total += read;
            }

            if (total > maxBytes)
                return false;

            var content = buffer.AsSpan(0, total);
            if (content.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
                content = content[3..];

            try
            {
                text = StrictUtf8.GetString(content);
                return true;
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException($"O arquivo '{Path.GetFileName(path)}' não contém texto UTF-8 válido.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static byte[] Grow(byte[] current, int newSize)
    {
        var bigger = new byte[newSize];
        current.CopyTo(bigger, 0);
        CryptographicOperations.ZeroMemory(current);
        return bigger;
    }
}
