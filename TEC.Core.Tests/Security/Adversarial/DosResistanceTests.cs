using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Symmetric;
using TEC.Core.Csv;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;
using TEC.Core.Numbers.Extensions;
using TEC.Core.Text.Extensions;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Masking;
using TEC.Core.Text.Validation;

namespace TEC.Core.Tests.Security.Adversarial;

/// <summary>
/// Negação de serviço: entradas adversariais (streams sem fim, campos gigantes, aninhamento profundo, expressões que
/// provocam backtracking, calendários impossíveis) precisam falhar rápido, com leitura e memória limitadas.
/// </summary>
/// <remarks>
/// Os limites de tempo são folgados (máquinas de CI lentas e testes em paralelo): pegam laços sem fim e crescimento
/// quadrático/exponencial, não pequenas regressões de desempenho (essas ficam com o TEC.Core.Benchmarks).
/// </remarks>
[NotInParallel(TimingKey)]
public class DosResistanceTests
{
    /// <summary>Chave compartilhada com os testes de tempo constante: não rodam ao mesmo tempo.</summary>
    public const string TimingKey = "seguranca-tempo";

    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(5);

    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // ---------- CSV: o leitor para sozinho, sem consumir o stream inteiro ----------

    [Test]
    [Arguments("campo-sem-fim", "Id;Name\n1;", "a")]
    [Arguments("aspas-sem-fim", "Id;Name\n1;\"", "\"\"")]
    [Arguments("colunas-sem-fim", "Id;Name\n1;", ";")]
    [Arguments("cabecalho-sem-fim", "", "Coluna;")]
    [Arguments("espacos-sem-fim", "Id;Name\n1;", " ")]
    public async Task Csv_EndlessInput_StopsAtConfiguredLimit(string scenario, string prefix, string pattern)
    {
        var options = new CsvOptions();
        long hardLimit = options.MaxRecordLength * 4L;
        var stream = new EndlessStream(Encoding.UTF8.GetBytes(prefix), Encoding.UTF8.GetBytes(pattern), hardLimit);

        var watch = Stopwatch.StartNew();
        await Assert.That(async () => { await new CsvReader(options).ReadAsync<Row>(stream).ToListAsync(); })
            .ThrowsExactly<CsvException>().Because(scenario);

        await Assert.That(stream.HitHardLimit).IsFalse().Because($"{scenario}: o leitor deveria parar em MaxRecordLength/MaxFieldLength/MaxColumns");
        await Assert.That(stream.BytesRead).IsLessThanOrEqualTo(options.MaxRecordLength + 2L * options.BufferSize);
        await Assert.That(watch.Elapsed).IsLessThan(Fast);
    }

    [Test]
    public async Task Csv_EndlessValidRows_WithSkipInvalidRows_KeepsStreamingWithBoundedMemory()
    {
        // Linhas válidas sem fim não são ataque (é streaming legítimo): o consumidor decide quando parar
        var stream = new EndlessStream("Id;Name\n"u8.ToArray(), "1;abc\n2;x\n"u8.ToArray(), long.MaxValue);
        long before = GC.GetTotalMemory(true);
        int count = 0;
        await foreach (var _ in new CsvReader(new CsvOptions { SkipInvalidRows = true }).ReadAsync<Row>(stream))
        {
            if (++count == 500_000)
                break;
        }

        await Assert.That(GC.GetTotalMemory(true) - before).IsLessThan(16L * 1024 * 1024);
    }

    // ---------- JSON ----------

