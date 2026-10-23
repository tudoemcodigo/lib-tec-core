using TEC.Core.Cryptography.Asymmetric;

namespace TEC.Core.Cryptography.Abstractions;

/// <summary>
/// Criptografia assimétrica (chave pública cifra/verifica, chave privada decifra/assina).
/// </summary>
public interface IAsymmetricCryptography
{
    /// <summary>Gera um novo par de chaves no formato PEM.</summary>
    /// <param name="keySizeInBits">Tamanho da chave em bits (mínimo 2048).</param>
    RsaKeyPair GenerateKeyPair(int keySizeInBits = 2048);

    /// <summary>
    /// Criptografa dados com a chave pública. O tamanho máximo é limitado pela chave
    /// (≈190 bytes para 2048 bits); para dados maiores use <c>HybridCryptography</c>.
    /// </summary>
    byte[] Encrypt(byte[] data, string publicKeyPem);

    /// <summary>Descriptografa dados com a chave privada.</summary>
    byte[] Decrypt(byte[] encryptedData, string privateKeyPem);

    /// <summary>Criptografa um texto (UTF-8) e retorna Base64.</summary>
    string Encrypt(string plainText, string publicKeyPem);

    /// <summary>Descriptografa um texto em Base64.</summary>
    string Decrypt(string cipherTextBase64, string privateKeyPem);

    /// <summary>Assina os dados com a chave privada.</summary>
    byte[] SignData(byte[] data, string privateKeyPem);

    /// <summary>Assina um texto (UTF-8) e retorna a assinatura em Base64.</summary>
    string SignData(string data, string privateKeyPem);

    /// <summary>Verifica a assinatura com a chave pública.</summary>
    bool VerifyData(byte[] data, byte[] signature, string publicKeyPem);

    /// <summary>Verifica a assinatura (Base64) de um texto com a chave pública.</summary>
    bool VerifyData(string data, string signatureBase64, string publicKeyPem);
}
