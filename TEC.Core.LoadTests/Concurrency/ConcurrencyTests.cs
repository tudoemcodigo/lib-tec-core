using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Csv;
using TEC.Core.Csv.Abstractions;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.DependencyInjection;
using TEC.Core.LoadTests.Infrastructure;
using TEC.Core.Numbers.Words;
using TEC.Core.Responses;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Generation;
using TEC.Core.Text.Validation;

namespace TEC.Core.LoadTests.Concurrency;

/// <summary>
/// Os tipos registrados como Singleton (calendário, fábrica de dias úteis, criptografia, CSV) são usados por muitas
/// threads ao mesmo tempo: cada teste compara o resultado concorrente com o resultado sequencial de referência.
/// </summary>
[Category(LoadSettings.Ci)]
public class ConcurrencyTests
{
    private const int Workers = 64;

    // Registra a primeira divergência (para a mensagem) e conta todas
    private sealed class Divergences
    {
        private readonly ConcurrentQueue<string> _samples = new();
        private int _count;

        public int Count => _count;

        public void Add(string description)
        {
            if (Interlocked.Increment(ref _count) <= 5)
                _samples.Enqueue(description);
        }

        public override string ToString() => string.Join(Environment.NewLine, _samples);
    }

    [Test]
    public async Task HolidayCalendar_ConcurrentQueries_MatchSequentialResults()
    {
        var calendar = await Calendars.SampleAsync();
        var random = new Random(1);
        var queries = Enumerable.Range(0, 2_000)
            .Select(_ => (Location: Calendars.RandomLocation(random), Date: new DateOnly(2026, 1, 1).AddDays(random.Next(730))))
            .ToArray();
        var expected = queries
            .Select(q => (Holiday: calendar.GetProvider(q.Location).GetHoliday(q.Date)?.Description,
                Year: calendar.GetProvider(q.Location).GetHolidays(q.Date.Year).Count))
            .ToArray();

        var divergences = new Divergences();
        await Parallel.ForAsync(0, 200_000, new ParallelOptions { MaxDegreeOfParallelism = Workers }, (i, _) =>
        {
            int q = i % queries.Length;
            var provider = calendar.GetProvider(queries[q].Location);
            var holiday = provider.GetHoliday(queries[q].Date)?.Description;
            int year = i % 10 == 0 ? provider.GetHolidays(queries[q].Date.Year).Count : expected[q].Year;
            if (holiday != expected[q].Holiday || year != expected[q].Year || provider.IsHoliday(queries[q].Date) != (holiday is not null))
                divergences.Add($"{queries[q].Location} {queries[q].Date}: esperado {expected[q]}, obtido ({holiday}, {year})");
            return ValueTask.CompletedTask;
        });

        await Assert.That(divergences.Count).IsEqualTo(0).Because(divergences.ToString());
    }

    [Test]
    public async Task BusinessDayFactory_ConcurrentCalculations_MatchSequentialResults()
    {
        var factory = new BusinessDayCalculatorFactory(await Calendars.SampleAsync());
        var random = new Random(2);
        var cases = Enumerable.Range(0, 2_000)
            .Select(_ => (Location: Calendars.RandomLocation(random), Date: new DateOnly(2026, 1, 1).AddDays(random.Next(600)), Days: random.Next(-60, 60)))
            .ToArray();
        var expected = cases
            .Select(c => (Due: factory.For(c.Location).AddBusinessDays(c.Date, c.Days),
                Count: factory.For(c.Location).CountBusinessDays(c.Date, c.Date.AddDays(90))))
            .ToArray();

        var divergences = new Divergences();
        await Parallel.ForAsync(0, 100_000, new ParallelOptions { MaxDegreeOfParallelism = Workers }, (i, _) =>
        {
            int c = i % cases.Length;
            var calculator = factory.For(cases[c].Location);
            var due = calculator.AddBusinessDays(cases[c].Date, cases[c].Days);
            int count = calculator.CountBusinessDays(cases[c].Date, cases[c].Date.AddDays(90));
            if (due != expected[c].Due || count != expected[c].Count)
                divergences.Add($"{cases[c]}: esperado {expected[c]}, obtido ({due}, {count})");
            return ValueTask.CompletedTask;
        });

        await Assert.That(divergences.Count).IsEqualTo(0).Because(divergences.ToString());
    }

