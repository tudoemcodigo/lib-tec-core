using TEC.Core.LoadGenerator;
using TEC.Core.LoadTests.Infrastructure;

namespace TEC.Core.LoadTests.Api;

/// <summary>
/// Carga HTTP na API de exemplo hospedada em processo (Kestrel real em 127.0.0.1), com o gerador TEC.Core.LoadGenerator.
/// </summary>
/// <remarks>
/// Para medir uma API publicada em outro servidor, use o gerador pela linha de comando (veja samples/README.md).
/// </remarks>
public class SampleApiLoadTests
{
    [Test]
    [Category(LoadSettings.Ci)]
    // Sozinho: os ConcurrencyTests (Parallel.ForAsync com 64 workers) saturam o thread pool do processo e, no runner de
    // 2 CPUs do CI, nenhuma requisição terminava dentro da janela medida (relatório com 0 requisições)
    [NotInParallel]
    public async Task SmokeLoad_AllScenarios_WithoutErrors()
    {
        await using var host = await SampleApiHost.StartAsync();
        using var client = host.CreateClient(16);

        var report = await LoadRunner.RunAsync(client, new LoadOptions
        {
            Concurrency = 16,
            WarmUp = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(3),
            Scenarios = SampleScenarios.Parse("feriados,vencimento,documento,protecao,csv-exportar,csv-importar,erro-validacao,senha:1"),
        });
        LoadSettings.Report("API de exemplo — fumaça (CI)", report.ToText());

        await Assert.That(report.Requests).IsGreaterThan(100);
        await Assert.That(report.Errors).IsEqualTo(0).Because(report.ToText());
        await Assert.That(report.Scenarios.All(s => s.Requests > 0)).IsTrue().Because("todos os cenários devem ser exercitados");
    }

    [Test]
    [Explicit]
    [Category(LoadSettings.Heavy)]
    [NotInParallel(LoadSettings.Heavy)]
    public async Task SustainedLoad_ErrorRateAndLatencyWithinLimits()
    {
        await using var host = await SampleApiHost.StartAsync();
        int concurrency = LoadSettings.ApiConcurrency;
        using var client = host.CreateClient(concurrency);

        long memoryBefore = MemoryProbe.RetainedBytes();
        var report = await LoadRunner.RunAsync(client, new LoadOptions
        {
            Concurrency = concurrency,
            WarmUp = TimeSpan.FromSeconds(10),
            Duration = TimeSpan.FromSeconds(LoadSettings.ApiSeconds),
            Scenarios = SampleScenarios.Parse("feriados,vencimento,documento,protecao,csv-exportar,csv-importar,erro-validacao,senha:1"),
        });
        long memoryAfter = MemoryProbe.RetainedBytes();

        LoadSettings.Report("API de exemplo — carga sustentada",
            report.ToText() + $"Memória retida (cliente + servidor): {MemoryProbe.Megabytes(memoryBefore)} → {MemoryProbe.Megabytes(memoryAfter)}{Environment.NewLine}");

        // Limites generosos: o objetivo é pegar regressões grosseiras (erros, travamentos, vazamento), não medir a máquina
        await Assert.That(report.ErrorRate).IsLessThanOrEqualTo(0.001).Because(report.ToText());
        await Assert.That(report.Latency.P99).IsLessThan(2_000);
        await Assert.That(memoryAfter - memoryBefore).IsLessThan(128L * 1024 * 1024);
    }

    [Test]
    [Explicit]
    [Category(LoadSettings.Heavy)]
    [NotInParallel(LoadSettings.Heavy)]
    public async Task PasswordScenario_CpuBoundLoad_StaysResponsive()
    {
        // PBKDF2 com as 600 mil iterações padrão: o endpoint de login é o gargalo típico de CPU
        await using var host = await SampleApiHost.StartAsync(passwordIterations: 600_000);
        using var client = host.CreateClient(Environment.ProcessorCount * 2);

        var report = await LoadRunner.RunAsync(client, new LoadOptions
        {
            Concurrency = Environment.ProcessorCount * 2,
            WarmUp = TimeSpan.FromSeconds(3),
            Duration = TimeSpan.FromSeconds(Math.Min(LoadSettings.ApiSeconds, 30)),
            Scenarios = SampleScenarios.Parse("senha:1,feriados:1"),
        });
        LoadSettings.Report("API de exemplo — login (PBKDF2 600k) disputando CPU com consultas", report.ToText());

        await Assert.That(report.Errors).IsEqualTo(0).Because(report.ToText());
        // As consultas leves continuam sendo atendidas mesmo com a CPU ocupada pelo PBKDF2
        await Assert.That(report.Scenarios.Single(s => s.Name == "feriados").Requests).IsGreaterThan(0);
    }
}
