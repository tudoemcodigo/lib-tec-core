using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TEC.Core.Common.Guards;
using TEC.Core.Cryptography.Abstractions;

namespace TEC.Core.Cryptography.Symmetric;

/// <summary>
/// Criptografia simétrica com AES-GCM (cifra autenticada: garante sigilo e integridade).
/// </summary>
/// <remarks>
/// <para>Formato em memória: [versão 1 byte = 0x01][nonce 12 bytes][tag 16 bytes][dados cifrados].
/// O nonce é aleatório; por recomendação do NIST, não cifre mais que 2³² mensagens com a mesma chave (faça rotação).
/// O byte de versão permite evoluir o formato no futuro; versões desconhecidas geram <see cref="CryptographicException"/>
/// com mensagem explícita. O byte de versão é autenticado (entra no dado associado do AES-GCM, antes do
/// <c>associatedData</c> do chamador): trocá-lo invalida os dados.</para>
/// <para>Formato em stream: [versão 1 byte = 0x01][salt 32 bytes] seguido de blocos
/// [flag de último bloco 1 byte][tamanho 4 bytes][tag 16 bytes][dados cifrados].
/// Cada stream usa uma subchave própria derivada via HKDF (chave + salt aleatório), eliminando o risco de
/// reutilização de nonce entre arquivos. Cada bloco é autenticado, o que impede reordenação, truncamento
/// e dados extras anexados ao final.</para>
/// </remarks>
public sealed class AesGcmCryptography : ISymmetricCryptography
{
    /// <summary>Versão do formato em memória (primeiro byte de <see cref="Encrypt(byte[], byte[], byte[])"/>).</summary>
    public const byte FormatVersion = 1;

    private const int VersionSize = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int DefaultKeySize = 32;
    private const int StreamChunkSize = 64 * 1024;
    private const int StreamSaltSize = 32;
    private const byte StreamVersion = 1;
    private const int StreamHeaderSize = 1 + StreamSaltSize;
    private const int ChunkHeaderSize = 1 + sizeof(int) + TagSize;
    private static readonly byte[] StreamKeyInfo = "TEC.Core.AesGcm.Stream.v1"u8.ToArray();

    /// <inheritdoc />
    public byte[] GenerateKey() => RandomNumberGenerator.GetBytes(DefaultKeySize);

