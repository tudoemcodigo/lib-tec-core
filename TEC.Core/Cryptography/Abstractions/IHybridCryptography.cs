using TEC.Core.Cryptography.Asymmetric;

namespace TEC.Core.Cryptography.Abstractions;

/// <summary>
/// Criptografia híbrida: os dados são cifrados com AES (sem limite de tamanho)
/// e a chave AES é cifrada com RSA. Combina a praticidade da chave pública com o desempenho do AES.
/// </summary>
public interface IHybridCryptography
{
    /// <summary>Criptografa dados de qualquer tamanho com a chave pública em PEM.</summary>
    byte[] Encrypt(byte[] data, string publicKeyPem);

    /// <summary>Criptografa dados de qualquer tamanho com uma chave pública já importada.</summary>
    byte[] Encrypt(byte[] data, RsaKey publicKey);

    /// <summary>Descriptografa dados com a chave privada em PEM.</summary>
    byte[] Decrypt(byte[] encryptedData, string privateKeyPem);

    /// <summary>Descriptografa dados com uma chave privada já importada.</summary>
    byte[] Decrypt(byte[] encryptedData, RsaKey privateKey);

    /// <summary>Criptografa um texto (UTF-8) e retorna Base64.</summary>
    string Encrypt(string plainText, string publicKeyPem);

    /// <summary>Criptografa um texto (UTF-8) com chave pública já importada e retorna Base64.</summary>
    string Encrypt(string plainText, RsaKey publicKey);

    /// <summary>Descriptografa um texto em Base64.</summary>
    string Decrypt(string cipherTextBase64, string privateKeyPem);

    /// <summary>Descriptografa um texto em Base64 com chave privada já importada.</summary>
    string Decrypt(string cipherTextBase64, RsaKey privateKey);

    /// <summary>Criptografa dados em memória (bytes) e salva o resultado diretamente em um arquivo no disco, de forma atômica.</summary>
    Task EncryptToFileAsync(byte[] data, string outputFilePath, string publicKeyPem, CancellationToken cancellationToken = default);

    /// <summary>Criptografa dados em memória (bytes) com chave pública já importada e salva o resultado diretamente em um arquivo no disco, de forma atômica.</summary>
    Task EncryptToFileAsync(byte[] data, string outputFilePath, RsaKey publicKey, CancellationToken cancellationToken = default);

    /// <summary>Lê um arquivo criptografado do disco e retorna os dados binários descriptografados em memória.</summary>
    Task<byte[]> DecryptFromFileAsync(string inputFilePath, string privateKeyPem, CancellationToken cancellationToken = default);

    /// <summary>Lê um arquivo criptografado do disco e retorna os dados binários descriptografados com chave privada já importada.</summary>
    Task<byte[]> DecryptFromFileAsync(string inputFilePath, RsaKey privateKey, CancellationToken cancellationToken = default);

    /// <summary>Criptografa um arquivo do disco para outro em streaming, de forma atômica (chave AES efêmera cifrada com RSA no cabeçalho).</summary>
    Task EncryptFileAsync(string inputFilePath, string outputFilePath, string publicKeyPem, CancellationToken cancellationToken = default);

    /// <summary>Criptografa um arquivo do disco para outro em streaming de forma atômica com chave pública já importada.</summary>
    Task EncryptFileAsync(string inputFilePath, string outputFilePath, RsaKey publicKey, CancellationToken cancellationToken = default);

    /// <summary>Descriptografa um arquivo do disco para outro em streaming, de forma atômica.</summary>
    Task DecryptFileAsync(string inputFilePath, string outputFilePath, string privateKeyPem, CancellationToken cancellationToken = default);

    /// <summary>Descriptografa um arquivo do disco para outro em streaming de forma atômica com chave privada já importada.</summary>
    Task DecryptFileAsync(string inputFilePath, string outputFilePath, RsaKey privateKey, CancellationToken cancellationToken = default);
}
