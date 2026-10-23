using System.Runtime.CompilerServices;
using TEC.Core.Common.Guards;
using TEC.Core.Csv;

namespace TEC.Core.Dates.Holidays.Sources;

/// <summary>
/// Feriados em CSV (arquivo ou stream). Veja o layout esperado em <see cref="Holiday"/>.
/// </summary>
/// <remarks>
/// As colunas <c>Data</c> e <c>Descricao</c> são obrigatórias em toda linha: data ou descrição vazia (ou coluna ausente no
/// cabeçalho) gera <see cref="CsvException"/> com a linha, como um valor inválido.
/// </remarks>
/// <example>
/// <code>
/// var calendar = await HolidayCalendar.CreateBuilder()
///     .AddBrazilianNational()
///     .AddSource(new CsvHolidaySource("feriados/municipais.csv"))
///     .BuildAsync();
/// </code>
/// </example>
public sealed class CsvHolidaySource : IHolidaySource
{
    private readonly string? _filePath;
    private readonly Func<Stream>? _openStream;
    private readonly CsvOptions? _options;

    /// <summary>Lê de um arquivo. O caminho não deve vir do usuário final (risco de <i>path traversal</i>).</summary>
    public CsvHolidaySource(string filePath, CsvOptions? options = null)
    {
        _filePath = Guard.NotNullOrWhiteSpace(filePath);
        _options = options;
        Name = filePath;
    }

    /// <summary>Lê de um stream aberto a cada carga (ex.: recurso embutido). O stream é descartado ao final da leitura.</summary>
    /// <param name="openStream">Abre o stream.</param>
    /// <param name="name">Nome da fonte, usado em mensagens de erro.</param>
    /// <param name="options">Opções de CSV (padrão: <see cref="CsvOptions.Default"/>).</param>
    public CsvHolidaySource(Func<Stream> openStream, string name, CsvOptions? options = null)
    {
        _openStream = Guard.NotNull(openStream);
        Name = Guard.NotNullOrWhiteSpace(name);
        _options = options;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public async IAsyncEnumerable<Holiday> LoadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reader = new CsvReader(_options);

        if (_filePath is not null)
        {
            if (!File.Exists(_filePath))
                throw new FileNotFoundException("Arquivo de feriados não encontrado.", _filePath);

            await foreach (var holiday in reader.ReadFileValidatedAsync<Holiday>(_filePath, Validate, cancellationToken).ConfigureAwait(false))
                yield return holiday;
            yield break;
        }

        var stream = _openStream!() ?? throw new InvalidOperationException("A função que abre o stream de feriados retornou null.");
        await using (stream.ConfigureAwait(false))
        {
            await foreach (var holiday in reader.ReadValidatedAsync<Holiday>(stream, Validate, cancellationToken).ConfigureAwait(false))
                yield return holiday;
        }
    }

    // Mesma regra das fontes JSON e de banco: data e descrição obrigatórias. Sem isso, campo vazio (ou coluna ausente no
    // cabeçalho) virava feriado em 01/01/0001 e o dia real continuava útil, sem erro
    private static (string Message, string ColumnName)? Validate(Holiday holiday)
    {
        if (holiday.Date == default)
            return ("Feriado sem data (campo vazio ou coluna ausente)", "Data");

        if (string.IsNullOrWhiteSpace(holiday.Description))
            return ("Feriado sem descrição (campo vazio ou coluna ausente)", "Descricao");

        return null;
    }
}
