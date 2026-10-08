using System.Diagnostics;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using TEC.Core.Cryptography.Symmetric;
using TEC.Core.Csv;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;
using TEC.Core.LoadTests.Infrastructure;
using TEC.Core.SampleApi;

namespace TEC.Core.LoadTests.Volume;

/// <summary>Grandes volumes: streaming com memória constante e comportamento nos limites de segurança.</summary>
[Explicit]
[Category(LoadSettings.Heavy)]
[NotInParallel(LoadSettings.Heavy)]
public class VolumeTests
{
    public sealed class Order
    {
        public long Id { get; set; }
        public string Customer { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public decimal Value { get; set; }
        public string? Notes { get; set; }
    }

    [Test]
    public async Task Csv_MillionsOfRows_StreamWithConstantMemory()
    {
        int rows = LoadSettings.CsvRows;
        int sampleEvery = Math.Max(rows / 10, 1);
        var path = Path.Combine(Path.GetTempPath(), $"tec-core-carga-{Guid.NewGuid():N}.csv");
        long baseline = MemoryProbe.RetainedBytes();
        long peakWrite = 0, peakRead = 0;

        try
        {
            var watch = Stopwatch.StartNew();
            await new CsvWriter().WriteFileAsync(path, Generate());
            var writeTime = watch.Elapsed;

            watch.Restart();
            long count = 0, idSum = 0;
            await foreach (var order in new CsvReader().ReadFileAsync<Order>(path))
            {
                count++;
                idSum += order.Id;
                if (count % sampleEvery == 0)
                    peakRead = Math.Max(peakRead, MemoryProbe.RetainedBytes() - baseline);
            }
            var readTime = watch.Elapsed;

            long fileSize = new FileInfo(path).Length;
            LoadSettings.Report("CSV em streaming", string.Create(CultureInfo.InvariantCulture, $"""
                Linhas: {rows:N0} · arquivo: {MemoryProbe.Megabytes(fileSize)}
                Escrita: {writeTime.TotalSeconds:F1} s ({rows / writeTime.TotalSeconds:N0} linhas/s) · pico de memória retida: {MemoryProbe.Megabytes(peakWrite)}
                Leitura: {readTime.TotalSeconds:F1} s ({rows / readTime.TotalSeconds:N0} linhas/s) · pico de memória retida: {MemoryProbe.Megabytes(peakRead)}

                """));

            await Assert.That(count).IsEqualTo(rows);
            await Assert.That(idSum).IsEqualTo((long)rows * (rows + 1) / 2);
            // Memória retida independente do tamanho do arquivo (buffers e mapa de tipos apenas)
            await Assert.That(peakWrite).IsLessThan(32L * 1024 * 1024);
            await Assert.That(peakRead).IsLessThan(32L * 1024 * 1024);
        }
        finally
        {
            File.Delete(path);
        }

        async IAsyncEnumerable<Order> Generate()
        {
            for (long i = 1; i <= rows; i++)
            {
                yield return new Order
                {
                    Id = i,
                    Customer = $"Cliente {i % 10_000}",
                    Date = new DateOnly(2026, 1, 1).AddDays((int)(i % 365)),
                    Value = i % 100_000 / 100m,
                    Notes = i % 7 == 0 ? $"Observação; com \"aspas\" e separador {i}" : null
                };

                if (i % sampleEvery == 0)
                {
                    peakWrite = Math.Max(peakWrite, MemoryProbe.RetainedBytes() - baseline);
                    await Task.Yield();
                }
            }
        }
    }

    [Test]
    public async Task AesGcm_StreamGigabytes_RoundTripWithConstantMemory()
    {
        long length = LoadSettings.AesMegabytes * 1024L * 1024L;
        var aes = new AesGcmCryptography();
        var key = aes.GenerateKey();
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024, resumeWriterThreshold: 2 * 1024 * 1024));
        long baseline = MemoryProbe.RetainedBytes();

        using var source = new GeneratedStream(length);
        using var sink = new HashingSinkStream();
        var watch = Stopwatch.StartNew();

        // Cifra e decifra ao mesmo tempo, ligados por um pipe: nada do conteúdo fica inteiro em memória ou em disco
        var encrypt = Task.Run(async () =>
        {
            await using var writer = pipe.Writer.AsStream();
            await aes.EncryptAsync(source, writer, key);
        });
        var decrypt = Task.Run(async () =>
        {
            await using var reader = pipe.Reader.AsStream();
            await aes.DecryptAsync(reader, sink, key);
        });

        long peak = 0;
        while (!Task.WhenAll(encrypt, decrypt).IsCompleted)
        {
            await Task.Delay(500);
            peak = Math.Max(peak, GC.GetTotalMemory(false) - baseline);
        }
        await Task.WhenAll(encrypt, decrypt);
        watch.Stop();

        LoadSettings.Report("AES-GCM em stream", string.Create(CultureInfo.InvariantCulture,
            $"Volume: {MemoryProbe.Megabytes(length)} · tempo: {watch.Elapsed.TotalSeconds:F1} s ({length / 1048576.0 / watch.Elapsed.TotalSeconds:F0} MB/s cifrando e decifrando) · pico de heap: {MemoryProbe.Megabytes(peak)}{Environment.NewLine}"));

