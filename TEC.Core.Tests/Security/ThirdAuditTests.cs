using System.Globalization;
using System.Text;
using System.Text.Json;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Csv;
using TEC.Core.Csv.Attributes;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Extensions;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;
using TEC.Core.Dates.TimeZones;
using TEC.Core.Numbers.Words;
using TEC.Core.Responses.Pagination;
using TEC.Core.Tests.Compatibility;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Masking;
using TEC.Core.Text.Validation;

namespace TEC.Core.Tests.Security;

/// <summary>
/// Testes de regressão da terceira auditoria: acentos com InvariantGlobalization, ida e volta do apóstrofo anti-fórmula,
/// CSV com aspas malformadas, DateTimeKind.Unspecified em ToBrasiliaTime, vírgula no extenso, D+0 em dia não útil,
/// RsaKeyPair desserializado sem chave privada e formatação que descartava caracteres.
/// </summary>
public class ThirdAuditTests
{
    public sealed class Row
    {
        public string? Name { get; set; }
    }

    public sealed class FormulaHeaderRow
    {
        [CsvColumn("=Total")]
        public string? Total { get; set; }
    }

    private static async Task<string> WriteAsync<T>(IEnumerable<T> items, CsvOptions? options = null)
    {
        using var stream = new MemoryStream();
        await new CsvWriter(options).WriteAsync(stream, items);
        var encoding = options?.Encoding ?? Encoding.UTF8;
        return encoding.GetString(stream.ToArray()).TrimStart('﻿');
    }

    private static Task<List<T>> ReadAsync<T>(string csv, CsvOptions? options = null) where T : new() =>
        ReadAsync<T>(Encoding.UTF8.GetBytes(csv), options);

    private static Task<List<T>> ReadAsync<T>(byte[] bytes, CsvOptions? options = null) where T : new() =>
        new CsvReader(options).ReadAsync<T>(new MemoryStream(bytes)).ToListAsync().AsTask();

    // ---------- Feriados: nomes de campo acentuados (InvariantGlobalization) ----------

    // Regressão: string.Normalize(FormD) não faz nada com InvariantGlobalization e "descrição" não era reconhecido
    [Test]
    public async Task HolidayJson_AccentedKeys_ComposedAndDecomposed()
    {
        const string json = "[{ \"data\": \"2026-01-25\", \"descrição\": \"A\", \"código_ibge\": 3550308 }," +
                            " { \"data\": \"2026-01-26\", \"descrição\": \"B\" }]";

        var calendar = await HolidayCalendar.CreateBuilder()
            .AddSource(new JsonHolidaySource(() => new MemoryStream(Encoding.UTF8.GetBytes(json)), "json"))
            .BuildAsync();

        await Assert.That(calendar.Count).IsEqualTo(2);
        await Assert.That(calendar.GetProvider(new HolidayLocation("SP", 3550308)).GetHoliday(new DateOnly(2026, 1, 25))?.Description)
            .IsEqualTo("A");
        await Assert.That(calendar.National.GetHoliday(new DateOnly(2026, 1, 26))?.Description).IsEqualTo("B");
    }

    // ---------- CSV: fórmulas ----------

