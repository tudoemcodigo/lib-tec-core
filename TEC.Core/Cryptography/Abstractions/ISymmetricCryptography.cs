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
}