    [Test]
    public async Task HolidayCalendarBuilder_ParallelSourceLoading_IsDeterministic()
    {
        // 40 fontes com atrasos aleatórios terminam fora de ordem; na mesma data vale sempre a registrada primeiro
        static HolidayCalendarBuilder Builder()
        {
            var builder = HolidayCalendar.CreateBuilder();
            for (int source = 0; source < 40; source++)
            {
                int id = source;
                builder.AddDelegate(async ct =>
                {
                    await Task.Delay(Random.Shared.Next(0, 20), ct);
                    return Enumerable.Range(0, 500).Select(d => new Holiday(new DateOnly(2026, 1, 1).AddDays(d % 365), $"Fonte {id}") { Uf = "SP" });
                }, $"fonte-{id}");
            }

            return builder;
        }

        var reference = (await Builder().BuildAsync()).GetProvider(new HolidayLocation("SP")).GetHolidays(2026).Select(h => h.Description).ToArray();
        var builds = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Builder().BuildAsync()));

        await Assert.That(reference.Distinct()).IsEquivalentTo(new[] { "Fonte 0" });
        foreach (var calendar in builds)
        {
            await Assert.That(calendar.Count).IsEqualTo(40 * 500);
            await Assert.That(calendar.GetProvider(new HolidayLocation("SP")).GetHolidays(2026).Select(h => h.Description)).IsEquivalentTo(reference);
        }
    }

    [Test]
    public async Task CryptographySingletons_ConcurrentRoundTrips()
    {
        await using var provider = new ServiceCollection().AddTecCore(options => options.PasswordHashIterations = 100_000).BuildServiceProvider();

        // Resolução concorrente na primeira chamada: todos recebem a mesma instância
        using var gate = new Barrier(Workers);
        var instances = await Task.WhenAll(Enumerable.Range(0, Workers).Select(_ => Task.Factory.StartNew(() =>
        {
            gate.SignalAndWait();
            return (provider.GetRequiredService<ISymmetricCryptography>(), provider.GetRequiredService<IHybridCryptography>());
        }, TaskCreationOptions.LongRunning)));
        await Assert.That(instances.Select(i => i.Item1).Distinct().Count()).IsEqualTo(1);
        await Assert.That(instances.Select(i => i.Item2).Distinct().Count()).IsEqualTo(1);

        var aes = provider.GetRequiredService<ISymmetricCryptography>();
        var hybrid = provider.GetRequiredService<IHybridCryptography>();
        var rsa = provider.GetRequiredService<IAsymmetricCryptography>();
        var keys = rsa.GenerateKeyPair();
        var aesKey = aes.GenerateKey();
        var aesKeyBase64 = Convert.ToBase64String(aesKey);

        var divergences = new Divergences();
        await Parallel.ForAsync(0, 4_000, new ParallelOptions { MaxDegreeOfParallelism = Workers }, async (i, ct) =>
        {
            var text = $"mensagem {i} " + new string('x', i % 512);
            var data = Encoding.UTF8.GetBytes(text);
            bool ok = (i % 8) switch
            {
                0 => aes.Decrypt(aes.Encrypt(text, aesKeyBase64), aesKeyBase64) == text,
                1 => aes.Decrypt(aes.Encrypt(data, aesKey, [(byte)i]), aesKey, [(byte)i]).AsSpan().SequenceEqual(data),
                2 => await StreamRoundTripAsync(aes, aesKey, data, ct),
                3 => hybrid.Decrypt(hybrid.Encrypt(text, keys.PublicKeyPem), keys.PrivateKeyPem) == text,
                4 => rsa.VerifyData(text, rsa.SignData(text, keys.PrivateKeyPem), keys.PublicKeyPem),
                5 => !rsa.VerifyData(text + "!", rsa.SignData(text, keys.PrivateKeyPem), keys.PublicKeyPem),
                6 => rsa.Decrypt(rsa.Encrypt(text[..Math.Min(text.Length, 100)], keys.PublicKeyPem), keys.PrivateKeyPem) == text[..Math.Min(text.Length, 100)],
                _ => aes.Decrypt(aes.Encrypt(data, aesKey), aesKey).AsSpan().SequenceEqual(data),
            };
            if (!ok)
                divergences.Add($"operação {i % 8} falhou na iteração {i}");
        });

        await Assert.That(divergences.Count).IsEqualTo(0).Because(divergences.ToString());
    }

    [Test]
    public async Task PasswordHasher_ConcurrentHashAndVerify()
    {
        await using var provider = new ServiceCollection().AddTecCore(options => options.PasswordHashIterations = 100_000).BuildServiceProvider();
        var hasher = provider.GetRequiredService<IPasswordHasher>();

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            var password = $"senha-{i}-ção";
            var hash = hasher.Hash(password);
            return hasher.Verify(password, hash) && !hasher.Verify(password + "x", hash) && !hasher.Verify(password, null);
        })));

        await Assert.That(results.All(ok => ok)).IsTrue();
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public DateOnly Date { get; set; }
    }

    // Tipo usado somente aqui: o mapa de colunas é criado na primeira chamada, disputada por todas as threads
    public sealed class FirstUseRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Test]
    public async Task Csv_SharedReaderAndWriter_ConcurrentRoundTrips()
    {
        await using var provider = new ServiceCollection().AddTecCore().BuildServiceProvider();
        var writer = provider.GetRequiredService<ICsvWriter>();
        var reader = provider.GetRequiredService<ICsvReader>();

        var divergences = new Divergences();
        await Parallel.ForAsync(0, 400, new ParallelOptions { MaxDegreeOfParallelism = Workers }, async (task, ct) =>
        {
            var rows = Enumerable.Range(0, 250)
                .Select(i => new Row { Id = task * 1000 + i, Text = $"=tarefa {task}; \"linha\" {i}\n fim", Value = i * 1.5m, Date = new DateOnly(2026, 1, 1).AddDays(i) })
                .ToArray();
            using var stream = new MemoryStream();
            await writer.WriteAsync(stream, rows, ct);
            stream.Position = 0;

            int index = 0;
            await foreach (var row in reader.ReadAsync<Row>(stream, ct))
            {
                var original = rows[index++];
                if (row.Id != original.Id || row.Text != original.Text || row.Value != original.Value || row.Date != original.Date)
                    divergences.Add($"tarefa {task}, linha {index}: '{row.Text}' != '{original.Text}'");
            }

            if (index != rows.Length)
                divergences.Add($"tarefa {task}: {index} linhas lidas de {rows.Length}");
        });

        await Assert.That(divergences.Count).IsEqualTo(0).Because(divergences.ToString());
    }

    [Test]
    public async Task Csv_TypeMap_FirstUseRace()
    {
        var reader = new CsvReader();
        var bytes = Encoding.UTF8.GetBytes("Id;Name\n1;Ana\n2;Bia\n");
        using var gate = new Barrier(Workers);

        var counts = await Task.WhenAll(Enumerable.Range(0, Workers).Select(_ => Task.Factory.StartNew(async () =>
        {
            gate.SignalAndWait();
            int count = 0;
            await foreach (var row in reader.ReadAsync<FirstUseRow>(new MemoryStream(bytes)))
                count += row.Name.Length;
            return count;
        }, TaskCreationOptions.LongRunning).Unwrap()));

        await Assert.That(counts.All(c => c == 6)).IsTrue();
    }

    [Test]
    public async Task StatelessHelpers_ConcurrentUse()
    {
        var divergences = new Divergences();
        await Parallel.ForAsync(0, 50_000, new ParallelOptions { MaxDegreeOfParallelism = Workers }, (i, _) =>
        {
            var cpf = DocumentGenerator.GenerateCpf();
            var cnpj = DocumentGenerator.GenerateCnpj(alphanumeric: i % 2 == 0);
            if (!DocumentValidator.IsValidCpf(DocumentFormatter.FormatCpf(cpf)) || !DocumentValidator.IsValidCnpj(DocumentFormatter.FormatCnpj(cnpj)))
                divergences.Add($"documento inválido: {cpf} / {cnpj}");

            if (NumberToWordsConverter.ToWords(i).Length == 0)
                divergences.Add($"extenso vazio para {i}");

            var json = ApiResponse<int>.Ok(i).ToJson();
            if (json.FromJson<ApiResponse<int>>()?.Data != i)
                divergences.Add($"JSON divergente para {i}");
            return ValueTask.CompletedTask;
        });

        await Assert.That(divergences.Count).IsEqualTo(0).Because(divergences.ToString());
    }

    private static async Task<bool> StreamRoundTripAsync(ISymmetricCryptography aes, byte[] key, byte[] data, CancellationToken ct)
    {
        using var encrypted = new MemoryStream();
        await aes.EncryptAsync(new MemoryStream(data), encrypted, key, ct);
        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await aes.DecryptAsync(encrypted, decrypted, key, ct);
        return decrypted.ToArray().AsSpan().SequenceEqual(data);
    }
}
