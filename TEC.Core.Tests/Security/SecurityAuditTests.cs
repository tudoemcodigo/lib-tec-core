using System.Text;
using System.Text.Json;
using TEC.Core.Common.Results;
using TEC.Core.Common.Serialization;
using TEC.Core.Csv;
using TEC.Core.Csv.Attributes;
using TEC.Core.Cryptography.Symmetric;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Numbers.Extensions;
using TEC.Core.Text.Extensions;
using TEC.Core.Text.Masking;

namespace TEC.Core.Tests.Security;

/// <summary>
/// Testes de regressão da segunda auditoria de segurança: enums fora do domínio no JSON, limites e fórmulas do CSV,
/// mascaramento com caracteres de controle, texto UTF-16 inválido, intervalos de dias úteis e formato do AES-GCM.
/// </summary>
public class SecurityAuditTests
{
    public enum Status { Active = 1, Blocked = 2 }

    [Flags]
    public enum Access { None = 0, Read = 1, Write = 2 }

    public sealed record Dto(Status Status);

    public sealed record FlagsDto(Access Access);

    // ---------- JSON: enums ----------

    [Test]
    [Arguments("\"999\"")]      // número entre aspas (aceito pelo conversor do .NET 8)
    [Arguments("\"1\"")]
    [Arguments("\"-1\"")]
    [Arguments("\"active, blocked\"")]   // combinação em enum sem [Flags]: viraria 3, fora do enum
    [Arguments("\"\"")]
    [Arguments("999")]
    [Arguments("\"inexistente\"")]
    public async Task Json_RejectsEnumOutsideTheDefinedMembers(string json)
    {
        await Assert.That(() => $"{{\"status\":{json}}}".FromJson<Dto>()).Throws<JsonException>();
        await Assert.That(() => JsonSerializer.Deserialize<Status>(json, AotOptions())).Throws<JsonException>();
    }

    [Test]
    public async Task Json_EnumNames_RoundTrip()
    {
        await Assert.That("{\"status\":\"blocked\"}".FromJson<Dto>()!.Status).IsEqualTo(Status.Blocked);
        await Assert.That("{\"status\":\"Blocked\"}".FromJson<Dto>()!.Status).IsEqualTo(Status.Blocked);
        await Assert.That(new Dto(Status.Active).ToJson()).IsEqualTo("{\"status\":\"active\"}");
        await Assert.That(JsonSerializer.Deserialize<Status>("\"blocked\"", AotOptions())).IsEqualTo(Status.Blocked);
    }

    [Test]
    public async Task Json_FlagsEnum_AcceptsOnlyDefinedNames()
    {
        await Assert.That("{\"access\":\"read, write\"}".FromJson<FlagsDto>()!.Access).IsEqualTo(Access.Read | Access.Write);
        await Assert.That(() => "{\"access\":\"read, 4\"}".FromJson<FlagsDto>()).Throws<JsonException>();
        await Assert.That(() => "{\"access\":\"7\"}".FromJson<FlagsDto>()).Throws<JsonException>();
    }

    [Test]
    public async Task Json_EnumAsDictionaryKey_RoundTripsAndRejectsNumbers()
    {
        var json = new Dictionary<Status, int> { [Status.Blocked] = 2 }.ToJson();

        await Assert.That(json).IsEqualTo("{\"blocked\":2}");
        await Assert.That(json.FromJson<Dictionary<Status, int>>()![Status.Blocked]).IsEqualTo(2);
        await Assert.That(() => "{\"999\":2}".FromJson<Dictionary<Status, int>>()).Throws<JsonException>();
    }

    [Test]
    public async Task Json_InvalidEnum_DoesNotEchoTheValue()
    {
        var ex = await Assert.That(() => JsonSerializer.Deserialize<Status>("\"valor-do-cliente\"", AotOptions())).Throws<JsonException>();

        await Assert.That(ex!.Message).DoesNotContain("valor-do-cliente");
    }

    private static JsonSerializerOptions AotOptions()
    {
        var options = JsonDefaults.CreateOptions(typeInfoResolver: null);
        options.Converters.Add(JsonDefaults.CreateEnumConverter<Status>());
        options.TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
        return options;
    }

