namespace TEC.Core.Cryptography.Abstractions;

/// <summary>
/// Criptografia híbrida: os dados são cifrados com AES (sem limite de tamanho)
/// e a chave AES é cifrada com RSA. Combina a praticidade da chave pública com o desempenho do AES.
/// </summary>
public interface IHybridCryptography
{
    /// <summary>Criptografa dados de qualquer tamanho com a chave pública.</summary>
    byte[] Encrypt(byte[] data, string publicKeyPem);

    /// <summary>Descriptografa dados com a chave privada.</summary>
    byte[] Decrypt(byte[] encryptedData, string privateKeyPem);

    /// <summary>Criptografa um texto (UTF-8) e retorna Base64.</summary>
    string Encrypt(string plainText, string publicKeyPem);

    /// <summary>Descriptografa um texto em Base64.</summary>
    string Decrypt(string cipherTextBase64, string privateKeyPem);
}
