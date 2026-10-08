using System.Security.Cryptography;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Hybrid;
using TUnit.Assertions.Enums;

namespace TEC.Core.Tests.Cryptography;

public class AsymmetricCryptographyTests
{
    private static readonly RsaCryptography Rsa = new();
    private static readonly RsaKeyPair Keys = Rsa.GenerateKeyPair();

    [Test]
    public async Task GenerateKeyPair_ReturnsPemKeys()
    {
        await Assert.That(Keys.PublicKeyPem).StartsWith("-----BEGIN PUBLIC KEY-----");
        await Assert.That(Keys.PrivateKeyPem).StartsWith("-----BEGIN PRIVATE KEY-----");
        await Assert.That(Keys.ToString()).DoesNotContain("PRIVATE");
    }

    [Test]
    public async Task GenerateKeyPair_KeyTooSmall_Throws()
    {
        await Assert.That(() => Rsa.GenerateKeyPair(1024)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Encrypt_Decrypt_RoundTrip()
    {
        var encrypted = Rsa.Encrypt("segredo", Keys.PublicKeyPem);
        await Assert.That(Rsa.Decrypt(encrypted, Keys.PrivateKeyPem)).IsEqualTo("segredo");
    }

    [Test]
    public async Task Decrypt_WithOtherPrivateKey_Throws()
    {
        var other = Rsa.GenerateKeyPair();
        var encrypted = Rsa.Encrypt("segredo", Keys.PublicKeyPem);
        await Assert.That(() => Rsa.Decrypt(encrypted, other.PrivateKeyPem)).Throws<CryptographicException>();
    }

    [Test]
    public async Task Sign_Verify_ValidAndTampered()
    {
        var signature = Rsa.SignData("payload", Keys.PrivateKeyPem);

        await Assert.That(Rsa.VerifyData("payload", signature, Keys.PublicKeyPem)).IsTrue();
        await Assert.That(Rsa.VerifyData("payload alterado", signature, Keys.PublicKeyPem)).IsFalse();
        await Assert.That(Rsa.VerifyData("payload", "assinatura-invalida", Keys.PublicKeyPem)).IsFalse();
    }

    [Test]
    public async Task Hybrid_EncryptsLargeData()
    {
        var hybrid = new HybridCryptography();
        var data = RandomNumberGenerator.GetBytes(1024 * 1024);

        var encrypted = hybrid.Encrypt(data, Keys.PublicKeyPem);

        await Assert.That(hybrid.Decrypt(encrypted, Keys.PrivateKeyPem)).IsEquivalentTo(data, CollectionOrdering.Matching);
    }
}