    /// <inheritdoc />
    public string GenerateKeyBase64()
    {
        var key = GenerateKey();
        try
        {
            return Convert.ToBase64String(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc />
    public byte[] Encrypt(byte[] plainData, byte[] key, byte[]? associatedData = null)
    {
        Guard.NotNull(plainData);
        ValidateKey(key);

        var result = new byte[VersionSize + NonceSize + TagSize + plainData.Length];
        result[0] = FormatVersion;
        var nonce = result.AsSpan(VersionSize, NonceSize);
        var tag = result.AsSpan(VersionSize + NonceSize, TagSize);
        var cipher = result.AsSpan(VersionSize + NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainData, cipher, tag, VersionedAssociatedData(associatedData));
        return result;
    }

    /// <inheritdoc />
    /// <exception cref="CryptographicException">
    /// Dados corrompidos, chave incorreta, dados associados diferentes ou versão de formato não suportada.
    /// </exception>
    public byte[] Decrypt(byte[] encryptedData, byte[] key, byte[]? associatedData = null)
    {
        Guard.NotNull(encryptedData);
        ValidateKey(key);
        if (encryptedData.Length < VersionSize + NonceSize + TagSize)
            throw InvalidData();
        if (encryptedData[0] != FormatVersion)
            throw UnsupportedVersion(encryptedData[0]);

        var nonce = encryptedData.AsSpan(VersionSize, NonceSize);
        var tag = encryptedData.AsSpan(VersionSize + NonceSize, TagSize);
        var cipher = encryptedData.AsSpan(VersionSize + NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, VersionedAssociatedData(associatedData));
        return plain;
    }

    // Dado associado efetivo: [versão do formato][associatedData do chamador]
    private static byte[] VersionedAssociatedData(byte[]? associatedData)
    {
        var result = new byte[VersionSize + (associatedData?.Length ?? 0)];
        result[0] = FormatVersion;
        associatedData?.CopyTo(result, VersionSize);
        return result;
    }

    /// <inheritdoc />
    public string Encrypt(string plainText, string base64Key)
    {
        Guard.NotNull(plainText);
        var key = DecodeKey(base64Key);
        var plain = Encoding.UTF8.GetBytes(plainText);
        try
        {
            return Convert.ToBase64String(Encrypt(plain, key));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <inheritdoc />
    public string Decrypt(string cipherTextBase64, string base64Key)
    {
        Guard.NotNullOrWhiteSpace(cipherTextBase64);
        var key = DecodeKey(base64Key);
        byte[]? plain = null;
        try
        {
            plain = Decrypt(DecodeBase64(cipherTextBase64), key);
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (plain is not null)
                CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <inheritdoc />
    public async Task EncryptAsync(Stream input, Stream output, byte[] key, CancellationToken cancellationToken = default)
    {
        ValidateStreams(input, output);
        ValidateKey(key);

        var header = new byte[StreamHeaderSize];
        header[0] = StreamVersion;
        RandomNumberGenerator.Fill(header.AsSpan(1));
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);

        var streamKey = DeriveStreamKey(key, header);
        var current = new byte[StreamChunkSize];
        var next = new byte[StreamChunkSize];
        try
        {
            using var aes = new AesGcm(streamKey, TagSize);
            var cipher = new byte[StreamChunkSize];
            var chunkHeader = new byte[ChunkHeaderSize];
            var nonce = new byte[NonceSize];
            var aad = BuildAssociatedData(header);

            int currentLength = await input.ReadAtLeastAsync(current, StreamChunkSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            ulong counter = 0;

            while (true)
            {
                // Lê o próximo bloco antecipadamente para saber se o atual é o último
                int nextLength = currentLength == StreamChunkSize
                    ? await input.ReadAtLeastAsync(next, StreamChunkSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false)
                    : 0;
                bool isFinal = nextLength == 0;

                BuildChunkNonce(counter, nonce);
                aad[^1] = isFinal ? (byte)1 : (byte)0;

                chunkHeader[0] = aad[^1];
                BinaryPrimitives.WriteInt32BigEndian(chunkHeader.AsSpan(1, sizeof(int)), currentLength);
                aes.Encrypt(nonce, current.AsSpan(0, currentLength), cipher.AsSpan(0, currentLength),
                    chunkHeader.AsSpan(1 + sizeof(int), TagSize), aad);

                await output.WriteAsync(chunkHeader, cancellationToken).ConfigureAwait(false);
                await output.WriteAsync(cipher.AsMemory(0, currentLength), cancellationToken).ConfigureAwait(false);

                if (isFinal)
                    break;

                (current, next) = (next, current);
                currentLength = nextLength;
                counter = checked(counter + 1);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(streamKey);
            CryptographicOperations.ZeroMemory(current);
            CryptographicOperations.ZeroMemory(next);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Cada bloco é verificado antes de ser escrito, mas truncamentos só são detectados ao final.
    /// Se este método lançar exceção, descarte todo o conteúdo já escrito em <paramref name="output"/>.
    /// </remarks>
    public async Task DecryptAsync(Stream input, Stream output, byte[] key, CancellationToken cancellationToken = default)
    {
        ValidateStreams(input, output);
        ValidateKey(key);

        var header = new byte[StreamHeaderSize];
        int headerRead = await input.ReadAtLeastAsync(header, StreamHeaderSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (headerRead < StreamHeaderSize)
            throw InvalidData();
        if (header[0] != StreamVersion)
            throw UnsupportedVersion(header[0]);

        var streamKey = DeriveStreamKey(key, header);
        var plain = new byte[StreamChunkSize];
        try
        {
            using var aes = new AesGcm(streamKey, TagSize);
            var cipher = new byte[StreamChunkSize];
            var chunkHeader = new byte[ChunkHeaderSize];
            var nonce = new byte[NonceSize];
            var aad = BuildAssociatedData(header);
            ulong counter = 0;

            while (true)
            {
                int read = await input.ReadAtLeastAsync(chunkHeader, ChunkHeaderSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (read < ChunkHeaderSize)
                    throw InvalidData();

                byte finalFlag = chunkHeader[0];
                int length = BinaryPrimitives.ReadInt32BigEndian(chunkHeader.AsSpan(1, sizeof(int)));
                if (finalFlag > 1 || length < 0 || length > StreamChunkSize || (finalFlag == 0 && length != StreamChunkSize))
                    throw InvalidData();

                if (await input.ReadAtLeastAsync(cipher.AsMemory(0, length), length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false) < length)
                    throw InvalidData();

                BuildChunkNonce(counter, nonce);
                aad[^1] = finalFlag;
                aes.Decrypt(nonce, cipher.AsSpan(0, length), chunkHeader.AsSpan(1 + sizeof(int), TagSize),
                    plain.AsSpan(0, length), aad);

                await output.WriteAsync(plain.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

                if (finalFlag == 1)
                    break;

                counter = checked(counter + 1);
            }

            // Nenhum dado pode existir após o bloco final
            if (await input.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) != 0)
                throw InvalidData();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(streamKey);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    // Subchave exclusiva por stream: HKDF-SHA256(chave, salt do cabeçalho)
    private static byte[] DeriveStreamKey(byte[] key, byte[] header) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, key, DefaultKeySize, header.AsSpan(1, StreamSaltSize).ToArray(), StreamKeyInfo);

    // Nonce de cada bloco = 4 bytes zerados + contador do bloco (8 bytes); seguro porque a subchave é única por stream
    private static void BuildChunkNonce(ulong counter, byte[] nonce)
    {
        nonce.AsSpan(0, 4).Clear();
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), counter);
    }

    // Dados autenticados de cada bloco: cabeçalho do stream + flag de último bloco
    private static byte[] BuildAssociatedData(byte[] header)
    {
        var aad = new byte[StreamHeaderSize + 1];
        header.CopyTo(aad, 0);
        return aad;
    }

    private static byte[] DecodeKey(string base64Key)
    {
        Guard.NotNullOrWhiteSpace(base64Key);
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException)
        {
            throw new ArgumentException("A chave deve estar em Base64.", nameof(base64Key));
        }

        if (key.Length is not (16 or 24 or 32))
        {
            CryptographicOperations.ZeroMemory(key);
            throw new ArgumentException("A chave AES deve ter 16, 24 ou 32 bytes (Base64 de 24, 32 ou 44 caracteres).", nameof(base64Key));
        }

        return key;
    }

    private static void ValidateStreams(Stream input, Stream output)
    {
        Guard.NotNull(input);
        Guard.NotNull(output);
        if (!input.CanRead)
            throw new ArgumentException("O stream de entrada não permite leitura.", nameof(input));
        if (!output.CanWrite)
            throw new ArgumentException("O stream de saída não permite escrita.", nameof(output));
        if (ReferenceEquals(input, output))
            throw new ArgumentException("Os streams de entrada e saída devem ser diferentes.", nameof(output));
    }

    private static byte[] DecodeBase64(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw InvalidData();
        }
    }

    private static void ValidateKey(byte[] key)
    {
        Guard.NotNull(key);
        if (key.Length is not (16 or 24 or 32))
            throw new ArgumentException("A chave AES deve ter 16, 24 ou 32 bytes.", nameof(key));
    }

    // Mensagem única para qualquer falha de formato/integridade: não revela o motivo exato
    private static CryptographicException InvalidData() => new("Dados criptografados inválidos ou corrompidos.");

    // A versão é pública (primeiro byte, sem segredo): informá-la ajuda a diagnosticar dados de formato antigo ou mais novo
    private static CryptographicException UnsupportedVersion(byte version) =>
        new($"Versão de formato dos dados criptografados não suportada: {version} (esperado {FormatVersion}). " +
            "Dados cifrados por versões anteriores do TEC.Core (sem byte de versão) precisam ser recifrados.");
}
