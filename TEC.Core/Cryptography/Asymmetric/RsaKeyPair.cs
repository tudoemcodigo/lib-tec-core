using System.Text.Json.Serialization;

namespace TEC.Core.Cryptography.Asymmetric;

/// <summary>
/// Par de chaves RSA no formato PEM.
/// </summary>
/// <param name="PublicKeyPem">Chave pública (SubjectPublicKeyInfo), pode ser distribuída.</param>
/// <param name="PrivateKeyPem">
/// Chave privada (PKCS#8), deve ser mantida em segredo (cofre de segredos, nunca no código ou em logs).
/// Não é incluída em serializações JSON nem no <see cref="ToString"/> para evitar vazamentos acidentais.
/// </param>
public sealed record RsaKeyPair(string PublicKeyPem, [property: JsonIgnore] string PrivateKeyPem)
{
    /// <summary>Evita que a chave privada seja exposta acidentalmente em logs.</summary>
    public override string ToString() => $"RsaKeyPair {{ PublicKeyPem = {PublicKeyPem}, PrivateKeyPem = *** }}";
}
