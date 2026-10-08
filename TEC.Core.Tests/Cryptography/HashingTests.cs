using TEC.Core.Cryptography.Generators;
using TEC.Core.Cryptography.Hashing;

namespace TEC.Core.Tests.Cryptography;

public class HashingTests
{
    [Test]
    public async Task ComputeHash_Sha256_KnownValue()
    {
        await Assert.That(HashHelper.ComputeHash("abc")).IsEqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Test]
    public async Task ComputeHmac_KnownValue()
    {
        // Vetor de teste RFC 4231 (caso 2)
        await Assert.That(HashHelper.ComputeHmac("what do ya want for nothing?", "Jefe"))
            .IsEqualTo("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843");
    }

    [Test]
    public async Task ComputeHashAsync_Stream_MatchesInMemory()
    {
        var bytes = "conteúdo"u8.ToArray();
        var hash = await HashHelper.ComputeHashAsync(new MemoryStream(bytes), HashAlgorithmType.Sha512);
        await Assert.That(hash).IsEqualTo(Convert.ToHexString(HashHelper.ComputeHash(bytes, HashAlgorithmType.Sha512)).ToLowerInvariant());
    }

    [Test]
    public async Task FixedTimeEquals_IsOrdinal_CaseSensitive()
    {
        // Regressão: antes ignorava maiúsculas/minúsculas, o que enfraquece tokens e assinaturas em Base64
        await Assert.That(HashHelper.FixedTimeEquals("ABCDEF", "abcdef")).IsFalse();
        await Assert.That(HashHelper.FixedTimeEquals("aBc+/=", "aBc+/=")).IsTrue();
        await Assert.That(HashHelper.FixedTimeEquals("abc", "abd")).IsFalse();
        await Assert.That(HashHelper.FixedTimeEquals("abc", "abcd")).IsFalse();
        await Assert.That(HashHelper.FixedTimeEquals(null, "abd")).IsFalse();
        await Assert.That(HashHelper.FixedTimeEquals("İ", "i")).IsFalse();
    }

    [Test]
    public async Task FixedTimeEqualsHex_IgnoresHexCase_AndRejectsInvalidHex()
    {
        var hmac = HashHelper.ComputeHmac("payload", "chave");
        await Assert.That(HashHelper.FixedTimeEqualsHex(hmac.ToUpperInvariant(), hmac)).IsTrue();
        await Assert.That(HashHelper.FixedTimeEqualsHex("ABCDEF", "abcdef")).IsTrue();
        await Assert.That(HashHelper.FixedTimeEqualsHex("abcdef", "abcdee")).IsFalse();
        await Assert.That(HashHelper.FixedTimeEqualsHex("abc", "abc")).IsFalse();     // tamanho ímpar
        await Assert.That(HashHelper.FixedTimeEqualsHex("zz", "zz")).IsFalse();       // não é hexadecimal
        await Assert.That(HashHelper.FixedTimeEqualsHex(null, "ab")).IsFalse();
    }

    [Test]
    public async Task PasswordHasher_Verify_NullHash_ReturnsFalse_WithEquivalentCost()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var stored = hasher.Hash("senha-forte");

        // Usuário inexistente: retorna false sem lançar exceção (e executa uma derivação fictícia)
        await Assert.That(hasher.Verify("senha-forte", null)).IsFalse();
        await Assert.That(hasher.Verify("senha-forte", "lixo")).IsFalse();
        await Assert.That(hasher.Verify("senha-forte", stored)).IsTrue();

        // O custo da verificação fictícia deve ser da mesma ordem da verificação real (não retorna imediatamente)
        // Melhor de várias medições, intercaladas: a carga da máquina (testes em paralelo, runner do CI) oscila no tempo e,
        // medindo real e fictícia alternadamente, um pico afeta as duas em vez de inflar só uma delas
        var (real, dummy) = FastestInterleaved(
            () => hasher.Verify("outra-senha", stored),
            () => hasher.Verify("outra-senha", null));

        await Assert.That(dummy.TotalMilliseconds).IsGreaterThan(real.TotalMilliseconds / 4);
    }

    private static (TimeSpan First, TimeSpan Second) FastestInterleaved(Action first, Action second)
    {
        var bestFirst = TimeSpan.MaxValue;
        var bestSecond = TimeSpan.MaxValue;
        for (int i = 0; i < 7; i++)
        {
            bestFirst = Min(bestFirst, Measure(first));
            bestSecond = Min(bestSecond, Measure(second));
        }

        return (bestFirst, bestSecond);

        static TimeSpan Measure(Action action)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            action();
            watch.Stop();
            return watch.Elapsed;
        }

        static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
    }

    [Test]
    public async Task PasswordHasher_HashAndVerify()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var hash = hasher.Hash("Senha@123");

        await Assert.That(hash).StartsWith("PBKDF2-SHA256$100000$");
        await Assert.That(hasher.Verify("Senha@123", hash)).IsTrue();
        await Assert.That(hasher.Verify("senha@123", hash)).IsFalse();
        await Assert.That(hasher.Verify("Senha@123", "hash-invalido")).IsFalse();
    }

    [Test]
    public async Task PasswordHasher_NeedsRehash_WhenIterationsIncrease()
    {
        var hash = new Pbkdf2PasswordHasher(100_000).Hash("x");

        await Assert.That(new Pbkdf2PasswordHasher(100_000).NeedsRehash(hash)).IsFalse();
        await Assert.That(new Pbkdf2PasswordHasher(200_000).NeedsRehash(hash)).IsTrue();
    }

    [Test]
    public async Task GeneratePassword_ContainsAllGroups()
    {
        var password = SecureRandomGenerator.GeneratePassword(12);

        await Assert.That(password.Length).IsEqualTo(12);
        await Assert.That(password.Any(char.IsUpper)).IsTrue();
        await Assert.That(password.Any(char.IsLower)).IsTrue();
        await Assert.That(password.Any(char.IsDigit)).IsTrue();
        await Assert.That(password.Any(c => !char.IsLetterOrDigit(c))).IsTrue();
    }

    [Test]
    public async Task GenerateNumericCode_And_Token()
    {
        var code = SecureRandomGenerator.GenerateNumericCode(6);
        await Assert.That(code).Matches("^[0-9]{6}$");

        var token = SecureRandomGenerator.GenerateToken();
        await Assert.That(token).Matches("^[A-Za-z0-9_-]+$");
    }
}
