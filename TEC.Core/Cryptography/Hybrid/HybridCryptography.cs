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
    public byte[] Encrypt(byte[] data, RsaKey publicKey)
    {
        Guard.NotNull(data);
        Guard.NotNull(publicKey);
        var aesKey = _symmetric.GenerateKey();
        try
        {
            var encryptedKey = _asymmetric is RsaCryptography rsa
                ? rsa.Encrypt(aesKey, publicKey)
                : _asymmetric.Encrypt(aesKey, publicKey.Rsa.ExportSubjectPublicKeyInfoPem());

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
    public byte[] Encrypt(byte[] data, string publicKeyPem)
    {
        Guard.NotNull(data);
        using var key = RsaKey.FromPublicKeyPem(publicKeyPem);
        return Encrypt(data, key);
    }

    /// <inheritdoc />
    public byte[] Decrypt(byte[] encryptedData, RsaKey privateKey)
    {
        Guard.NotNull(encryptedData);
        Guard.NotNull(privateKey);
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
            aesKey = _asymmetric is RsaCryptography rsa
                ? rsa.Decrypt(encryptedKey, privateKey)
                : _asymmetric.Decrypt(encryptedKey, privateKey.Rsa.ExportPkcs8PrivateKeyPem());

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
    /// <remarks>
    /// Qualquer falha (formato, chave RSA ou integridade) gera a mesma exceção genérica; a única exceção é a versão
    /// de formato desconhecida, informada explicitamente (o byte de versão não é secreto).
    /// </remarks>
    public byte[] Decrypt(byte[] encryptedData, string privateKeyPem)
    {
        Guard.NotNull(encryptedData);
        using var key = RsaKey.FromPrivateKeyPem(privateKeyPem);
        return Decrypt(encryptedData, key);
    }

    /// <inheritdoc />
    public string Encrypt(string plainText, RsaKey publicKey)
    {
        Guard.NotNull(plainText);
        var plain = Encoding.UTF8.GetBytes(plainText);
        try
        {
            return Convert.ToBase64String(Encrypt(plain, publicKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <inheritdoc />
    public string Encrypt(string plainText, string publicKeyPem)
    {
        Guard.NotNull(plainText);
        using var key = RsaKey.FromPublicKeyPem(publicKeyPem);
        return Encrypt(plainText, key);
    }

    /// <inheritdoc />
    public string Decrypt(string cipherTextBase64, RsaKey privateKey)
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

        var plain = Decrypt(cipher, privateKey);
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
    public string Decrypt(string cipherTextBase64, string privateKeyPem)
    {
        Guard.NotNullOrWhiteSpace(cipherTextBase64);
        using var key = RsaKey.FromPrivateKeyPem(privateKeyPem);
        return Decrypt(cipherTextBase64, key);
    }

    /// <inheritdoc />
    public async Task EncryptToFileAsync(byte[] data, string outputFilePath, RsaKey publicKey, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(data);
        Guard.NotNullOrWhiteSpace(outputFilePath);
        Guard.NotNull(publicKey);

        var fullOutput = Path.GetFullPath(outputFilePath);
        var directory = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = $"{fullOutput}.{Guid.NewGuid():N}.tmp";
        try
        {
            var encrypted = Encrypt(data, publicKey);
            await File.WriteAllBytesAsync(tempPath, encrypted, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, fullOutput, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task EncryptToFileAsync(byte[] data, string outputFilePath, string publicKeyPem, CancellationToken cancellationToken = default)
    {
        using var key = RsaKey.FromPublicKeyPem(publicKeyPem);
        await EncryptToFileAsync(data, outputFilePath, key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<byte[]> DecryptFromFileAsync(string inputFilePath, RsaKey privateKey, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(inputFilePath);
        Guard.NotNull(privateKey);

        var fullInput = Path.GetFullPath(inputFilePath);
        var fileBytes = await File.ReadAllBytesAsync(fullInput, cancellationToken).ConfigureAwait(false);
        return Decrypt(fileBytes, privateKey);
    }

    /// <inheritdoc />
    public async Task<byte[]> DecryptFromFileAsync(string inputFilePath, string privateKeyPem, CancellationToken cancellationToken = default)
    {
        using var key = RsaKey.FromPrivateKeyPem(privateKeyPem);
        return await DecryptFromFileAsync(inputFilePath, key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EncryptFileAsync(string inputFilePath, string outputFilePath, RsaKey publicKey, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(inputFilePath);
        Guard.NotNullOrWhiteSpace(outputFilePath);
        Guard.NotNull(publicKey);

        var fullInput = Path.GetFullPath(inputFilePath);
        var fullOutput = Path.GetFullPath(outputFilePath);
        if (string.Equals(fullInput, fullOutput, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Os caminhos de arquivo de entrada e saída devem ser diferentes.", nameof(outputFilePath));

        var directory = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = $"{fullOutput}.{Guid.NewGuid():N}.tmp";
        var aesKey = _symmetric.GenerateKey();
        try
        {
            var encryptedKey = _asymmetric is RsaCryptography rsa
                ? rsa.Encrypt(aesKey, publicKey)
                : _asymmetric.Encrypt(aesKey, publicKey.Rsa.ExportSubjectPublicKeyInfoPem());

            var header = new byte[HeaderPrefixSize + encryptedKey.Length];
            header[0] = FormatVersion;
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(VersionSize), checked((ushort)encryptedKey.Length));
            encryptedKey.CopyTo(header, HeaderPrefixSize);

            var inputStream = new FileStream(fullInput, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (inputStream.ConfigureAwait(false))
            {
                var outputStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, FileOptions.Asynchronous);
                await using (outputStream.ConfigureAwait(false))
                {
                    await outputStream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                    await _symmetric.EncryptAsync(inputStream, outputStream, aesKey, cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(tempPath, fullOutput, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
        }
    }

    /// <inheritdoc />
    public async Task EncryptFileAsync(string inputFilePath, string outputFilePath, string publicKeyPem, CancellationToken cancellationToken = default)
    {
        using var key = RsaKey.FromPublicKeyPem(publicKeyPem);
        await EncryptFileAsync(inputFilePath, outputFilePath, key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DecryptFileAsync(string inputFilePath, string outputFilePath, RsaKey privateKey, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(inputFilePath);
        Guard.NotNullOrWhiteSpace(outputFilePath);
        Guard.NotNull(privateKey);

        var fullInput = Path.GetFullPath(inputFilePath);
        var fullOutput = Path.GetFullPath(outputFilePath);
        if (string.Equals(fullInput, fullOutput, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Os caminhos de arquivo de entrada e saída devem ser diferentes.", nameof(outputFilePath));

        var directory = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = $"{fullOutput}.{Guid.NewGuid():N}.tmp";
        byte[]? aesKey = null;
        try
        {
            var inputStream = new FileStream(fullInput, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (inputStream.ConfigureAwait(false))
            {
                var prefix = new byte[HeaderPrefixSize];
                int prefixRead = await inputStream.ReadAtLeastAsync(prefix, HeaderPrefixSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (prefixRead < HeaderPrefixSize)
                    throw DecryptionFailed();
                if (prefix[0] != FormatVersion)
                    throw new CryptographicException($"Versão de formato dos dados criptografados não suportada: {prefix[0]} (esperado {FormatVersion}).");

                int keyLength = BinaryPrimitives.ReadUInt16BigEndian(prefix.AsSpan(VersionSize));
                if (keyLength == 0)
                    throw DecryptionFailed();

                var encryptedKey = new byte[keyLength];
                int keyRead = await inputStream.ReadAtLeastAsync(encryptedKey, keyLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (keyRead < keyLength)
                    throw DecryptionFailed();

                try
                {
                    aesKey = _asymmetric is RsaCryptography rsa
                        ? rsa.Decrypt(encryptedKey, privateKey)
                        : _asymmetric.Decrypt(encryptedKey, privateKey.Rsa.ExportPkcs8PrivateKeyPem());
                }
                catch (Exception ex) when (ex is CryptographicException || ex is ArgumentException)
                {
                    throw DecryptionFailed();
                }

                var outputStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, FileOptions.Asynchronous);
                await using (outputStream.ConfigureAwait(false))
                {
                    await _symmetric.DecryptAsync(inputStream, outputStream, aesKey, cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(tempPath, fullOutput, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
        finally
        {
            if (aesKey is not null)
                CryptographicOperations.ZeroMemory(aesKey);
        }
    }

    /// <inheritdoc />
    public async Task DecryptFileAsync(string inputFilePath, string outputFilePath, string privateKeyPem, CancellationToken cancellationToken = default)
    {
        using var key = RsaKey.FromPrivateKeyPem(privateKeyPem);
        await DecryptFileAsync(inputFilePath, outputFilePath, key, cancellationToken).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Melhor esforço: a falha original é a que importa para quem chamou
        }
    }

    private static CryptographicException DecryptionFailed() => new("Falha ao descriptografar os dados.");
}
