using System.Security.Cryptography;
using System.Text;
using TEC.Core.Common.Guards;
using TEC.Core.Cryptography.Abstractions;

namespace TEC.Core.Cryptography.Asymmetric;

/// <summary>
/// Criptografia assimétrica RSA.
/// Cifra com OAEP-SHA256 e assina com SHA-256 + PSS (ou PKCS#1 v1.5, para compatibilidade).
/// </summary>
/// <remarks>
/// Chaves com menos de 2048 bits são recusadas, inclusive na importação. Na importação, chaves com mais de
/// <see cref="MaximumImportKeySize"/> bits também são recusadas (proteção contra consumo excessivo de CPU).
/// </remarks>
public sealed class RsaCryptography : IAsymmetricCryptography
{
    /// <summary>Tamanho mínimo aceito para chaves (bits).</summary>
    public const int MinimumKeySize = 2048;

    /// <summary>Tamanho máximo aceito para geração de chaves (bits). Evita consumo excessivo de CPU.</summary>
    public const int MaximumKeySize = 8192;

    /// <summary>
    /// Tamanho máximo aceito na importação de chaves (bits). Chaves PEM podem vir de fontes externas: um módulo enorme
    /// tornaria cada operação extremamente lenta (negação de serviço). Chaves maiores são recusadas.
    /// </summary>
    public const int MaximumImportKeySize = 16384;

    // Uma chave privada PKCS#8 de 16384 bits ocupa ~12,6 mil caracteres em PEM; acima disso nem tenta interpretar
    private const int MaxPemLength = 32 * 1024;

    private static readonly RSAEncryptionPadding EncryptionPadding = RSAEncryptionPadding.OaepSHA256;
    private static readonly HashAlgorithmName SignatureHash = HashAlgorithmName.SHA256;
    private readonly RSASignaturePadding _signaturePadding;

    /// <summary>Cria a instância.</summary>
    /// <param name="signatureMode">Esquema de assinatura. Padrão: PSS.</param>
    public RsaCryptography(RsaSignatureMode signatureMode = RsaSignatureMode.Pss)
    {
        _signaturePadding = signatureMode switch
        {
            RsaSignatureMode.Pss => RSASignaturePadding.Pss,
            RsaSignatureMode.Pkcs1 => RSASignaturePadding.Pkcs1,
            _ => throw new ArgumentOutOfRangeException(nameof(signatureMode))
        };
    }