    [Test]
    public async Task HolidayJson_EndlessArray_StopsAtMaxJsonBytes()
    {
        var item = """{"data":"2026-01-01","descricao":"Feriado"},"""u8.ToArray();
        long hardLimit = JsonHolidaySource.MaxJsonBytes * 2L;
        var stream = new EndlessStream("["u8.ToArray(), item, hardLimit);

        var watch = Stopwatch.StartNew();
        var ex = await Assert.That(async () =>
        {
            await HolidayCalendar.CreateBuilder().AddSource(new JsonHolidaySource(() => stream, "json-sem-fim")).BuildAsync();
        }).ThrowsExactly<HolidaySourceException>();

        await Assert.That(ex!.InnerException).IsTypeOf<InvalidDataException>();
        await Assert.That(stream.HitHardLimit).IsFalse();
        await Assert.That(stream.BytesRead).IsLessThanOrEqualTo(JsonHolidaySource.MaxJsonBytes + 1024L * 1024);
        await Assert.That(watch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));
    }

    [Test]
    [Arguments(100)]
    [Arguments(100_000)]
    public async Task Json_DeepNesting_IsRejectedQuickly(int depth)
    {
        var json = new string('[', depth) + new string(']', depth);
        var nestedHolidays = "[" + new string('[', depth) + new string(']', depth) + "]";

        var watch = Stopwatch.StartNew();
        await Assert.That(() => json.FromJson<object>()).ThrowsExactly<JsonException>();
        await Assert.That(json.TryFromJson<object>(out _)).IsFalse();
        await Assert.That(async () =>
        {
            await HolidayCalendar.CreateBuilder().AddSource(new JsonHolidaySource(() => new MemoryStream(Encoding.UTF8.GetBytes(nestedHolidays)), "json")).BuildAsync();
        }).ThrowsExactly<HolidaySourceException>();
        await Assert.That(watch.Elapsed).IsLessThan(Fast);
    }

    // ---------- AES-GCM em stream: cabeçalhos forjados não provocam alocação gigante ----------

    [Test]
    [Arguments(int.MaxValue)]
    [Arguments(-1)]
    [Arguments(64 * 1024 + 1)]
    public async Task AesStream_ForgedChunkLength_RejectedWithoutHugeAllocation(int forgedLength)
    {
        var aes = new AesGcmCryptography();
        var key = aes.GenerateKey();
        using var encrypted = new MemoryStream();
        await aes.EncryptAsync(new MemoryStream(new byte[100]), encrypted, key);
        var bytes = encrypted.ToArray();

        // Formato: [versão][salt 32] e, por bloco, [final][tamanho int32 big-endian][tag 16][dados]
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(1 + 32 + 1, 4), forgedLength);

        long allocated = GC.GetAllocatedBytesForCurrentThread();
        await Assert.That(async () => await aes.DecryptAsync(new MemoryStream(bytes), Stream.Null, key)).Throws<CryptographicException>();
        await Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated).IsLessThan(4L * 1024 * 1024);
    }

    [Test]
    public async Task AesStream_EndlessGarbage_FailsOnFirstChunk()
    {
        var aes = new AesGcmCryptography();
        var stream = new EndlessStream([1], [0x55], 1024L * 1024 * 1024);

        await Assert.That(async () => await aes.DecryptAsync(stream, Stream.Null, aes.GenerateKey())).Throws<CryptographicException>();
        await Assert.That(stream.BytesRead).IsLessThan(256L * 1024);
    }

    // ---------- Expressões regulares e texto ----------

    [Test]
    public async Task EmailRegex_WorstCaseInputs_NoCatastrophicBacktracking()
    {
        string[] attacks =
        [
            "a@" + string.Concat(Enumerable.Repeat("a-", 125)) + "!",
            "a@" + string.Concat(Enumerable.Repeat("a.", 125)) + "-",
            new string('a', 64) + "@" + new string('a', 63) + "." + new string('a', 63) + "." + new string('a', 60) + "-",
            string.Concat(Enumerable.Repeat("a.", 31)) + "a@" + string.Concat(Enumerable.Repeat("b-b.", 40)) + "!",
            "@" + new string('a', 252) + "@",
        ];

        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 2_000; i++)
        {
            foreach (var attack in attacks)
                _ = DocumentValidator.IsValidEmail(attack);
        }

        await Assert.That(watch.Elapsed).IsLessThan(Fast).Because("10 mil validações de entradas de pior caso devem levar milissegundos");
    }

    [Test]
    public async Task HugeStrings_AreHandledInLinearTime()
    {
        const string Piece = "Ação 123.456-7 😀 ";
        const int Repeats = 600_000; // ~10 MB
        const int Factor = 16;

        // Compara a entrada cheia com uma 16x menor, em vez de um limite absoluto: no CI (2 CPUs, cobertura, assemblies
        // em paralelo) o tempo varia 10x de uma máquina para outra, mas a razão não. Linear dá ~16x; quadrático, ~256x
        _ = ProcessHugeString(string.Concat(Enumerable.Repeat(Piece, 1_000))); // aquecimento (JIT, regex)
        var small = ProcessHugeString(string.Concat(Enumerable.Repeat(Piece, Repeats / Factor)));
        var huge = ProcessHugeString(string.Concat(Enumerable.Repeat(Piece, Repeats)));

        await Assert.That(small.AllRejected && huge.AllRejected).IsTrue();
        double ratio = huge.Elapsed.TotalMilliseconds / Math.Max(small.Elapsed.TotalMilliseconds, 1);
        await Assert.That(ratio).IsLessThan(Factor * 4.0)
            .Because($"{Factor}x mais texto levou {ratio:F1}x mais tempo ({small.Elapsed.TotalMilliseconds:F0} ms → {huge.Elapsed.TotalMilliseconds:F0} ms)");
    }

    private static (bool AllRejected, TimeSpan Elapsed) ProcessHugeString(string text)
    {
        var watch = Stopwatch.StartNew();

        bool allRejected = !DocumentValidator.IsValidCpf(text)
            & !DocumentValidator.IsValidCnpj(text)
            & !DocumentValidator.IsValidCpfOrCnpj(text)
            & !DocumentValidator.IsValidEmail(text)
            & !DocumentValidator.IsValidPhone(text)
            & !text.TryParseBrazilianDecimal(out _)
            & !text.TryFromBase64(out _);
        _ = DocumentFormatter.FormatCpf(text) + DocumentFormatter.FormatPhone(text) + SensitiveDataMasker.MaskEmail(text) + SensitiveDataMasker.Mask(text, 3, 3);
        _ = text.RemoveAccents().Length + text.ToSlug().Length + text.CollapseWhitespace().Length + text.ToSnakeCase().Length;

        return (allRejected, watch.Elapsed);
    }

    // ---------- Dias úteis: calendário sem nenhum dia útil ----------

    private sealed class EveryDayIsHoliday : IHolidayProvider
    {
        public bool IsHoliday(DateOnly date) => true;
        public Holiday? GetHoliday(DateOnly date) => new(date, "Feriado");
        public IReadOnlyList<Holiday> GetHolidays(int year) => [];
        public IReadOnlyList<Holiday> GetHolidays(DateOnly start, DateOnly end) => [];
    }

    [Test]
    public async Task BusinessDays_ImpossibleCalendar_FailsInsteadOfLoopingForever()
    {
        var calculator = new BusinessDayCalculator(new EveryDayIsHoliday());
        var date = new DateOnly(2026, 1, 1);
        var watch = Stopwatch.StartNew();

        await Assert.That(() => calculator.NextBusinessDay(date)).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => calculator.PreviousOrSameBusinessDay(date)).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => calculator.AddBusinessDays(date, BusinessDayCalculator.MaxBusinessDaysToAdd)).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => calculator.GetFirstBusinessDayOfMonth(2026, 1)).Throws<Exception>();
        await Assert.That(calculator.CountBusinessDays(date, date.AddDays(BusinessDayCalculator.MaxRangeDays))).IsEqualTo(0);
        await Assert.That(watch.Elapsed).IsLessThan(Fast);
    }
}
