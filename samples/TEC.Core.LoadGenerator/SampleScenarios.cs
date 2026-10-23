using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using TEC.Core.Text.Generation;

namespace TEC.Core.LoadGenerator;

/// <summary>Cenários da API de exemplo (TEC.Core.SampleApi), com os pesos da mistura padrão.</summary>
public static class SampleScenarios
{
    // Mesmos códigos sintéticos da API de exemplo: prefixo da UF + 10 + índice do município (200 por UF por padrão)
    private static readonly int[] UfCodes = [11, 12, 13, 14, 15, 16, 17, 21, 22, 23, 24, 25, 26, 27, 28, 29, 31, 32, 33, 35, 41, 42, 43, 50, 51, 52, 53];
    private const int MunicipalitiesPerUf = 200;

    private static readonly string ImportCsv = BuildImportCsv(200);

    /// <summary>Todos os cenários. O de senha (PBKDF2, uso intenso de CPU) tem peso 0: ligue-o explicitamente.</summary>
    public static IReadOnlyList<LoadScenario> All { get; } =
    [
        new("feriados", 30, r => Get($"/feriados/{RandomIbge(r)}/2026")),
        new("vencimento", 25, r => Get(string.Create(CultureInfo.InvariantCulture,
            $"/dias-uteis/vencimento?ibge={RandomIbge(r)}&date={new DateOnly(2026, 1, 1).AddDays(r.Next(365)):yyyy-MM-dd}&term={r.Next(1, 60)}"))),
        new("documento", 20, r => Post("/documentos/validar", new { document = r.Next(2) == 0 ? DocumentGenerator.GenerateCpf(true) : DocumentGenerator.GenerateCnpj(true, alphanumeric: r.Next(2) == 0) })),
        new("protecao", 10, r => Post("/criptografia/protecao", new { text = new string('x', r.Next(64, 2048)) })),
        new("csv-exportar", 5, _ => Get("/csv/exportar?rows=500")),
        new("csv-importar", 5, _ => new HttpRequestMessage(HttpMethod.Post, "/csv/importar") { Content = new StringContent(ImportCsv, Encoding.UTF8, "text/csv") }),
        new("erro-validacao", 5, _ => Post("/documentos/validar", new { }), ExpectedStatus: 400),
        new("senha", 0, _ => Post("/senhas/verificar", new { password = "senha-errada" }), ExpectedStatus: 401),
    ];

    /// <summary>
    /// Seleciona cenários por nome, com peso opcional: <c>"feriados,documento:50,senha:5"</c>. Vazio: mistura padrão.
    /// </summary>
    public static IReadOnlyList<LoadScenario> Parse(string? selection)
    {
        if (string.IsNullOrWhiteSpace(selection))
            return All;

        var selected = new List<LoadScenario>();
        foreach (var item in selection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = item.Split(':', 2);
            var scenario = All.FirstOrDefault(s => s.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Cenário desconhecido: {parts[0]}. Disponíveis: {string.Join(", ", All.Select(s => s.Name))}.");

            int weight = parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : Math.Max(scenario.Weight, 1);
            selected.Add(scenario with { Weight = weight });
        }

        return selected;
    }

    private static int RandomIbge(Random random) =>
        UfCodes[random.Next(UfCodes.Length)] * 100_000 + 10 + random.Next(MunicipalitiesPerUf);

    private static HttpRequestMessage Get(string path) => new(HttpMethod.Get, path);

    private static HttpRequestMessage Post<T>(string path, T body) => new(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

    private static string BuildImportCsv(int rows)
    {
        var csv = new StringBuilder("Numero;Cliente;Data;Valor\n");
        for (int i = 1; i <= rows; i++)
            csv.Append(CultureInfo.InvariantCulture, $"{i};Cliente {i};{new DateOnly(2026, 1, 1).AddDays(i % 365):dd/MM/yyyy};{i},50\n");
        csv.Append("x;linha inválida;31/02/2026;abc\n");
        return csv.ToString();
    }
}