    // Regressão: "'=abc" era gravado sem alteração e lido como "=abc" (o apóstrofo do próprio dado se perdia)
    [Test]
    [Arguments("'=abc")]
    [Arguments("'-5")]
    [Arguments("=abc")]
    [Arguments("-5")]
    [Arguments("'texto")]
    [Arguments("''=abc")]
    [Arguments("'")]
    [Arguments("@x")]
    [Arguments("＝1")]
    public async Task Csv_FormulaGuard_RoundTripIsExact(string value)
    {
        var csv = await WriteAsync([new Row { Name = value }]);
        var rows = await ReadAsync<Row>(csv);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Name).IsEqualTo(value);
    }

    [Test]
    public async Task Csv_FormulaGuard_PrefixesValuesAlreadyStartingWithApostrophe()
    {
        var csv = await WriteAsync([new Row { Name = "'=abc" }, new Row { Name = "'texto" }, new Row { Name = "=abc" }]);

        await Assert.That(csv).IsEqualTo("Name\r\n''=abc\r\n'texto\r\n'=abc\r\n");
    }

    [Test]
    public async Task Csv_HeaderStartingWithFormulaChar_IsSanitizedAndRoundTrips()
    {
        var csv = await WriteAsync([new FormulaHeaderRow { Total = "10" }]);
        await Assert.That(csv).StartsWith("'=Total\r\n");

        var rows = await ReadAsync<FormulaHeaderRow>(csv);
        await Assert.That(rows[0].Total).IsEqualTo("10");

        // Arquivo de outro sistema, sem o apóstrofo, também é reconhecido
        var plain = await ReadAsync<FormulaHeaderRow>("=Total\n20");
        await Assert.That(plain[0].Total).IsEqualTo("20");
    }

    // ---------- CSV: aspas malformadas ----------

    // Regressão: o conteúdo depois da aspa de fechamento era concatenado ("ab"cd virava abcd)
    [Test]
    [Arguments("Name\n\"ab\"cd\n", 2, 1)]
    [Arguments("Id;Name\n1;\"a\" x\n", 2, 2)]
    [Arguments("Id;Name\n1;ok\n2;\"linha\nquebrada\"z\n", 4, 2)]
    public async Task Csv_MalformedQuotes_ThrowWithLineAndColumn(string csv, int line, int column)
    {
        var ex = await Assert.That(async () => { await ReadAsync<Row>(csv); }).ThrowsExactly<CsvException>();

        await Assert.That(ex!.LineNumber).IsEqualTo((long)line);
        await Assert.That(ex.Message).Contains($"coluna {column}");
        await Assert.That(ex.Message).DoesNotContain("cd");
    }

    [Test]
    public async Task Csv_ValidQuotes_StillAccepted()
    {
        var rows = await ReadAsync<Row>("Id;Name\n1; \"a;b\" \n2;5\" polegadas\n3;\"x\"\"y\"\n4;\"z\"");

        await Assert.That(rows.Select(r => r.Name ?? string.Empty)).IsEquivalentTo(new[] { "a;b", "5\" polegadas", "x\"y", "z" });
    }

    // ---------- CSV: opções e codificação ----------

    [Test]
    public async Task Csv_QuoteAllFields_QuotesHeaderAndValuesAndRoundTrips()
    {
        var options = new CsvOptions { QuoteAllFields = true };
        var csv = await WriteAsync([new Row { Name = "Ana" }, new Row { Name = null }], options);

        await Assert.That(csv).IsEqualTo("\"Name\"\r\n\"Ana\"\r\n\"\"\r\n");

        var rows = await ReadAsync<Row>(csv, options);
        await Assert.That(rows.Count).IsEqualTo(2);
        await Assert.That(rows[0].Name).IsEqualTo("Ana");
    }

    [Test]
    public async Task Csv_CarriageReturnOnlyLineEndings()
    {
        var rows = await ReadAsync<Row>("Name\rAna\rBia\r\"C\rD\"\r");

        await Assert.That(rows.Select(r => r.Name ?? string.Empty)).IsEquivalentTo(new[] { "Ana", "Bia", "C\rD" });
    }

    [Test]
    public async Task Csv_Utf8BomWins_OverConfiguredLatin1()
    {
        var latin1 = new CsvOptions { Encoding = Encoding.Latin1 };
        byte[] withBom = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("Name\nJoão")];

        var fromBom = await ReadAsync<Row>(withBom, latin1);
        await Assert.That(fromBom[0].Name).IsEqualTo("João");

        // Sem BOM, vale a codificação configurada
        var fromLatin1 = await ReadAsync<Row>(Encoding.Latin1.GetBytes("Name\nJoão"), latin1);
        await Assert.That(fromLatin1[0].Name).IsEqualTo("João");

        var written = await WriteAsync([new Row { Name = "Ação" }], latin1);
        await Assert.That(written).IsEqualTo("Name\r\nAção\r\n");
    }

    // ---------- Datas: fuso ----------

    // Regressão: Unspecified era tratado como UTC, e BrazilTimeZone.Now.ToBrasiliaTime() descontava 3 horas de novo
    [Test]
    public async Task ToBrasiliaTime_EachKind()
    {
        var utc = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
        await Assert.That(utc.ToBrasiliaTime()).IsEqualTo(new DateTime(2026, 9, 30, 12, 0, 0));
        await Assert.That(utc.ToBrasiliaTime().Kind).IsEqualTo(DateTimeKind.Unspecified);

        var local = utc.ToLocalTime();
        await Assert.That(local.ToBrasiliaTime()).IsEqualTo(new DateTime(2026, 9, 30, 12, 0, 0));

        var unspecified = new DateTime(2026, 9, 30, 15, 0, 0);
        var ex = await Assert.That(() => unspecified.ToBrasiliaTime()).ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.Message).Contains("Unspecified");
        await Assert.That(() => BrazilTimeZone.Now.ToBrasiliaTime()).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ToBrasiliaTime_ExplicitInterpretationOfUnspecified()
    {
        var unspecified = new DateTime(2026, 9, 30, 15, 0, 0);
        await Assert.That(unspecified.ToBrasiliaTime(DateTimeKind.Utc)).IsEqualTo(new DateTime(2026, 9, 30, 12, 0, 0));

        var asLocal = DateTime.SpecifyKind(unspecified, DateTimeKind.Local).ToBrasiliaTime();
        await Assert.That(unspecified.ToBrasiliaTime(DateTimeKind.Local)).IsEqualTo(asLocal);

        // O Kind do próprio valor prevalece
        var utc = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
        await Assert.That(utc.ToBrasiliaTime(DateTimeKind.Local)).IsEqualTo(new DateTime(2026, 9, 30, 12, 0, 0));

        await Assert.That(() => unspecified.ToBrasiliaTime(DateTimeKind.Unspecified)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => unspecified.ToBrasiliaTime((DateTimeKind)42)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task BrazilTimeZone_Now_IsWallClockConvertibleToUtc()
    {
        var now = BrazilTimeZone.Now;
        await Assert.That(now.Kind).IsEqualTo(DateTimeKind.Unspecified);

        var difference = (now.FromBrasiliaTimeToUtc() - DateTime.UtcNow).Duration();
        await Assert.That(difference).IsLessThan(TimeSpan.FromMinutes(1));
        await Assert.That(DateOnly.FromDateTime(now)).IsEqualTo(BrazilTimeZone.Today);
    }

    // Regressão: sem data de referência, CalculateAge usava DateTime.Today (data do servidor)
    [Test]
    public async Task CalculateAge_DefaultsToBrasiliaToday()
    {
        var today = BrazilTimeZone.Today;

        await Assert.That(today.CalculateAge()).IsEqualTo(0);
        await Assert.That(today.AddYears(-30).CalculateAge()).IsEqualTo(30);
        await Assert.That(today.AddYears(-30).AddDays(1).CalculateAge()).IsEqualTo(29);
        await Assert.That(() => today.AddDays(1).CalculateAge()).ThrowsExactly<ArgumentException>();
    }

    // ---------- Datas: dias úteis e feriados ----------

    // Regressão: AddBusinessDays(d, 0) devolvia a própria data mesmo em dia não útil
    [Test]
    public async Task AddBusinessDays_Zero_OnNonBusinessDay_ReturnsNextBusinessDay()
    {
        var calc = new BusinessDayCalculator(new BrazilianNationalHolidays());

        await Assert.That(calc.AddBusinessDays(new DateOnly(2026, 10, 10), 0)).IsEqualTo(new DateOnly(2026, 10, 13)); // sábado + 12/10
        await Assert.That(calc.AddBusinessDays(new DateOnly(2026, 10, 9), 0)).IsEqualTo(new DateOnly(2026, 10, 9));
        await Assert.That(calc.AddBusinessDays(new DateTime(2026, 10, 11, 8, 30, 0), 0)).IsEqualTo(new DateTime(2026, 10, 13, 8, 30, 0));
    }

    [Test]
    public async Task BusinessDays_YearTurn()
    {
        var calc = new BusinessDayCalculator(new BrazilianNationalHolidays());
        var newYearsEve = new DateOnly(2026, 12, 31); // quinta-feira; 01/01/2027 é sexta (feriado)

        await Assert.That(calc.NextBusinessDay(newYearsEve)).IsEqualTo(new DateOnly(2027, 1, 4));
        await Assert.That(calc.AddBusinessDays(newYearsEve, 1)).IsEqualTo(new DateOnly(2027, 1, 4));
        await Assert.That(calc.AddBusinessDays(new DateOnly(2027, 1, 4), -1)).IsEqualTo(newYearsEve);
        await Assert.That(calc.PreviousBusinessDay(new DateOnly(2027, 1, 1))).IsEqualTo(newYearsEve);
        await Assert.That(calc.CountBusinessDays(newYearsEve, new DateOnly(2027, 1, 4))).IsEqualTo(2);
    }

    [Test]
    [Arguments(2024, "2024-02-12", "2024-02-13", "2024-05-30")]
    [Arguments(2025, "2025-03-03", "2025-03-04", "2025-06-19")]
    [Arguments(2026, "2026-02-16", "2026-02-17", "2026-06-04")]
    [Arguments(2027, "2027-02-08", "2027-02-09", "2027-05-27")]
    [Arguments(2038, "2038-03-08", "2038-03-09", "2038-06-24")]
    public async Task CarnivalAndCorpusChristi(int year, string carnivalMonday, string carnivalTuesday, string corpusChristi)
    {
        var national = new BrazilianNationalHolidays();
        var holidays = national.GetHolidays(year);

        string[] carnival = [.. holidays.Where(h => h.Description == "Carnaval").Select(h => h.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))];
        await Assert.That(carnival).IsEquivalentTo(new[] { carnivalMonday, carnivalTuesday });

        var corpus = DateOnly.ParseExact(corpusChristi, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        await Assert.That(national.GetHoliday(corpus)?.Description).IsEqualTo("Corpus Christi");
        await Assert.That(corpus.DayOfWeek).IsEqualTo(DayOfWeek.Thursday);
    }

    // ---------- Números por extenso ----------

    [Test]
    [Arguments(1_001_100L, "um milhão, mil e cem")]
    [Arguments(1_001_001L, "um milhão, mil e um")]
    [Arguments(1_100_001L, "um milhão, cem mil e um")]
    [Arguments(1_000_100L, "um milhão e cem")]
    [Arguments(1_234_567L, "um milhão, duzentos e trinta e quatro mil quinhentos e sessenta e sete")]
    [Arguments(2_001_000_000L, "dois bilhões e um milhão")]
    [Arguments(1_001_000_001L, "um bilhão, um milhão e um")]
    [Arguments(101_000L, "cento e um mil")]
    public async Task ToWords_CommaBetweenNonFinalClasses(long value, string expected) =>
        await Assert.That(NumberToWordsConverter.ToWords(value)).IsEqualTo(expected);

    [Test]
    public async Task ToWords_Limits()
    {
        const string max = "novecentos e noventa e nove trilhões, novecentos e noventa e nove bilhões, " +
                           "novecentos e noventa e nove milhões, novecentos e noventa e nove mil novecentos e noventa e nove";

        await Assert.That(NumberToWordsConverter.ToWords(NumberToWordsConverter.MaxValue)).IsEqualTo(max);
        await Assert.That(NumberToWordsConverter.ToWords(-NumberToWordsConverter.MaxValue)).IsEqualTo("menos " + max);
        await Assert.That(() => NumberToWordsConverter.ToWords(NumberToWordsConverter.MaxValue + 1)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => NumberToWordsConverter.ToWords(long.MinValue)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments("0.005", "um centavo")]
    [Arguments("-0.005", "menos um centavo")]
    [Arguments("0.004", "zero real")]
    [Arguments("-0.004", "zero real")]
    [Arguments("-2", "menos dois reais")]
    [Arguments("-1", "menos um real")]
    [Arguments("1000000.50", "um milhão de reais e cinquenta centavos")]
    [Arguments("-1001100.5", "menos um milhão, mil e cem reais e cinquenta centavos")]
    [Arguments("2001000000", "dois bilhões e um milhão de reais")]
    public async Task ToCurrencyWords_RoundingNegativesAndClasses(string value, string expected) =>
        await Assert.That(NumberToWordsConverter.ToCurrencyWords(decimal.Parse(value, CultureInfo.InvariantCulture))).IsEqualTo(expected);

    [Test]
    public async Task ToCurrencyWords_Limits()
    {
        await Assert.That(NumberToWordsConverter.ToCurrencyWords(-NumberToWordsConverter.MaxValue)).StartsWith("menos novecentos");
        await Assert.That(() => NumberToWordsConverter.ToCurrencyWords(NumberToWordsConverter.MaxValue + 0.995m)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    // ---------- RsaKeyPair ----------

    // Regressão: o JSON (sem a chave privada) era desserializado com PrivateKeyPem nulo
    [Test]
    public async Task RsaKeyPair_DeserializationFailsWithClearMessage()
    {
        var keys = new RsaKeyPair("PUBLIC", "PRIVATE-SECRET");
        var json = keys.ToJson();

        await Assert.That(json).IsEqualTo("{\"publicKeyPem\":\"PUBLIC\"}");

        var ex = await Assert.That(() => json.FromJson<RsaKeyPair>()).Throws<JsonException>();
        await Assert.That(ex!.Message).Contains("PrivateKeyPem");
        await Assert.That(json.TryFromJson<RsaKeyPair>(out var value)).IsFalse();
        await Assert.That(value).IsNull();

        // Mesmo com a chave privada no JSON, o par não é reconstruído
        await Assert.That(() => "{\"publicKeyPem\":\"a\",\"privateKeyPem\":\"b\"}".FromJson<RsaKeyPair>()).Throws<JsonException>();
    }

    [Test]
    public async Task RsaKeyPair_SourceGeneratedContext()
    {
        var keys = new RsaKeyPair("PUBLIC", "PRIVATE-SECRET");
        var json = keys.ToJson(CompatJsonContext.Default);

        await Assert.That(json).DoesNotContain("PRIVATE");
        await Assert.That(() => json.FromJson<RsaKeyPair>(CompatJsonContext.Default)).Throws<JsonException>();
        await Assert.That(json.TryFromJson<RsaKeyPair>(CompatJsonContext.Default, out _)).IsFalse();
    }

    [Test]
    public async Task RsaKeyPair_RequiresBothKeys()
    {
        await Assert.That(() => new RsaKeyPair("PUBLIC", null!)).Throws<ArgumentException>();
        await Assert.That(() => new RsaKeyPair(" ", "PRIVATE")).Throws<ArgumentException>();

        var keys = new RsaKeyPair("PUBLIC", "PRIVATE");
        await Assert.That(() => keys with { PrivateKeyPem = "" }).Throws<ArgumentException>();
        await Assert.That((keys with { PublicKeyPem = "OUTRA" }).PrivateKeyPem).IsEqualTo("PRIVATE");
    }

    // ---------- Texto ----------

    // Regressão: letras e caracteres estranhos eram descartados e o restante formatado ("abc12345678901" virava CPF)
    [Test]
    public async Task Formatters_DoNotDiscardUnexpectedCharacters()
    {
        await Assert.That(DocumentFormatter.FormatCpf("abc12345678901")).IsEqualTo("abc12345678901");
        await Assert.That(DocumentFormatter.FormatCpf("529.982.247-25")).IsEqualTo("529.982.247-25");
        await Assert.That(DocumentFormatter.FormatCpf("529 982 247 25")).IsEqualTo("529.982.247-25");
        await Assert.That(DocumentFormatter.FormatCpf("529982247x25")).IsEqualTo("529982247x25");
        await Assert.That(DocumentFormatter.FormatCpfOrCnpj("abc12345678")).IsEqualTo("abc12345678");
        await Assert.That(DocumentFormatter.FormatCnpj("12.abc.345/01de-35")).IsEqualTo("12.ABC.345/01DE-35");
        await Assert.That(DocumentFormatter.FormatCnpj("12#abc34501de35")).IsEqualTo("12#abc34501de35");
        await Assert.That(DocumentFormatter.FormatCep("01310-100x")).IsEqualTo("01310-100x");
        await Assert.That(DocumentFormatter.FormatPis("123.45678.91-9")).IsEqualTo("123.45678.91-9");
        await Assert.That(DocumentFormatter.FormatPhone("+55 (11) 98765-4321")).IsEqualTo("+55 (11) 98765-4321");
        await Assert.That(DocumentFormatter.FormatPhone("11 98765-4321 ramal")).IsEqualTo("11 98765-4321 ramal");
        await Assert.That(DocumentFormatter.FormatCpf(null)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task MaskFormatter_ValueLongerThanMask_IsReturnedUnchanged()
    {
        await Assert.That(MaskFormatter.Apply("1234567890", "#####-###")).IsEqualTo("1234567890");
        await Assert.That(MaskFormatter.Apply("12345678", "#####-###")).IsEqualTo("12345-678");
        await Assert.That(MaskFormatter.Apply("123", "#####-###")).IsEqualTo("123");
    }

    [Test]
    [Arguments("01310-100", true)]
    [Arguments("01310100", true)]
    [Arguments("01.310-100", true)]
    [Arguments("00000000", false)]
    [Arguments("0131010", false)]
    [Arguments("013101000", false)]
    [Arguments("01310-100a", false)]
    [Arguments("01310\n100", false)]
    [Arguments("", false)]
    [Arguments(null, false)]
    public async Task IsValidCep(string? cep, bool expected) =>
        await Assert.That(DocumentValidator.IsValidCep(cep)).IsEqualTo(expected);

    [Test]
    public async Task RemoveMask_KeepsOnlyLettersAndDigitsUppercase()
    {
        await Assert.That(DocumentFormatter.RemoveMask("123.456.789-09")).IsEqualTo("12345678909");
        await Assert.That(DocumentFormatter.RemoveMask("12.abc.345/01de-35")).IsEqualTo("12ABC34501DE35");
        await Assert.That(DocumentFormatter.RemoveMask("(11) 98765-4321")).IsEqualTo("11987654321");
        await Assert.That(DocumentFormatter.RemoveMask(null)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task MaskPhone_WithCountryCode()
    {
        await Assert.That(SensitiveDataMasker.MaskPhone("+55 (11) 98765-4321")).IsEqualTo("+55 (11) *****-4321");
        await Assert.That(SensitiveDataMasker.MaskPhone("5511987654321")).IsEqualTo("+55 (11) *****-4321");
        await Assert.That(SensitiveDataMasker.MaskPhone("551133334444")).IsEqualTo("+55 (11) ****-4444");
        await Assert.That(SensitiveDataMasker.MaskPhone("4411987654321")).DoesNotContain("98765");
    }

    // ---------- Paginação ----------

    [Test]
    public async Task CalculateSkip_HugePage_DoesNotOverflow()
    {
        await Assert.That(PaginationExtensions.CalculateSkip(int.MaxValue, 1)).IsEqualTo(int.MaxValue - 1);
        await Assert.That(() => PaginationExtensions.CalculateSkip(int.MaxValue, 10)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => PaginationExtensions.CalculateSkip(int.MaxValue, 1000)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ToPagedResult_Queryable_PageBeyondTotal()
    {
        var query = Enumerable.Range(1, 25).AsQueryable();

        var last = query.ToPagedResult(page: 3, pageSize: 10);
        await Assert.That(last.Items.Count).IsEqualTo(5);

        var beyond = query.ToPagedResult(page: 4, pageSize: 10);
        await Assert.That(beyond.Items).IsEmpty();
        await Assert.That(beyond.TotalItems).IsEqualTo(25L);
        await Assert.That(beyond.Pagination.TotalPages).IsEqualTo(3);
        await Assert.That(beyond.Pagination.HasNextPage).IsFalse();

        var huge = query.ToPagedResult(page: int.MaxValue, pageSize: 1);
        await Assert.That(huge.Items).IsEmpty();
        await Assert.That(huge.Page).IsEqualTo(int.MaxValue);
    }

    // ---------- JSON: [Flags] ----------

    [Flags]
    public enum Access { None = 0, Read = 1, Write = 2 }

    [Test]
    public async Task Json_FlagsEnumConverter_RejectsUndefinedBits()
    {
        var options = JsonDefaults.CreateOptions(typeInfoResolver: null);
        options.Converters.Add(JsonDefaults.CreateEnumConverter<Access>());
        options.TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();

        await Assert.That(JsonSerializer.Serialize(Access.Read | Access.Write, options)).IsEqualTo("\"read, write\"");
        await Assert.That(JsonSerializer.Deserialize<Access>("\"read, write\"", options)).IsEqualTo(Access.Read | Access.Write);

        await Assert.That(() => JsonSerializer.Deserialize<Access>("\"read, 4\"", options)).Throws<JsonException>();
        await Assert.That(() => JsonSerializer.Deserialize<Access>("\"4\"", options)).Throws<JsonException>();
        await Assert.That(() => JsonSerializer.Deserialize<Access>("5", options)).Throws<JsonException>();
        await Assert.That(() => JsonSerializer.Serialize((Access)4, options)).Throws<JsonException>();
        await Assert.That(() => JsonSerializer.Serialize(Access.Read | (Access)8, options)).Throws<JsonException>();
    }
}
