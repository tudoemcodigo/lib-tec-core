using System.Globalization;

namespace TEC.Core.LoadTests.Infrastructure;

/// <summary>
/// Categorias e parâmetros dos testes de carga. Os volumes e durações dos testes pesados podem ser ajustados por
/// variáveis de ambiente, sem recompilar (ex.: TEC_CARGA_SOAK_SEGUNDOS=600 para um soak de 10 minutos).
/// </summary>
public static class LoadSettings
{
    /// <summary>Testes rápidos que rodam no CI a cada push.</summary>
    public const string Ci = "Carga-CI";

    /// <summary>
    /// Testes pesados: [Explicit], só rodam quando selecionados por filtro. Também é a chave de [NotInParallel] deles:
    /// rodam um de cada vez, porque medem memória e vazão do processo inteiro.
    /// </summary>
    public const string Heavy = "Carga-Pesada";

    /// <summary>Linhas do CSV no teste de volume (padrão 2 milhões).</summary>
    public static int CsvRows => GetInt("TEC_CARGA_CSV_LINHAS", 2_000_000);

    /// <summary>Megabytes cifrados e decifrados no teste de volume do AES-GCM em stream (padrão 1024).</summary>
    public static int AesMegabytes => GetInt("TEC_CARGA_AES_MB", 1024);

    /// <summary>Duração do soak em processo (padrão 120 s).</summary>
    public static int SoakSeconds => GetInt("TEC_CARGA_SOAK_SEGUNDOS", 120);

    /// <summary>Duração da carga pesada na API de exemplo (padrão 60 s).</summary>
    public static int ApiSeconds => GetInt("TEC_CARGA_API_SEGUNDOS", 60);

    /// <summary>Requisições simultâneas na carga pesada da API (padrão 64).</summary>
    public static int ApiConcurrency => GetInt("TEC_CARGA_API_CONCORRENCIA", 64);

    /// <summary>
    /// Pasta onde os testes gravam os relatórios em Markdown (o workflow de performance publica no resumo do CI).
    /// Sem a variável, os relatórios vão só para a saída do teste.
    /// </summary>
    public static string? ReportDirectory => Environment.GetEnvironmentVariable("TEC_CARGA_RELATORIOS");

    /// <summary>Escreve o relatório na saída do teste e, se configurado, em arquivo.</summary>
    public static void Report(string title, string body)
    {
        Console.WriteLine($"## {title}{Environment.NewLine}{body}");
        if (ReportDirectory is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "carga.md"), $"### {title}{Environment.NewLine}{Environment.NewLine}{body}{Environment.NewLine}");
        }
    }

    private static int GetInt(string name, int defaultValue) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : defaultValue;
}
