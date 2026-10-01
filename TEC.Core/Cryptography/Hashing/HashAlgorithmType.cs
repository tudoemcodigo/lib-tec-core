namespace TEC.Core.Cryptography.Hashing;

/// <summary>
/// Algoritmos de hash suportados.
/// </summary>
public enum HashAlgorithmType
{
    /// <summary>SHA-256 (padrão recomendado).</summary>
    Sha256 = 0,

    /// <summary>SHA-384.</summary>
    Sha384 = 1,

    /// <summary>SHA-512.</summary>
    Sha512 = 2
}
