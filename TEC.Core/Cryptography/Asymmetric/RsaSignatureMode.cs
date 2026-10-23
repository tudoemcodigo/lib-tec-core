namespace TEC.Core.Cryptography.Asymmetric;

/// <summary>
/// Esquema de preenchimento das assinaturas RSA.
/// </summary>
public enum RsaSignatureMode
{
    /// <summary>RSASSA-PSS (padrão recomendado, com prova de segurança).</summary>
    Pss = 0,

    /// <summary>PKCS#1 v1.5. Use apenas para compatibilidade com sistemas legados (ex.: JWT RS256).</summary>
    Pkcs1 = 1
}
