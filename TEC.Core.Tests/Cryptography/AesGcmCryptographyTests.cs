using System.Security.Cryptography;
using TEC.Core.Cryptography.Symmetric;
using TUnit.Assertions.Enums;

namespace TEC.Core.Tests.Cryptography;

public class AesGcmCryptographyTests
{
    private readonly AesGcmCryptography _sut = new();

    [Test]
    public async Task Encrypt_Decrypt_Text_RoundTrip()
    {
        var key = _sut.GenerateKeyBase64();

        var encrypted = _sut.Encrypt("Informação sigilosa ção", key);

        await Assert.That(encrypted).IsNotEqualTo("Informação sigilosa ção");
        await Assert.That(_sut.Decrypt(encrypted, key)).IsEqualTo("Informação sigilosa ção");
    }

    [Test]
    public async Task Encrypt_SameText_ProducesDifferentOutputs()
    {
        var key = _sut.GenerateKeyBase64();
        await Assert.That(_sut.Encrypt("abc", key)).IsNotEqualTo(_sut.Encrypt("abc", key));
    }

    [Test]
    public async Task Decrypt_WithWrongKey_Throws()
    {
        var encrypted = _sut.Encrypt("abc", _sut.GenerateKeyBase64());
        await Assert.That(() => _sut.Decrypt(encrypted, _sut.GenerateKeyBase64())).Throws<CryptographicException>();
    }

    [Test]
    public async Task Decrypt_TamperedData_Throws()
    {
        var key = _sut.GenerateKey();
        var encrypted = _sut.Encrypt([1, 2, 3, 4], key);
        encrypted[^1] ^= 0xFF;

        await Assert.That(() => _sut.Decrypt(encrypted, key)).Throws<CryptographicException>();
    }

    [Test]
    public async Task Encrypt_InvalidKeySize_Throws()
    {
        await Assert.That(() => _sut.Encrypt([1], new byte[10])).ThrowsExactly<ArgumentException>();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(64 * 1024)]
    [Arguments(64 * 1024 * 3 + 17)]
    public async Task EncryptAsync_DecryptAsync_Stream_RoundTrip(int size)
    {
        var key = _sut.GenerateKey();
        var original = RandomNumberGenerator.GetBytes(size);

        using var encrypted = new MemoryStream();
        await _sut.EncryptAsync(new MemoryStream(original), encrypted, key);

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await _sut.DecryptAsync(encrypted, decrypted, key);

        await Assert.That(decrypted.ToArray()).IsEquivalentTo(original, CollectionOrdering.Matching);
    }

    [Test]
    public async Task DecryptAsync_TruncatedStream_Throws()
    {
        var key = _sut.GenerateKey();
        using var encrypted = new MemoryStream();
        await _sut.EncryptAsync(new MemoryStream(RandomNumberGenerator.GetBytes(64 * 1024 * 2 + 5)), encrypted, key);

        // Remove os blocos finais: sem o bloco final, a descriptografia deve falhar
        var truncated = encrypted.ToArray()[..(33 + 21 + 64 * 1024)];

        await Assert.That(() => _sut.DecryptAsync(new MemoryStream(truncated), new MemoryStream(), key))
            .Throws<CryptographicException>();
    }

    [Test]
    public async Task DecryptAsync_TrailingData_Throws()
    {
        var key = _sut.GenerateKey();
        using var encrypted = new MemoryStream();
        await _sut.EncryptAsync(new MemoryStream([1, 2, 3]), encrypted, key);
        encrypted.WriteByte(0x00);

        await Assert.That(() => _sut.DecryptAsync(new MemoryStream(encrypted.ToArray()), new MemoryStream(), key))
            .Throws<CryptographicException>();
    }

    [Test]
    public async Task DecryptAsync_TamperedSalt_Throws()
    {
        var key = _sut.GenerateKey();
        using var encrypted = new MemoryStream();
        await _sut.EncryptAsync(new MemoryStream([1, 2, 3]), encrypted, key);
        var bytes = encrypted.ToArray();
        bytes[5] ^= 0x01;

        await Assert.That(() => _sut.DecryptAsync(new MemoryStream(bytes), new MemoryStream(), key))
            .Throws<CryptographicException>();
    }

    [Test]
    [Arguments("não-é-base64!!")]
    [Arguments("AAAA")]
    public async Task Decrypt_InvalidInput_ThrowsGenericCryptographicException(string cipher)
    {
        var ex = await Assert.That(() => _sut.Decrypt(cipher, _sut.GenerateKeyBase64())).Throws<CryptographicException>();
        await Assert.That(ex!.Message).DoesNotContain(cipher);
    }
}
