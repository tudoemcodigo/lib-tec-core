using System.Security.Cryptography;
using System.Text;
using TEC.Core.Common.Guards;
using TEC.Core.Polyfills;

namespace TEC.Core.Cryptography.Hashing;

/// <summary>
/// Funções de hash (SHA) e HMAC. Resultados em hexadecimal minúsculo.
/// </summary>
/// <remarks>Para senhas use <see cref="Pbkdf2PasswordHasher"/>, nunca SHA puro.</remarks>
public static class HashHelper
{
    /// <summary>Calcula o hash dos bytes.</summary>
    public static byte[] ComputeHash(byte[] data, HashAlgorithmType algorithm = HashAlgorithmType.Sha256)
    {
        Guard.NotNull(data);
        return algorithm switch
        {
            HashAlgorithmType.Sha256 => SHA256.HashData(data),
            HashAlgorithmType.Sha384 => SHA384.HashData(data),
            HashAlgorithmType.Sha512 => SHA512.HashData(data),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
    }

    /// <summary>Calcula o hash de um texto (UTF-8) e retorna em hexadecimal.</summary>
    public static string ComputeHash(string text, HashAlgorithmType algorithm = HashAlgorithmType.Sha256)
    {
        Guard.NotNull(text);
        return EncodingCompat.ToHexLower(ComputeHash(Encoding.UTF8.GetBytes(text), algorithm));
    }

    /// <summary>Calcula o hash de um stream (ex.: arquivo) sem carregá-lo inteiro em memória.</summary>
    public static async Task<string> ComputeHashAsync(Stream stream, HashAlgorithmType algorithm = HashAlgorithmType.Sha256, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("O stream não permite leitura.", nameof(stream));

        var hash = algorithm switch
        {
            HashAlgorithmType.Sha256 => await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false),
            HashAlgorithmType.Sha384 => await SHA384.HashDataAsync(stream, cancellationToken).ConfigureAwait(false),
            HashAlgorithmType.Sha512 => await SHA512.HashDataAsync(stream, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
        return EncodingCompat.ToHexLower(hash);
    }

    /// <summary>Calcula o hash de um arquivo.</summary>
    public static async Task<string> ComputeFileHashAsync(string filePath, HashAlgorithmType algorithm = HashAlgorithmType.Sha256, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(filePath);
        var stream = File.OpenRead(filePath);
        await using (stream.ConfigureAwait(false))
            return await ComputeHashAsync(stream, algorithm, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Calcula o HMAC dos bytes com a chave informada.</summary>
    /// <remarks>A chave não pode ser vazia; recomenda-se ao menos 32 bytes aleatórios.</remarks>
    public static byte[] ComputeHmac(byte[] data, byte[] key, HashAlgorithmType algorithm = HashAlgorithmType.Sha256)
    {
        Guard.NotNull(data);
        Guard.NotNull(key);
        if (key.Length == 0)
            throw new ArgumentException("A chave do HMAC não pode ser vazia.", nameof(key));

        return algorithm switch
        {
            HashAlgorithmType.Sha256 => HMACSHA256.HashData(key, data),
            HashAlgorithmType.Sha384 => HMACSHA384.HashData(key, data),
            HashAlgorithmType.Sha512 => HMACSHA512.HashData(key, data),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
    }

    /// <summary>Calcula o HMAC de um texto (UTF-8) e retorna em hexadecimal. Útil para assinar webhooks e payloads.</summary>
    public static string ComputeHmac(string text, string key, HashAlgorithmType algorithm = HashAlgorithmType.Sha256)
    {
        Guard.NotNull(text);
        Guard.NotNull(key);
        if (key.Length == 0)
            throw new ArgumentException("A chave do HMAC não pode ser vazia.", nameof(key));

        var keyBytes = Encoding.UTF8.GetBytes(key);
        try
        {
            return EncodingCompat.ToHexLower(ComputeHmac(Encoding.UTF8.GetBytes(text), keyBytes, algorithm));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    /// <summary>
    /// Compara dois textos (hashes, tokens, assinaturas em Base64) em tempo constante, evitando ataques de temporização.
    /// A comparação é ordinal: diferencia maiúsculas de minúsculas (essencial para Base64 e tokens).
    /// Para hashes hexadecimais que podem vir em maiúsculas, use <see cref="FixedTimeEqualsHex"/>.
    /// </summary>
    /// <remarks>Retorna <c>false</c> se algum valor for <c>null</c>. O tamanho dos textos não é protegido.</remarks>
    public static bool FixedTimeEquals(string? hashA, string? hashB)
    {
        if (hashA is null || hashB is null)
            return false;

        return CryptographicOperations.FixedTimeEquals(Utf16Bytes(hashA), Utf16Bytes(hashB));
    }

    /// <summary>
    /// Compara dois hashes em hexadecimal em tempo constante, sem diferenciar maiúsculas de minúsculas
    /// (ex.: assinatura de webhook enviada em maiúsculas comparada com <see cref="ComputeHmac(string, string, HashAlgorithmType)"/>).
    /// </summary>
    /// <remarks>
    /// Os textos são decodificados para bytes antes da comparação: retorna <c>false</c> se algum for <c>null</c>
    /// ou não for hexadecimal válido (tamanho ímpar ou caracteres fora de 0-9/a-f/A-F).
    /// </remarks>
    public static bool FixedTimeEqualsHex(string? hexA, string? hexB)
    {
        if (!TryDecodeHex(hexA, out var a) || !TryDecodeHex(hexB, out var b))
            return false;

        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    // Bytes UTF-16 do texto, sem alocação (comparação ordinal exata)
    private static ReadOnlySpan<byte> Utf16Bytes(string value) =>
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(value.AsSpan());

    private static bool TryDecodeHex(string? hex, out byte[] bytes)
    {
        bytes = [];
        return hex is not null && EncodingCompat.TryFromHex(hex, out bytes);
    }
}