    /// <inheritdoc />
    public RsaKeyPair GenerateKeyPair(int keySizeInBits = 2048)
    {
        if (keySizeInBits is < MinimumKeySize or > MaximumKeySize || keySizeInBits % 8 != 0)
            throw new ArgumentOutOfRangeException(nameof(keySizeInBits),
                $"O tamanho da chave deve ser múltiplo de 8, entre {MinimumKeySize} e {MaximumKeySize} bits.");

        using var rsa = RSA.Create(keySizeInBits);
        return new RsaKeyPair(rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    /// <inheritdoc />
    public byte[] Encrypt(byte[] data, string publicKeyPem)
    {
        Guard.NotNull(data);
        using var rsa = ImportKey(publicKeyPem, requirePrivate: false);

        int maxLength = GetMaxDataLength(rsa.KeySize);
        if (data.Length > maxLength)
            throw new ArgumentException(
                $"Para esta chave o tamanho máximo é {maxLength} bytes. Para dados maiores use HybridCryptography.", nameof(data));

        return rsa.Encrypt(data, EncryptionPadding);
    }

    /// <summary>Tamanho máximo (bytes) cifrável com RSA-OAEP-SHA256 para o tamanho de chave informado.</summary>
    public static int GetMaxDataLength(int keySizeInBits) => keySizeInBits / 8 - 2 * 32 - 2;

    /// <inheritdoc />
    /// <remarks>Qualquer falha gera a mesma exceção genérica, sem indicar a causa (evita ataques de oráculo de padding).</remarks>
    public byte[] Decrypt(byte[] encryptedData, string privateKeyPem)
    {
        Guard.NotNull(encryptedData);
        using var rsa = ImportKey(privateKeyPem, requirePrivate: true);
        try
        {
            return rsa.Decrypt(encryptedData, EncryptionPadding);
        }
        catch (CryptographicException)
        {
            throw new CryptographicException("Falha ao descriptografar os dados.");
        }
    }

    /// <inheritdoc />
    public string Encrypt(string plainText, string publicKeyPem)
    {
        Guard.NotNull(plainText);
        var plain = Encoding.UTF8.GetBytes(plainText);
        try
        {
            return Convert.ToBase64String(Encrypt(plain, publicKeyPem));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <inheritdoc />
    public string Decrypt(string cipherTextBase64, string privateKeyPem)
    {
        Guard.NotNullOrWhiteSpace(cipherTextBase64);
        if (!TryDecodeBase64(cipherTextBase64, out var cipher))
            throw new CryptographicException("Falha ao descriptografar os dados.");

        var plain = Decrypt(cipher, privateKeyPem);
        try
        {
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <inheritdoc />
    public byte[] SignData(byte[] data, string privateKeyPem)
    {
        Guard.NotNull(data);
        using var rsa = ImportKey(privateKeyPem, requirePrivate: true);
        return rsa.SignData(data, SignatureHash, _signaturePadding);
    }

    /// <inheritdoc />
    public string SignData(string data, string privateKeyPem)
    {
        Guard.NotNull(data);
        return Convert.ToBase64String(SignData(Encoding.UTF8.GetBytes(data), privateKeyPem));
    }

    /// <inheritdoc />
    public bool VerifyData(byte[] data, byte[] signature, string publicKeyPem)
    {
        Guard.NotNull(data);
        Guard.NotNull(signature);
        using var rsa = ImportKey(publicKeyPem, requirePrivate: false);

        // Assinatura com tamanho diferente do módulo da chave é inválida
        if (signature.Length != rsa.KeySize / 8)
            return false;

        return rsa.VerifyData(data, signature, SignatureHash, _signaturePadding);
    }

    /// <inheritdoc />
    public bool VerifyData(string data, string signatureBase64, string publicKeyPem)
    {
        Guard.NotNull(data);
        if (string.IsNullOrWhiteSpace(signatureBase64) || !TryDecodeBase64(signatureBase64, out var signature))
            return false;

        return VerifyData(Encoding.UTF8.GetBytes(data), signature, publicKeyPem);
    }

    // Assinaturas e dados cifrados RSA têm no máximo MaximumImportKeySize/8 bytes (2736 caracteres em Base64)
    private const int MaxBase64Length = MaximumImportKeySize / 8 * 4 / 3 + 4;

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        bytes = [];
        if (value.Length > MaxBase64Length)
            return false;

        var buffer = new byte[(value.Length * 3 + 3) / 4];
        if (Convert.TryFromBase64String(value, buffer, out int written))
        {
            bytes = buffer[..written];
            return true;
        }

        return false;
    }

    private static RSA ImportKey(string pem, bool requirePrivate)
    {
        Guard.NotNullOrWhiteSpace(pem);
        if (pem.Length > MaxPemLength)
            throw new ArgumentException($"Chave PEM excede o tamanho máximo aceito ({MaximumImportKeySize} bits).", nameof(pem));

        // Verifica pelo rótulo PEM se é chave privada, sem exportar o conteúdo da chave para a memória
        if (requirePrivate && (!PemEncoding.TryFind(pem, out var fields) || !pem[fields.Label].Contains("PRIVATE", StringComparison.Ordinal)))
            throw new ArgumentException("A operação exige a chave privada (PEM \"PRIVATE KEY\").", nameof(pem));

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            rsa.Dispose();
            throw new ArgumentException("Chave PEM inválida.", nameof(pem));
        }

        if (rsa.KeySize < MinimumKeySize)
        {
            rsa.Dispose();
            throw new ArgumentException($"Chaves RSA com menos de {MinimumKeySize} bits não são aceitas.", nameof(pem));
        }

        if (rsa.KeySize > MaximumImportKeySize)
        {
            rsa.Dispose();
            throw new ArgumentException($"Chaves RSA com mais de {MaximumImportKeySize} bits não são aceitas.", nameof(pem));
        }

        return rsa;
    }
}