    // ---------- CSV ----------

    public class BaseRow
    {
        public int Id { get; set; }

        public virtual string Name { get; set; } = string.Empty;
    }

    public sealed class DerivedRow : BaseRow
    {
        public string Extra { get; set; } = string.Empty;

        public override string Name { get; set; } = string.Empty;

        [CsvColumn("Primeiro", Order = 0)]
        public string First { get; set; } = string.Empty;
    }

    public sealed class LinkRow
    {
        public Uri? Link { get; set; }

        public char Mark { get; set; }

        public int Number { get; set; }
    }

    public sealed class RateRow
    {
        public double Rate { get; set; }

        public float Ratio { get; set; }
    }

    // A ordem não depende de PropertyInfo.MetadataToken (que lança em Native AOT): classe base primeiro, na ordem da declaração
    [Test]
    public async Task Csv_ColumnOrder_FollowsDeclarationAcrossInheritance()
    {
        var csv = await WriteAsync([new DerivedRow { Id = 1, Name = "Ana", Extra = "x", First = "p" }]);

        await Assert.That(csv).IsEqualTo("Primeiro;Id;Name;Extra\r\np;1;Ana;x\r\n");
    }

    [Test]
    public async Task Csv_SanitizesFormulas_InNonStringTextValues()
    {
        LinkRow[] rows = [new() { Link = new Uri("=cmd|' /C calc'!A0", UriKind.Relative), Mark = '=', Number = -5 }];

        var csv = await WriteAsync(rows);
        var back = await ReadAsync<LinkRow>(csv);

        await Assert.That(csv).Contains("'=cmd");
        await Assert.That(csv).Contains(";'=;");
        await Assert.That(csv).Contains(";-5");   // números negativos não são alterados
        await Assert.That(back[0].Link!.OriginalString).IsEqualTo(rows[0].Link!.OriginalString);
        await Assert.That(back[0].Mark).IsEqualTo('=');
    }

    [Test]
    public async Task Csv_MaxColumns_CountsTheLastField()
    {
        var options = new CsvOptions { MaxColumns = 3, HasHeader = false };

        await Assert.That((await ReadAsync<LinkRow>("a;b;7", options)).Count).IsEqualTo(1);
        await Assert.That(async () => { await ReadAsync<LinkRow>("a;b;7;8", options); }).ThrowsExactly<CsvException>();
        await Assert.That(async () => { await ReadAsync<LinkRow>("a;b;7;8\n", options); }).ThrowsExactly<CsvException>();
    }

    [Test]
    [Arguments("NaN;1")]
    [Arguments("1E999;1")]
    [Arguments("1;NaN")]
    [Arguments("1;1E99")]
    public async Task Csv_RejectsNonFiniteNumbers(string line)
    {
        await Assert.That(async () => { await ReadAsync<RateRow>("Rate;Ratio\n" + line); }).ThrowsExactly<CsvException>();
    }

