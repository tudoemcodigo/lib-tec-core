using TEC.Core.Common.Guards;
using TEC.Core.Csv;

namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Provedor de feriados carregado a partir de arquivo(s) CSV. Veja o layout esperado em <see cref="Holiday"/>.
/// </summary>
/// <example>
/// <code>
/// var provider = await CsvHolidayProvider.FromFileAsync("feriados-2026.csv");
/// services.AddSingleton&lt;IHolidayProvider&gt;(provider);
/// </code>
/// </example>
public sealed class CsvHolidayProvider : InMemoryHolidayProvider
{
    private CsvHolidayProvider(IEnumerable<Holiday> holidays) : base(holidays)
    {
    }

    /// <summary>Carrega os feriados de um arquivo CSV.</summary>
    public static Task<CsvHolidayProvider> FromFileAsync(string filePath, CsvOptions? options = null, CancellationToken cancellationToken = default) =>
        FromFilesAsync([filePath], options, cancellationToken);

    /// <summary>Carrega e combina os feriados de vários arquivos CSV (ex.: um arquivo por ano).</summary>
    public static async Task<CsvHolidayProvider> FromFilesAsync(IEnumerable<string> filePaths, CsvOptions? options = null, CancellationToken cancellationToken = default)
    {
        var files = Guard.NotEmpty(filePaths?.ToList(), nameof(filePaths));
        if (files.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A lista de arquivos contém caminhos vazios.", nameof(filePaths));

        var reader = new CsvReader(options);
        var holidays = new List<Holiday>();

        foreach (var filePath in files)
        {
            await foreach (var holiday in reader.ReadFileAsync<Holiday>(filePath, cancellationToken).ConfigureAwait(false))
                holidays.Add(holiday);
        }

        return new CsvHolidayProvider(holidays);
    }

    /// <summary>Carrega os feriados de um stream CSV (ex.: recurso embutido ou download).</summary>
    public static async Task<CsvHolidayProvider> FromStreamAsync(Stream stream, CsvOptions? options = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream);
        var reader = new CsvReader(options);
        var holidays = new List<Holiday>();

        await foreach (var holiday in reader.ReadAsync<Holiday>(stream, cancellationToken).ConfigureAwait(false))
            holidays.Add(holiday);

        return new CsvHolidayProvider(holidays);
    }
}
