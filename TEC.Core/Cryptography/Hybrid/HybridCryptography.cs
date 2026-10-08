using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TEC.Core.Common.Guards;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Symmetric;

namespace TEC.Core.Cryptography.Hybrid;

/// <summary>
/// Criptografia híbrida RSA + AES-GCM, sem limite de tamanho dos dados.
/// </summary>
/// <remarks>
/// <para>Formato: [versão 1 byte = 0x01][tamanho da chave cifrada 2 bytes][chave AES cifrada com RSA][dados cifrados com AES-GCM].
/// O cabeçalho inteiro (versão, tamanho e chave cifrada) é usado como dado associado do AES-GCM, vinculando as partes:
/// qualquer alteração é detectada. Versões desconhecidas geram <see cref="CryptographicException"/> com mensagem explícita.</para>
/// <para>Garante sigilo, mas não autoria: qualquer pessoa com a chave pública consegue cifrar.
/// Para garantir a origem, assine o conteúdo com <see cref="IAsymmetricCryptography.SignData(byte[], string)"/>.</para>
/// </remarks>
public sealed class HybridCryptography : IHybridCryptography
{
    /// <summary>Versão do formato (primeiro byte dos dados cifrados).</summary>
    public const byte FormatVersion = 1;

    private const int VersionSize = 1;
    private const int LengthPrefixSize = sizeof(ushort);
    private const int HeaderPrefixSize = VersionSize + LengthPrefixSize;
    private readonly ISymmetricCryptography _symmetric;
    private readonly IAsymmetricCryptography _asymmetric;

    /// <summary>Cria a instância com as implementações padrão (AES-GCM e RSA).</summary>
    public HybridCryptography() : this(new AesGcmCryptography(), new RsaCryptography())
    {
    }

    /// <summary>Cria a instância com implementações customizadas.</summary>
    public HybridCryptography(ISymmetricCryptography symmetric, IAsymmetricCryptography asymmetric)
    {
        _symmetric = Guard.NotNull(symmetric);
        _asymmetric = Guard.NotNull(asymmetric);
    }

    /// <inheritdoc />
    public byte[] Encrypt(byte[] data, string publicKeyPem)
    {
        Guard.NotNull(data);
        var aesKey = _symmetric.GenerateKey();
        try
        {
            var encryptedKey = _asymmetric.Encrypt(aesKey, publicKeyPem);

            var header = new byte[HeaderPrefixSize + encryptedKey.Length];
            header[0] = FormatVersion;
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(VersionSize), checked((ushort)encryptedKey.Length));
            encryptedKey.CopyTo(header, HeaderPrefixSize);

            var encryptedData = _symmetric.Encrypt(data, aesKey, associatedData: header);
            return [.. header, .. encryptedData];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Qualquer falha (formato, chave RSA ou integridade) gera a mesma exceção genérica; a única exceção é a versão
    /// de formato desconhecida, informada explicitamente (o byte de versão não é secreto).
    /// </remarks>
    public byte[] Decrypt(byte[] encryptedData, string privateKeyPem)
    {
        Guard.NotNull(encryptedData);
        if (encryptedData.Length < HeaderPrefixSize)
            throw DecryptionFailed();
        if (encryptedData[0] != FormatVersion)
            throw new CryptographicException(
                $"Versão de formato dos dados criptografados não suportada: {encryptedData[0]} (esperado {FormatVersion}). " +
                "Formato não suportado: byte de versão ausente ou desconhecido.");

        int keyLength = BinaryPrimitives.ReadUInt16BigEndian(encryptedData.AsSpan(VersionSize));
        int headerLength = HeaderPrefixSize + keyLength;
        if (keyLength == 0 || encryptedData.Length < headerLength)
            throw DecryptionFailed();

        var header = encryptedData[..headerLength];
        var encryptedKey = encryptedData[HeaderPrefixSize..headerLength];
        byte[]? aesKey = null;
        try
        {
            aesKey = _asymmetric.Decrypt(encryptedKey, privateKeyPem);
            return _symmetric.Decrypt(encryptedData[headerLength..], aesKey, associatedData: header);
        }
        catch (Exception ex) when (ex is CryptographicException || (ex is ArgumentException && aesKey is not null))
        {
            throw DecryptionFailed();
        }
        finally
        {
            if (aesKey is not null)
                CryptographicOperations.ZeroMemory(aesKey);
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
        byte[] cipher;
        try
        {
            cipher = Convert.FromBase64String(cipherTextBase64);
        }
        catch (FormatException)
        {
            throw DecryptionFailed();
        }

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

    private static CryptographicException DecryptionFailed() => new("Falha ao descriptografar os dados.");
}
