using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TEC.Core.Common.Globalization;
using TEC.Core.Common.Results;
using TEC.Core.Common.Serialization;
using TEC.Core.Csv;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Cryptography.Hybrid;
using TEC.Core.Cryptography.Symmetric;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Enums;
using TEC.Core.Responses;
using TEC.Core.Responses.Pagination;

namespace TEC.Core.Tests.Security;

/// <summary>
/// Testes de regressão das correções de segurança (Zero Trust: toda entrada é considerada não confiável).
/// </summary>
public class SecurityRegressionTests
{
    private static readonly RsaCryptography Rsa = new();
    private static readonly RsaKeyPair Keys = Rsa.GenerateKeyPair();

    // ---------- Globalização (disponibilidade) ----------

    [Test]
    public async Task BrazilianCulture_Fallback_FormatsLikePtBr()
    {
        var culture = BrazilianCulture.CreateFallback();

        await Assert.That(culture.IsReadOnly).IsTrue();
        await Assert.That(1234.56m.ToString("N2", culture)).IsEqualTo("1.234,56");
        await Assert.That(1234.56m.ToString("C2", culture)).IsEqualTo("R$ 1.234,56");
        await Assert.That(new DateOnly(2026, 9, 30).ToString("d 'de' MMMM 'de' yyyy", culture)).IsEqualTo("30 de setembro de 2026");
        await Assert.That(culture.DateTimeFormat.GetDayName(DayOfWeek.Wednesday)).IsEqualTo("quarta-feira");
    }

    // ---------- Criptografia ----------

