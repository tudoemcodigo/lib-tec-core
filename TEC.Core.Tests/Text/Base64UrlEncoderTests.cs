using System.Security.Cryptography;
using TEC.Core.Text.Codecs;

namespace TEC.Core.Tests.Text;

public class Base64UrlEncoderTests
{
    [Test]
    public async Task Encode_and_decode_round_trip_for_all_lengths()
    {
        for (int length = 0; length <= 64; length++)
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(length);
            string encoded = Base64UrlEncoder.Encode(bytes);

            await Assert.That(encoded).DoesNotContain("=");
            await Assert.That(Base64UrlEncoder.TryDecode(encoded, out byte[] decoded)).IsTrue();
            await Assert.That(decoded.SequenceEqual(bytes)).IsTrue();
        }
    }

    [Test]
    [Arguments("Zg==")]   // preenchimento
    [Arguments("Zm8+")]   // alfabeto do Base64 comum
    [Arguments("Zm8/")]
    [Arguments("Zm 8")]   // espaço
    [Arguments("Z")]      // tamanho impossível (resto 1)
    [Arguments("Zh")]     // bits finais diferentes de zero (forma não canônica de "Zg")
    public async Task TryDecode_rejects_invalid_or_non_canonical_input(string value)
    {
        await Assert.That(Base64UrlEncoder.TryDecode(value, out byte[] bytes)).IsFalse();
        await Assert.That(bytes).IsEmpty();
    }

    [Test]
    public async Task TryDecode_rejects_null() =>
        await Assert.That(Base64UrlEncoder.TryDecode(null, out _)).IsFalse();

    [Test]
    public async Task IsValid_accepts_only_url_alphabet()
    {
        await Assert.That(Base64UrlEncoder.IsValid("AZaz09-_")).IsTrue();
        await Assert.That(Base64UrlEncoder.IsValid("")).IsTrue();
        await Assert.That(Base64UrlEncoder.IsValid("ab+c")).IsFalse();
        await Assert.That(Base64UrlEncoder.IsValid("abcde")).IsFalse();
        await Assert.That(Base64UrlEncoder.IsValid("Zg")).IsTrue();
        await Assert.That(Base64UrlEncoder.IsValid("Zh")).IsFalse();     // bits finais não zerados
        await Assert.That(Base64UrlEncoder.IsValid("Zm9")).IsFalse();    // idem, resto 3
        await Assert.That(Base64UrlEncoder.IsValid("Zm8")).IsTrue();
    }
}