        await Assert.That(sink.BytesWritten).IsEqualTo(length);
        await Assert.That(sink.GetHash().AsSpan().SequenceEqual(source.GetHash())).IsTrue();
        // Heap total (inclui lixo ainda não coletado): limitado aos buffers dos blocos e do pipe
        await Assert.That(peak).IsLessThan(256L * 1024 * 1024);
    }

    [Test]
    public async Task HolidaySource_AtMaximumSize_LoadsQueriesAndRejectsOverflow()
    {
        int max = HolidayCalendarBuilder.MaxHolidaysPerSource;
        var path = Path.Combine(Path.GetTempPath(), $"tec-core-feriados-{Guid.NewGuid():N}.csv");
        try
        {
            await WriteHolidayCsvAsync(path, max);
            var watch = Stopwatch.StartNew();
            var calendar = await HolidayCalendar.CreateBuilder().AddBrazilianNational().AddCsvFile(path).BuildAsync();
            var loadTime = watch.Elapsed;
            await Assert.That(calendar.Count).IsEqualTo(max);

            // Consultas num calendário de 1 milhão de feriados. O índice é por data: o custo de cada consulta cresce com
            // a quantidade de feriados (de todas as localidades) no período, então o limite é de segurança, não de uso típico
            const int rangeQueries = 1_000, dayQueries = 10_000;
            var random = new Random(3);
            HolidayLocation RandomLocation() =>
                HolidayLocation.FromIbgeCode(SampleData.IbgeCode(SampleData.UfCodes[random.Next(27)], random.Next(5_000)));

            watch.Restart();
            int found = 0;
            for (int i = 0; i < rangeQueries; i++)
            {
                var start = new DateOnly(2026, 1, 1).AddDays(random.Next(330));
                found += calendar.GetProvider(RandomLocation()).GetHolidays(start, start.AddDays(30)).Count;
            }
            var rangeTime = watch.Elapsed;

            watch.Restart();
            for (int i = 0; i < dayQueries; i++)
                found += calendar.GetProvider(RandomLocation()).IsHoliday(new DateOnly(2026, 1, 1).AddDays(random.Next(365))) ? 1 : 0;
            var dayTime = watch.Elapsed;

            LoadSettings.Report("Fonte de feriados no limite", string.Create(CultureInfo.InvariantCulture, $"""
                Feriados: {max:N0} · carga: {loadTime.TotalSeconds:F1} s
                Período de 30 dias: {rangeTime.TotalMilliseconds * 1000 / rangeQueries:N0} µs por consulta ({rangeQueries:N0} consultas)
                IsHoliday: {dayTime.TotalMilliseconds * 1000 / dayQueries:N0} µs por consulta ({dayQueries:N0} consultas) · feriados encontrados: {found:N0}

                """));
            await Assert.That(loadTime).IsLessThan(TimeSpan.FromSeconds(60));
            await Assert.That(rangeTime).IsLessThan(TimeSpan.FromSeconds(60));
            await Assert.That(dayTime).IsLessThan(TimeSpan.FromSeconds(60));

            // Uma linha acima do limite: a carga é recusada (proteção de memória)
            await File.AppendAllTextAsync(path, "01/01/2026;Excedente;;\n");
            var ex = await Assert.That(async () => { await HolidayCalendar.CreateBuilder().AddCsvFile(path).BuildAsync(); })
                .ThrowsExactly<HolidaySourceException>();
            await Assert.That(ex!.InnerException).IsTypeOf<InvalidOperationException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task BusinessDays_MaximumRanges_CompleteQuickly()
    {
        var factory = new BusinessDayCalculatorFactory(await Calendars.SampleAsync());
        var watch = Stopwatch.StartNew();
        long total = 0;
        foreach (int uf in SampleData.UfCodes)
        {
            var calculator = factory.For(HolidayLocation.FromIbgeCode(SampleData.IbgeCode(uf, 0)));
            var start = new DateOnly(2000, 1, 1);
            total += calculator.CountBusinessDays(start, start.AddDays(BusinessDayCalculator.MaxRangeDays));
            total += calculator.GetBusinessDays(start, start.AddDays(BusinessDayCalculator.MaxRangeDays)).Count;
            total += calculator.AddBusinessDays(start, BusinessDayCalculator.MaxBusinessDaysToAdd).DayNumber;
            total += calculator.AddBusinessDays(new DateOnly(2090, 1, 1), -BusinessDayCalculator.MaxBusinessDaysToAdd).DayNumber;
        }
        watch.Stop();

        LoadSettings.Report("Dias úteis nos limites", string.Create(CultureInfo.InvariantCulture,
            $"27 localidades × (contagem + lista de {BusinessDayCalculator.MaxRangeDays:N0} dias + 2 somas de {BusinessDayCalculator.MaxBusinessDaysToAdd:N0} dias úteis): {watch.Elapsed.TotalMilliseconds:F0} ms{Environment.NewLine}"));
        await Assert.That(total).IsGreaterThan(0);
        await Assert.That(watch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));
    }

    // 27 UFs × 5.000 municípios, datas espalhadas pelo ano
    private static async Task WriteHolidayCsvAsync(string path, int count)
    {
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 16);
        await writer.WriteAsync("Data;Descricao;Uf;CodigoIbge\n");
        var line = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            int uf = SampleData.UfCodes[i % 27];
            int municipality = i / 27 % 5_000;
            var date = new DateOnly(2026, 1, 1).AddDays((int)(i * 7919L % 365));
            line.Clear().Append(CultureInfo.InvariantCulture, $"{date:dd/MM/yyyy};Feriado {i};;{SampleData.IbgeCode(uf, municipality)}\n");
            await writer.WriteAsync(line);
        }
    }
}