    private static async Task<string> WriteAsync<T>(IEnumerable<T> items)
    {
        using var stream = new MemoryStream();
        await new CsvWriter(new CsvOptions { Encoding = new UTF8Encoding(false) }).WriteAsync(stream, items);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static Task<List<T>> ReadAsync<T>(string csv, CsvOptions? options = null) where T : new() =>
        new CsvReader(options).ReadAsync<T>(new MemoryStream(Encoding.UTF8.GetBytes(csv))).ToListAsync().AsTask();

    // ---------- Texto ----------

    [Test]
    public async Task MaskEmail_WithControlCharacters_MasksEverything()
    {
        var masked = SensitiveDataMasker.MaskEmail("joao@x.com\r\n2026 INFO login ok admin");

        await Assert.That(masked.Any(char.IsControl)).IsFalse();
        await Assert.That(masked).DoesNotContain("admin");
        await Assert.That(masked.All(c => c == '*')).IsTrue();
    }

    [Test]
    public async Task RemoveAccents_WithLoneSurrogate_DoesNotThrow()
    {
        await Assert.That("a\uD800ção".RemoveAccents()).IsEqualTo("a\uD800cao");
        await Assert.That("a\uD800b".ToSlug()).IsNotNull();
        await Assert.That("a\uD800b".EqualsIgnoreCaseAndAccents("A\uD800B")).IsTrue();
    }

    // ---------- Números ----------

    [Test]
    [Arguments("12R$34,00")]
    [Arguments("R$R$ 1")]
    [Arguments("1,00-")]
    [Arguments("(1,00)")]
    [Arguments("R$")]
    [Arguments("-")]
    public async Task TryParseBrazilianDecimal_RejectsMalformedText(string text)
    {
        await Assert.That(text.TryParseBrazilianDecimal(out _)).IsFalse();
    }

    [Test]
    public async Task TryParseBrazilianDecimal_AcceptsSignAndSymbolPrefix()
    {
        await Assert.That("R$ 1.234,56".TryParseBrazilianDecimal(out var plain) && plain == 1234.56m).IsTrue();
        await Assert.That("-R$ 1.234,56".TryParseBrazilianDecimal(out var negative) && negative == -1234.56m).IsTrue();
        await Assert.That("- 10".TryParseBrazilianDecimal(out var spaced) && spaced == -10m).IsTrue();
        await Assert.That("+7,5".TryParseBrazilianDecimal(out var positive) && positive == 7.5m).IsTrue();
    }

    [Test]
    public async Task ToCurrency_Double_AtDecimalLimit_ThrowsArgumentOutOfRange()
    {
        await Assert.That(() => ((double)decimal.MaxValue).ToCurrency()).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => ((double)decimal.MinValue).ToCurrency()).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    // ---------- Dias úteis ----------

    [Test]
    public async Task BusinessDays_RejectsUnboundedCountAndListRanges()
    {
        var calculator = new BusinessDayCalculator(new InMemoryHolidayProvider([]));
        var start = new DateOnly(2000, 1, 1);

        await Assert.That(() => calculator.CountBusinessDays(DateOnly.MinValue, DateOnly.MaxValue)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => calculator.GetBusinessDays(DateOnly.MaxValue, DateOnly.MinValue)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(calculator.CountBusinessDays(start, start.AddDays(BusinessDayCalculator.MaxRangeDays))).IsGreaterThan(0);
    }

    // ---------- Criptografia ----------

    // O byte de versão é autenticado: trocá-lo por uma versão "válida" de outro formato não passa na verificação
    [Test]
    public async Task AesGcm_VersionByte_IsAuthenticated()
    {
        var aes = new AesGcmCryptography();
        var key = aes.GenerateKey();
        var encrypted = aes.Encrypt("dado"u8.ToArray(), key);

        // Reproduz o formato sem a versão no dado associado (como uma implementação que ignorasse o byte)
        var nonce = encrypted.AsSpan(1, 12);
        var tag = encrypted.AsSpan(13, 16);
        var plain = new byte[encrypted.Length - 29];
        using var raw = new System.Security.Cryptography.AesGcm(key, 16);

        await Assert.That(Throws(() => raw.Decrypt(encrypted.AsSpan(1, 12), encrypted.AsSpan(29), encrypted.AsSpan(13, 16), plain))).IsTrue();
        await Assert.That(Throws(() => raw.Decrypt(encrypted.AsSpan(1, 12), encrypted.AsSpan(29), encrypted.AsSpan(13, 16), plain, [AesGcmCryptography.FormatVersion]))).IsFalse();
        await Assert.That(aes.Decrypt(encrypted, key)).IsEquivalentTo("dado"u8.ToArray());
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return true;
        }
    }

    // ---------- Result ----------

    [Test]
    public async Task Result_ToFailure_PropagatesErrors_AndRejectsSuccess()
    {
        Result<int> failure = Error.NotFound("X", "não encontrado");

        await Assert.That(failure.ToFailure<string>().Errors).IsEquivalentTo(failure.Errors);
        await Assert.That(failure.ToFailure().IsFailure).IsTrue();
        await Assert.That(() => Result.Success(1).ToFailure<string>()).ThrowsExactly<InvalidOperationException>();
    }
}
