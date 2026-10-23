using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Csv.Abstractions;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.DependencyInjection;
using TEC.Core.LoadTests.Infrastructure;
using TEC.Core.Responses;
using TEC.Core.Text.Extensions;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Generation;
using TEC.Core.Text.Masking;
using TEC.Core.Text.Validation;

namespace TEC.Core.LoadTests.Soak;

/// <summary>
/// Soak: carga mista contínua por minutos, verificando que memória, handles e vazão ficam estáveis (sem vazamentos
/// nem degradação). Duração em TEC_CARGA_SOAK_SEGUNDOS (padrão 120 s).
/// </summary>
[Explicit]
[Category(LoadSettings.Heavy)]
[NotInParallel(LoadSettings.Heavy)]
public class SoakTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    private sealed record Sample(double Seconds, long Operations, long RetainedBytes, int Handles);

    [Test]
    public async Task MixedWorkload_MemoryHandlesAndThroughputStayStable()
    {
        var duration = TimeSpan.FromSeconds(LoadSettings.SoakSeconds);
        var interval = TimeSpan.FromSeconds(Math.Clamp(LoadSettings.SoakSeconds / 24.0, 2, 30));

        await using var services = new ServiceCollection().AddTecCore(options => options.PasswordHashIterations = 100_000).BuildServiceProvider();
        var aes = services.GetRequiredService<ISymmetricCryptography>();
        var hybrid = services.GetRequiredService<IHybridCryptography>();
        var rsa = services.GetRequiredService<IAsymmetricCryptography>();
        var hasher = services.GetRequiredService<IPasswordHasher>();
        var csvWriter = services.GetRequiredService<ICsvWriter>();
        var csvReader = services.GetRequiredService<ICsvReader>();
        var factory = new BusinessDayCalculatorFactory(await Calendars.SampleAsync());
        var keys = rsa.GenerateKeyPair();
        var aesKey = aes.GenerateKey();
        var passwordHash = hasher.Hash("senha");
        var rows = Enumerable.Range(1, 500).Select(i => new Row { Id = i, Name = $"=Nome {i}", Value = i * 0.5m }).ToArray();

        long operations = 0;
        using var stop = new CancellationTokenSource(duration);

        async Task WorkerAsync(int id)
        {
            var random = new Random(id);
            var payload = new byte[4096];
            while (!stop.IsCancellationRequested)
            {
                switch (random.Next(100))
                {
                    case < 25:
                        var calculator = factory.For(Calendars.RandomLocation(random));
                        _ = calculator.AddBusinessDays(new DateOnly(2026, 1, 1).AddDays(random.Next(600)), random.Next(1, 40));
                        break;
                    case < 45:
                        var cpf = DocumentGenerator.GenerateCpf();
                        _ = DocumentValidator.IsValidCpf(cpf) && SensitiveDataMasker.MaskCpf(DocumentFormatter.FormatCpf(cpf)).Length > 0;
                        _ = $"Ação Número {random.Next()}".ToSlug();
                        break;
                    case < 65:
                        random.NextBytes(payload);
                        _ = aes.Decrypt(aes.Encrypt(payload, aesKey), aesKey);
                        _ = HashHelper.ComputeHmac(payload, aesKey);
                        break;
                    case < 80:
                        using (var stream = new MemoryStream())
                        {
                            await csvWriter.WriteAsync(stream, rows);
                            stream.Position = 0;
                            await foreach (var _ in csvReader.ReadAsync<Row>(stream)) { }
                        }
                        break;
                    case < 95:
                        _ = ApiResponse<Row>.Ok(rows[random.Next(rows.Length)]).ToJson().FromJson<ApiResponse<Row>>();
                        break;
                    case < 99:
                        _ = hybrid.Decrypt(hybrid.Encrypt(payload, keys.PublicKeyPem), keys.PrivateKeyPem);
                        break;
                    default:
                        _ = hasher.Verify("senha", passwordHash);
                        break;
                }

                Interlocked.Increment(ref operations);
            }
        }

        var watch = Stopwatch.StartNew();
        var workers = Enumerable.Range(0, Environment.ProcessorCount).Select(i => Task.Run(() => WorkerAsync(i))).ToArray();
        var samples = new List<Sample>();
        while (!stop.IsCancellationRequested)
        {
            try { await Task.Delay(interval, stop.Token); }
            catch (OperationCanceledException) { break; }
            samples.Add(new Sample(watch.Elapsed.TotalSeconds, Interlocked.Read(ref operations), MemoryProbe.RetainedBytes(), MemoryProbe.HandleCount()));
        }
        await Task.WhenAll(workers);

        await Assert.That(samples.Count).IsGreaterThanOrEqualTo(8).Because("o soak precisa de amostras suficientes (aumente TEC_CARGA_SOAK_SEGUNDOS)");

        // Vazão de cada intervalo entre amostras
        var rates = samples.Select((s, i) => i == 0
            ? s.Operations / s.Seconds
            : (s.Operations - samples[i - 1].Operations) / (s.Seconds - samples[i - 1].Seconds)).ToArray();

        // Descarta o primeiro quarto (aquecimento: JIT, pools e caches) e compara o início com o fim da janela estável
        int skip = samples.Count / 4;
        int third = Math.Max((samples.Count - skip) / 3, 1);
        var first = samples.Skip(skip).Take(third).ToList();
        var last = samples.TakeLast(third).ToList();
        double firstThroughput = rates.Skip(skip).Take(third).Average();
        double lastThroughput = rates.TakeLast(third).Average();
        long firstMemory = Median(first.Select(s => s.RetainedBytes));
        long lastMemory = Median(last.Select(s => s.RetainedBytes));

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"Duração: {duration.TotalSeconds:F0} s · workers: {Environment.ProcessorCount} · operações: {operations:N0}");
        report.AppendLine("| t (s) | operações | memória retida | handles |");
        report.AppendLine("|---:|---:|---:|---:|");
        foreach (var s in samples)
            report.AppendLine(CultureInfo.InvariantCulture, $"| {s.Seconds:F0} | {s.Operations:N0} | {MemoryProbe.Megabytes(s.RetainedBytes)} | {s.Handles} |");
        report.AppendLine(CultureInfo.InvariantCulture, $"Vazão: {firstThroughput:N0} → {lastThroughput:N0} op/s · memória (mediana): {MemoryProbe.Megabytes(firstMemory)} → {MemoryProbe.Megabytes(lastMemory)}");
        LoadSettings.Report("Soak em processo (carga mista)", report.ToString());

        await Assert.That(lastMemory - firstMemory).IsLessThan(Math.Max(16L * 1024 * 1024, firstMemory / 5)).Because(report.ToString());
        await Assert.That(last[^1].Handles - first[0].Handles).IsLessThan(200).Because(report.ToString());
        await Assert.That(lastThroughput).IsGreaterThan(firstThroughput * 0.6).Because(report.ToString());
    }

    private static long Median(IEnumerable<long> values)
    {
        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }
}
