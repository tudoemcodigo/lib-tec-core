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
    public async Task RsaKey_ImportedOnce_InteroperatesWithPemOverloads()
    {
        // R07: chave importada uma vez e reutilizada, em vez de interpretar o PEM a cada operação
        using var publicKey = RsaKey.FromPublicKeyPem(Keys.PublicKeyPem);
        using var privateKey = RsaKey.FromPrivateKeyPem(Keys.PrivateKeyPem);
        var data = "segredo"u8.ToArray();

        await Assert.That(publicKey.HasPrivateKey).IsFalse();
        await Assert.That(privateKey.HasPrivateKey).IsTrue();
        await Assert.That(publicKey.KeySize).IsEqualTo(2048);

        await Assert.That(Rsa.Decrypt(Rsa.Encrypt(data, publicKey), Keys.PrivateKeyPem)).IsEquivalentTo(data, CollectionOrdering.Matching);
        await Assert.That(Rsa.Decrypt(Rsa.Encrypt(data, Keys.PublicKeyPem), privateKey)).IsEquivalentTo(data, CollectionOrdering.Matching);

        var signature = Rsa.SignData(data, privateKey);
        await Assert.That(Rsa.VerifyData(data, signature, Keys.PublicKeyPem)).IsTrue();
        await Assert.That(Rsa.VerifyData(data, Rsa.SignData(data, Keys.PrivateKeyPem), publicKey)).IsTrue();
        await Assert.That(Rsa.VerifyData("outro"u8.ToArray(), signature, publicKey)).IsFalse();
        await Assert.That(Rsa.VerifyData(data, signature[..^1], publicKey)).IsFalse();
    }

    [Test]
    public async Task RsaKey_PublicOnly_RefusesPrivateOperations_AndDisposedKeyThrows()
    {
        var publicKey = RsaKey.FromPublicKeyPem(Keys.PublicKeyPem);
        var data = "segredo"u8.ToArray();

        await Assert.That(() => Rsa.Decrypt(new byte[256], publicKey)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => Rsa.SignData(data, publicKey)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => RsaKey.FromPrivateKeyPem(Keys.PublicKeyPem)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => RsaKey.FromPublicKeyPem("não é PEM")).ThrowsExactly<ArgumentException>();

        publicKey.Dispose();
        await Assert.That(() => Rsa.Encrypt(data, publicKey)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task RsaKey_SharedAcrossThreads_SignsAndVerifiesConcurrently()
    {
        using var publicKey = RsaKey.FromPublicKeyPem(Keys.PublicKeyPem);
        using var privateKey = RsaKey.FromPrivateKeyPem(Keys.PrivateKeyPem);

        bool[] results = await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            var data = BitConverter.GetBytes(i);
            var encrypted = Rsa.Encrypt(data, publicKey);
            return Rsa.VerifyData(data, Rsa.SignData(data, privateKey), publicKey)
                && Rsa.Decrypt(encrypted, privateKey).AsSpan().SequenceEqual(data);
        })));

        await Assert.That(results.All(ok => ok)).IsTrue();
    }

    [Test]
    public async Task Hybrid_EncryptsLargeData()
    {
        var hybrid = new HybridCryptography();
        var data = RandomNumberGenerator.GetBytes(1024 * 1024);

        var encrypted = hybrid.Encrypt(data, Keys.PublicKeyPem);

        await Assert.That(hybrid.Decrypt(encrypted, Keys.PrivateKeyPem)).IsEquivalentTo(data, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Hybrid_WithRsaKey_RoundTrip_And_Text()
    {
        var hybrid = new HybridCryptography();
        using var publicKey = RsaKey.FromPublicKeyPem(Keys.PublicKeyPem);
        using var privateKey = RsaKey.FromPrivateKeyPem(Keys.PrivateKeyPem);

        var data = RandomNumberGenerator.GetBytes(128 * 1024);
        var encryptedBytes = hybrid.Encrypt(data, publicKey);
        var decryptedBytes = hybrid.Decrypt(encryptedBytes, privateKey);
        await Assert.That(decryptedBytes).IsEquivalentTo(data, CollectionOrdering.Matching);

        var plainText = "Mensagem ultra confidencial para hybrid cryptography com RsaKey";
        var cipherText = hybrid.Encrypt(plainText, publicKey);
        var recoveredText = hybrid.Decrypt(cipherText, privateKey);
        await Assert.That(recoveredText).IsEqualTo(plainText);
    }

    [Test]
    public async Task Hybrid_FileEncryption_RoundTrip()
    {
        var hybrid = new HybridCryptography();
        using var publicKey = RsaKey.FromPublicKeyPem(Keys.PublicKeyPem);
        using var privateKey = RsaKey.FromPrivateKeyPem(Keys.PrivateKeyPem);

        var data = RandomNumberGenerator.GetBytes(64 * 1024 + 13);
        var tempDir = Path.Combine(Path.GetTempPath(), $"tec-hybrid-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var rawFile = Path.Combine(tempDir, "source.bin");
            var encFilePem = Path.Combine(tempDir, "enc-pem.bin");
            var decFilePem = Path.Combine(tempDir, "dec-pem.bin");
            var encFileKey = Path.Combine(tempDir, "enc-key.bin");
            var decFileKey = Path.Combine(tempDir, "dec-key.bin");

            await File.WriteAllBytesAsync(rawFile, data);

            // 1. Streaming file encryption com PEM
            await hybrid.EncryptFileAsync(rawFile, encFilePem, Keys.PublicKeyPem);
            await hybrid.DecryptFileAsync(encFilePem, decFilePem, Keys.PrivateKeyPem);
            var decBytesPem = await File.ReadAllBytesAsync(decFilePem);
            await Assert.That(decBytesPem).IsEquivalentTo(data, CollectionOrdering.Matching);

            // 2. Streaming file encryption com RsaKey
            await hybrid.EncryptFileAsync(rawFile, encFileKey, publicKey);
            await hybrid.DecryptFileAsync(encFileKey, decFileKey, privateKey);
            var decBytesKey = await File.ReadAllBytesAsync(decFileKey);
            await Assert.That(decBytesKey).IsEquivalentTo(data, CollectionOrdering.Matching);

            // 3. EncryptToFileAsync / DecryptFromFileAsync
            var toFile = Path.Combine(tempDir, "tofile.bin");
            await hybrid.EncryptToFileAsync(data, toFile, publicKey);
            var fromFile = await hybrid.DecryptFromFileAsync(toFile, privateKey);
            await Assert.That(fromFile).IsEquivalentTo(data, CollectionOrdering.Matching);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ---------- DecryptFromFileAsync: limite de tamanho ----------

    [Test]
    public async Task Hybrid_DecryptFromFileAsync_AboveLimit_FailsClosed()
    {
        var hybrid = new HybridCryptography();
        using var publicKey = RsaKey.FromPublicKeyPem(Keys.PublicKeyPem);
        using var privateKey = RsaKey.FromPrivateKeyPem(Keys.PrivateKeyPem);
        var data = RandomNumberGenerator.GetBytes(2048);
        var path = Path.Combine(Path.GetTempPath(), $"tec-hybrid-limite-{Guid.NewGuid():N}.bin");
        try
        {
            await hybrid.EncryptToFileAsync(data, path, publicKey);
            int size = (int)new FileInfo(path).Length;

            await Assert.That(await hybrid.DecryptFromFileAsync(path, privateKey, maxBytes: size)).IsEquivalentTo(data, CollectionOrdering.Matching);
            await Assert.That(async () => await hybrid.DecryptFromFileAsync(path, privateKey, maxBytes: size - 1))
                .ThrowsExactly<InvalidDataException>();
            await Assert.That(async () => await hybrid.DecryptFromFileAsync(path, Keys.PrivateKeyPem, maxBytes: size - 1))
                .ThrowsExactly<InvalidDataException>();
            await Assert.That(async () => await hybrid.DecryptFromFileAsync(path, privateKey, maxBytes: 0))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(async () => await hybrid.DecryptFromFileAsync(path, Keys.PrivateKeyPem, maxBytes: -5))
                .Throws<ArgumentOutOfRangeException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Hybrid_DecryptFromFileAsync_HugeFile_RefusedByDefaultLimit()
    {
        var hybrid = new HybridCryptography();
        using var privateKey = RsaKey.FromPrivateKeyPem(Keys.PrivateKeyPem);
        var path = Path.Combine(Path.GetTempPath(), $"tec-hybrid-grande-{Guid.NewGuid():N}.bin");
        try
        {
            await using (var stream = File.Create(path))
                stream.SetLength(HybridCryptography.DefaultMaxFileBytes + 1L);

            await Assert.That(async () => await hybrid.DecryptFromFileAsync(path, privateKey))
                .ThrowsExactly<InvalidDataException>();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
