namespace TEC.Core.Cryptography.Abstractions;

/// <summary>
/// Criptografia simétrica (a mesma chave cifra e decifra).
/// </summary>
public interface ISymmetricCryptography
{
    /// <summary>Gera uma nova chave aleatória segura.</summary>
    byte[] GenerateKey();

    /// <summary>Gera uma nova chave aleatória segura em Base64.</summary>
    string GenerateKeyBase64();

    /// <summary>Criptografa os dados.</summary>
    /// <param name="plainData">Dados em claro.</param>
    /// <param name="key">Chave de 16, 24 ou 32 bytes.</param>
    /// <param name="associatedData">Dados adicionais autenticados, mas não cifrados (opcional).</param>
    byte[] Encrypt(byte[] plainData, byte[] key, byte[]? associatedData = null);

    /// <summary>Descriptografa os dados. Lança <see cref="System.Security.Cryptography.CryptographicException"/> se a chave estiver errada ou os dados tiverem sido adulterados.</summary>
    byte[] Decrypt(byte[] encryptedData, byte[] key, byte[]? associatedData = null);

    /// <summary>Criptografa um texto (UTF-8) e retorna o resultado em Base64.</summary>
    /// <param name="plainText">Texto em claro.</param>
    /// <param name="base64Key">Chave em Base64.</param>
    string Encrypt(string plainText, string base64Key);

    /// <summary>Descriptografa um texto em Base64 gerado por <see cref="Encrypt(string, string)"/>.</summary>
    string Decrypt(string cipherTextBase64, string base64Key);

    /// <summary>Criptografa um stream em blocos, sem carregar todo o conteúdo em memória (ideal para arquivos grandes).</summary>
    Task EncryptAsync(Stream input, Stream output, byte[] key, CancellationToken cancellationToken = default);

    /// <summary>Descriptografa um stream gerado por <see cref="EncryptAsync"/>.</summary>
    Task DecryptAsync(Stream input, Stream output, byte[] key, CancellationToken cancellationToken = default);

    /// <summary>Criptografa um arquivo do disco para outro em streaming, de forma atômica.</summary>
    /// <param name="inputFilePath">Caminho do arquivo em claro de entrada.</param>
    /// <param name="outputFilePath">Caminho do arquivo criptografado de saída.</param>
    /// <param name="key">Chave de 16, 24 ou 32 bytes.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    Task EncryptFileAsync(string inputFilePath, string outputFilePath, byte[] key, CancellationToken cancellationToken = default);

    /// <summary>Criptografa um arquivo do disco para outro em streaming, com chave em Base64.</summary>
    Task EncryptFileAsync(string inputFilePath, string outputFilePath, string base64Key, CancellationToken cancellationToken = default);

    /// <summary>Descriptografa um arquivo criptografado do disco para outro em streaming, de forma atômica.</summary>
    /// <param name="inputFilePath">Caminho do arquivo criptografado de entrada.</param>
    /// <param name="outputFilePath">Caminho do arquivo em claro de saída.</param>
    /// <param name="key">Chave de 16, 24 ou 32 bytes.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    Task DecryptFileAsync(string inputFilePath, string outputFilePath, byte[] key, CancellationToken cancellationToken = default);

    /// <summary>Descriptografa um arquivo criptografado do disco para outro em streaming, com chave em Base64.</summary>
    Task DecryptFileAsync(string inputFilePath, string outputFilePath, string base64Key, CancellationToken cancellationToken = default);

    /// <summary>Criptografa dados em memória (bytes) e salva o resultado diretamente em um arquivo no disco, de forma atômica.</summary>
    /// <param name="plainData">Dados binários em claro.</param>
    /// <param name="outputFilePath">Caminho do arquivo criptografado de destino.</param>
    /// <param name="key">Chave de 16, 24 ou 32 bytes.</param>
    /// <param name="associatedData">Dados adicionais autenticados (opcional).</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    Task EncryptToFileAsync(byte[] plainData, string outputFilePath, byte[] key, byte[]? associatedData = null, CancellationToken cancellationToken = default);

    /// <summary>Criptografa dados em memória (bytes) com chave em Base64 e salva o resultado diretamente em um arquivo no disco, de forma atômica.</summary>
    Task EncryptToFileAsync(byte[] plainData, string outputFilePath, string base64Key, byte[]? associatedData = null, CancellationToken cancellationToken = default);

    /// <summary>Lê um arquivo criptografado do disco e retorna os dados binários descriptografados em memória.</summary>
    /// <param name="inputFilePath">Caminho do arquivo criptografado.</param>
    /// <param name="key">Chave de 16, 24 ou 32 bytes.</param>
    /// <param name="associatedData">Dados adicionais autenticados (opcional).</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    Task<byte[]> DecryptFromFileAsync(string inputFilePath, byte[] key, byte[]? associatedData = null, CancellationToken cancellationToken = default);

    /// <summary>Lê um arquivo criptografado do disco e retorna os dados binários descriptografados em memória, com chave em Base64.</summary>
    Task<byte[]> DecryptFromFileAsync(string inputFilePath, string base64Key, byte[]? associatedData = null, CancellationToken cancellationToken = default);
}