    [Test]
    public async Task Rsa_RejectsWeakImportedKey()
    {
        using var weak = RSA.Create(1024);
        var weakPublicPem = weak.ExportSubjectPublicKeyInfoPem();

        await Assert.That(() => Rsa.Encrypt("x", weakPublicPem)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Rsa_RejectsOversizedKeyGeneration()
    {
        await Assert.That(() => Rsa.GenerateKeyPair(16384)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Rsa_InvalidPem_DoesNotLeakContent()
    {
        var ex = await Assert.That(() => Rsa.Encrypt("x", "-----BEGIN PUBLIC KEY-----\nSEGREDO\n-----END PUBLIC KEY-----"))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.Message).DoesNotContain("SEGREDO");
    }

    [Test]
    public async Task Rsa_SignatureModesAreNotInterchangeable()
    {
        var pkcs1 = new RsaCryptography(RsaSignatureMode.Pkcs1);
        var signature = pkcs1.SignData("payload", Keys.PrivateKeyPem);

        await Assert.That(pkcs1.VerifyData("payload", signature, Keys.PublicKeyPem)).IsTrue();
        await Assert.That(Rsa.VerifyData("payload", signature, Keys.PublicKeyPem)).IsFalse();
    }

    [Test]
    public async Task RsaKeyPair_PrivateKeyIsNotSerialized()
    {
        var json = Keys.ToJson();

        await Assert.That(json).DoesNotContain("PRIVATE");
        await Assert.That(Keys.ToString()).DoesNotContain("PRIVATE");
    }

    [Test]
    public async Task Hybrid_TamperedData_ThrowsGenericException()
    {
        var hybrid = new HybridCryptography();
        var encrypted = hybrid.Encrypt(RandomNumberGenerator.GetBytes(100), Keys.PublicKeyPem);

        // Índice 0 é o byte de versão (validado à parte, com mensagem própria)
        foreach (var index in new[] { 1, 2, 10, encrypted.Length - 1 })
        {
            var tampered = (byte[])encrypted.Clone();
            tampered[index] ^= 0x01;
            var ex = await Assert.That(() => hybrid.Decrypt(tampered, Keys.PrivateKeyPem)).Throws<CryptographicException>();
            await Assert.That(ex!.Message).IsEqualTo("Falha ao descriptografar os dados.");
        }
    }

    [Test]
    public async Task Hybrid_Format_StartsWithVersion_AndRejectsUnknownVersion()
    {
        var hybrid = new HybridCryptography();
        var encrypted = hybrid.Encrypt(RandomNumberGenerator.GetBytes(10), Keys.PublicKeyPem);
        await Assert.That(encrypted[0]).IsEqualTo(HybridCryptography.FormatVersion);

        var otherVersion = (byte[])encrypted.Clone();
        otherVersion[0] = 2;
        var ex = await Assert.That(() => hybrid.Decrypt(otherVersion, Keys.PrivateKeyPem)).Throws<CryptographicException>();
        await Assert.That(ex!.Message).Contains("Versão de formato");
    }

    [Test]
    public async Task AesGcm_Format_StartsWithVersion_AndRejectsUnknownVersion()
    {
        var aes = new AesGcmCryptography();
        var key = aes.GenerateKey();
        var encrypted = aes.Encrypt([1, 2, 3], key);

        // [versão 1][nonce 12][tag 16][dados]
        await Assert.That(encrypted.Length).IsEqualTo(1 + 12 + 16 + 3);
        await Assert.That(encrypted[0]).IsEqualTo(AesGcmCryptography.FormatVersion);

        var otherVersion = (byte[])encrypted.Clone();
        otherVersion[0] = 0;
        var ex = await Assert.That(() => aes.Decrypt(otherVersion, key)).Throws<CryptographicException>();
        await Assert.That(ex!.Message).Contains("Versão de formato");
    }

    [Test]
    public async Task Rsa_Import_RejectsOversizedPem()
    {
        var rsa = new RsaCryptography();
        var hugePem = "-----BEGIN PUBLIC KEY-----\n" + new string('A', 40_000) + "\n-----END PUBLIC KEY-----";

        await Assert.That(() => rsa.Encrypt([1], hugePem)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Rsa_Import_RejectsKeyAboveMaximumImportSize()
    {
        // Chave pública com módulo de 16.392 bits (acima do limite de 16.384), montada manualmente em DER
        var modulus = new byte[16_392 / 8];
        RandomNumberGenerator.Fill(modulus);
        modulus[0] |= 0x80;
        modulus[^1] |= 0x01;

        var rsaKey = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        using (rsaKey.PushSequence())
        {
            rsaKey.WriteInteger(new System.Numerics.BigInteger(modulus, isUnsigned: true, isBigEndian: true));
            rsaKey.WriteInteger(65537);
        }

        var spki = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        using (spki.PushSequence())
        {
            using (spki.PushSequence())
            {
                spki.WriteObjectIdentifier("1.2.840.113549.1.1.1");
                spki.WriteNull();
            }
            spki.WriteBitString(rsaKey.Encode());
        }

        var pem = PemEncoding.WriteString("PUBLIC KEY", spki.Encode());
        var ex = await Assert.That(() => new RsaCryptography().Encrypt([1], pem)).ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.ParamName).IsEqualTo("pem");
    }

    [Test]
    public async Task PasswordHasher_TamperedIterations_RejectedWithoutCpuExhaustion()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var parts = hasher.Hash("senha").Split('$');
        var tampered = $"{parts[0]}$2147483647${parts[2]}${parts[3]}";

        var sw = Stopwatch.StartNew();
        var verified = hasher.Verify("senha", tampered);
        sw.Stop();

        // 2 bilhões de iterações levariam minutos: o limite é folgado para não oscilar com a carga do runner do CI
        await Assert.That(verified).IsFalse();
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task PasswordHasher_OversizedStoredHash_Rejected()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var huge = Convert.ToBase64String(new byte[100_000]);
        await Assert.That(hasher.Verify("senha", $"PBKDF2-SHA256$100000${huge}${huge}")).IsFalse();
    }

    [Test]
    public async Task PasswordHasher_NormalizesUnicode()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var composed = "café";      // "é" como um único caractere
        var decomposed = "café";   // "e" + acento combinante

        await Assert.That(hasher.Verify(decomposed, hasher.Hash(composed))).IsTrue();
    }

    [Test]
    public async Task PasswordHasher_RejectsOversizedPassword()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var huge = new string('a', Pbkdf2PasswordHasher.MaxPasswordLength + 1);

        await Assert.That(() => hasher.Hash(huge)).ThrowsExactly<ArgumentException>();
        await Assert.That(hasher.Verify(huge, hasher.Hash("a"))).IsFalse();
    }

    // ---------- JSON ----------

    [Test]
    public async Task Json_EscapesHtmlSensitiveCharacters_ButKeepsAccents()
    {
        var json = new { Nome = "<script>alert('x')</script> João" }.ToJson();

        await Assert.That(json).DoesNotContain("<script>");
        await Assert.That(json).Contains("João");
    }

    [Test]
    public async Task Json_DefaultOptionsAreImmutable()
    {
        await Assert.That(() => JsonDefaults.Options.PropertyNamingPolicy = null).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Json_RejectsUndefinedNumericEnum()
    {
        await Assert.That(() => "{\"type\":999}".FromJson<ErrorHolder>()).ThrowsExactly<JsonException>();
    }

    private sealed record ErrorHolder(ErrorType Type);

    // ---------- CSV ----------

    public sealed class Row
    {
        public string? Text { get; set; }
        public decimal Number { get; set; }
    }

    private static async Task<string> WriteAsync(IEnumerable<Row> rows, CsvOptions? options = null)
    {
        using var stream = new MemoryStream();
        await new CsvWriter(options).WriteAsync(stream, rows);
        return Encoding.UTF8.GetString(stream.ToArray()).TrimStart('﻿');
    }

    private static Task<List<Row>> ReadAsync(string csv, CsvOptions? options = null) =>
        new CsvReader(options).ReadAsync<Row>(new MemoryStream(Encoding.UTF8.GetBytes(csv))).ToListAsync().AsTask();

    [Test]
    public async Task Csv_SanitizesFormulasByDefault_AndRoundTrips()
    {
        Row[] rows = [new() { Text = "=cmd|' /C calc'!A0", Number = -10 }, new() { Text = "＝SUM(A1)", Number = 1 }];

        var csv = await WriteAsync(rows);
        var back = await ReadAsync(csv);

        await Assert.That(csv).Contains("'=cmd");
        await Assert.That(csv).Contains(";-10");   // números negativos não são alterados
        await Assert.That(back[0].Text).IsEqualTo(rows[0].Text);
        await Assert.That(back[1].Text).IsEqualTo(rows[1].Text);
        await Assert.That(back[0].Number).IsEqualTo(-10);
    }

    [Test]
    public async Task Csv_FieldTooLarge_Throws()
    {
        var csv = "Text;Number\n\"" + new string('x', 2000);   // aspas nunca fechadas
        await Assert.That(async () => { await ReadAsync(csv, new CsvOptions { MaxFieldLength = 1000 }); }).ThrowsExactly<CsvException>();
    }

    [Test]
    public async Task Csv_TooManyColumns_Throws()
    {
        var csv = "Text;Number\n" + string.Join(';', Enumerable.Repeat("1", 50));
        await Assert.That(async () => { await ReadAsync(csv, new CsvOptions { MaxColumns = 10 }); }).ThrowsExactly<CsvException>();
    }

    [Test]
    public async Task Csv_ErrorDoesNotLeakRawValue()
    {
        var ex = await Assert.That(async () => { await ReadAsync("Text;Number\nx;123.456.789-09"); }).ThrowsExactly<CsvException>();

        await Assert.That(ex!.ToString()).DoesNotContain("123.456.789-09");
        await Assert.That(ex.RawValue).IsNull();
        await Assert.That(ex.InnerException).IsNull();
    }

    [Test]
    public async Task Csv_ErrorIncludesRawValueWhenOptedIn()
    {
        var ex = await Assert.That(async () => { await ReadAsync("Text;Number\nx;abc", new CsvOptions { IncludeRawValueInErrors = true }); })
            .ThrowsExactly<CsvException>();

        await Assert.That(ex!.RawValue).IsEqualTo("abc");
        await Assert.That(ex.InnerException).IsNotNull();
    }

    // ---------- Enums ----------

    [Flags]
    public enum Permission { None = 0, Read = 1, Write = 2 }

    [Test]
    public async Task Enum_Flags_RejectsUndefinedBits()
    {
        await Assert.That(EnumHelper.TryParse<Permission>("Read, Write", out var both)).IsTrue();
        await Assert.That(both).IsEqualTo(Permission.Read | Permission.Write);
        await Assert.That(EnumHelper.TryParse<Permission>("64", out _)).IsFalse();
    }

    [Test]
    public async Task Enum_ParseError_DoesNotEchoInput()
    {
        var ex = await Assert.That(() => EnumHelper.Parse<Permission>("<valor malicioso>\r\nLOG FALSO")).ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.Message).DoesNotContain("malicioso");
    }

    // ---------- API / paginação ----------

    [Test]
    public async Task ApiResponse_InternalFailure_HidesDetails()
    {
        var result = Result.Failure(Error.Failure("DB", "Timeout em Server=10.0.0.5;User=sa"));

        var response = ApiResponse.FromResult(result);

        await Assert.That(response.StatusCode).IsEqualTo(500);
        await Assert.That(response.ToJson()).DoesNotContain("10.0.0.5");
        await Assert.That(response.Errors).IsEmpty();
    }

    [Test]
    public async Task ApiResponse_MixedErrors_WithInternalFailureNotFirst_HidesAllDetails()
    {
        var result = Result<int>.Failure(
            Error.Validation("NOME_OBRIGATORIO", "Nome obrigatório", "nome"),
            Error.Failure("DB", "Timeout em Server=10.0.0.5;User=sa"));

        var response = ApiResponse<int>.FromResult(result);

        await Assert.That(response.StatusCode).IsEqualTo(500);
        await Assert.That(response.ToJson()).DoesNotContain("10.0.0.5");
        await Assert.That(response.ToJson()).DoesNotContain("NOME_OBRIGATORIO");
        await Assert.That(response.Errors).IsEmpty();
    }

    [Test]
    public async Task ApiResponse_MixedErrors_WithExternalServiceNotFirst_Returns502WithoutDetails()
    {
        var result = Result.Failure(
            Error.NotFound("CLIENTE_NAO_ENCONTRADO", "Cliente não encontrado"),
            Error.ExternalService("SEFAZ", "Falha em https://10.0.0.9/sefaz"));

        var response = ApiResponse.FromResult(result);

        await Assert.That(response.StatusCode).IsEqualTo(502);
        await Assert.That(response.ToJson()).DoesNotContain("10.0.0.9");
        await Assert.That(response.Errors).IsEmpty();
    }

    [Test]
    public async Task ApiResponse_MixedErrors_InternalFailurePrevailsOverExternalService()
    {
        var result = Result.Failure(Error.ExternalService("SEFAZ", "x"), Error.Failure("DB", "y"));

        await Assert.That(ApiResponse.FromResult(result).StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Pagination_RejectsAbusiveParameters()
    {
        var source = Enumerable.Range(1, 10);

        await Assert.That(() => source.ToPagedResult(1, int.MaxValue)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => source.ToPagedResult(int.MaxValue, 1000)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => source.ToPagedResult(0, 10)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Result_IsImmutable()
    {
        var errors = new[] { Error.Failure("A", "a") };
        var result = Result.Failure(errors);

        errors[0] = Error.Failure("B", "b");

        await Assert.That(result.Error!.Code).IsEqualTo("A");
    }

    // ---------- Datas ----------

    [Test]
    public async Task BusinessDays_RejectsExcessiveRange()
    {
        var calc = new BusinessDayCalculator(new InMemoryHolidayProvider([]));

        await Assert.That(() => calc.AddBusinessDays(new DateOnly(2026, 1, 1), int.MaxValue)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new BusinessDayCalculator(new InMemoryHolidayProvider([]), [(DayOfWeek)42])).ThrowsExactly<ArgumentException>();
    }
}
