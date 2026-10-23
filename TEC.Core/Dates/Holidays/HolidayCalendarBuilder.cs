using System.Data.Common;
using System.Text.Json;
using TEC.Core.Common.Guards;
using TEC.Core.Csv;
using TEC.Core.Dates.Holidays.Sources;

namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Configura as fontes de feriados e carrega o <see cref="HolidayCalendar"/>, uma única vez, na inicialização.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>As fontes são lidas em paralelo; a ordem de registro define a prioridade quando há feriados na mesma data.</item>
///   <item>Fonte obrigatória (padrão) que falhar interrompe a carga com <see cref="HolidaySourceException"/>.</item>
///   <item>Fonte com <c>optional: true</c> que falhar é ignorada: vai para <see cref="HolidayCalendar.Failures"/> e para o
///   callback de <see cref="OnSourceError"/>.</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs
/// var calendar = await HolidayCalendar.CreateBuilder()
///     .AddBrazilianNational()
///     .AddJsonFile("feriados/locais.json")
///     .AddDatabase(() => new SqlConnection(cs), "SELECT Data, Descricao, Uf, CodigoIbge FROM Feriados")
///     .AddBrasilApi(httpClient, [2026, 2027], optional: true)
///     .OnSourceError(f => logger.LogWarning(f.Exception, "Fonte de feriados ignorada: {Source}", f.SourceName))
///     .BuildAsync();
///
/// builder.Services.AddBusinessDayCalculator(calendar, defaultLocation: new HolidayLocation("SP", 3550308));
/// </code>
/// </example>
public sealed class HolidayCalendarBuilder
{
    /// <summary>Quantidade máxima de feriados por fonte (1.000.000). Protege a memória contra resultados inesperados.</summary>
    public const int MaxHolidaysPerSource = 1_000_000;

    private readonly List<Entry> _entries = [];
    private BrazilianNationalHolidays? _national;
    private Action<HolidaySourceFailure>? _onSourceError;

    internal HolidayCalendarBuilder()
    {
    }

    /// <summary>Inclui os feriados nacionais calculados (<see cref="BrazilianNationalHolidays"/>), para qualquer ano.</summary>
    /// <param name="includeCarnival">Inclui a segunda e a terça-feira de Carnaval.</param>
    /// <param name="includeCorpusChristi">Inclui Corpus Christi.</param>
    /// <exception cref="InvalidOperationException">Chamado mais de uma vez.</exception>
    public HolidayCalendarBuilder AddBrazilianNational(bool includeCarnival = true, bool includeCorpusChristi = true)
    {
        if (_national is not null)
            throw new InvalidOperationException("Os feriados nacionais calculados já foram adicionados.");

        _national = new BrazilianNationalHolidays(includeCarnival, includeCorpusChristi);
        _entries.Add(new Entry(null, Optional: false));
        return this;
    }

    /// <summary>Inclui feriados já em memória (copiados no momento da chamada).</summary>
    public HolidayCalendarBuilder AddHolidays(IEnumerable<Holiday> holidays, string name = "Memória")
    {
        Guard.NotNull(holidays);
        Holiday[] copy = [.. holidays];
        if (copy.Any(h => h is null))
            throw new ArgumentException("A lista de feriados não pode conter itens nulos.", nameof(holidays));

        return AddSource(new DelegateHolidaySource(_ => Task.FromResult<IEnumerable<Holiday>>(copy), name));
    }

    /// <summary>Inclui um arquivo CSV (layout em <see cref="Holiday"/>).</summary>
    public HolidayCalendarBuilder AddCsvFile(string filePath, CsvOptions? options = null, bool optional = false) =>
        AddSource(new CsvHolidaySource(filePath, options), optional);

    /// <summary>Inclui um arquivo JSON (formato em <see cref="JsonHolidaySource"/>).</summary>
    public HolidayCalendarBuilder AddJsonFile(string filePath, Func<JsonElement, Holiday?>? mapper = null, bool optional = false) =>
        AddSource(new JsonHolidaySource(filePath, mapper), optional);

    /// <summary>Inclui uma consulta ADO.NET (colunas em <see cref="DbHolidaySource"/>).</summary>
    public HolidayCalendarBuilder AddDatabase(Func<DbConnection> connectionFactory, string commandText,
        Action<DbCommand>? configureCommand = null, bool optional = false) =>
        AddSource(new DbHolidaySource(connectionFactory, commandText, configureCommand), optional);

    /// <summary>Inclui uma função de carga (EF Core, Dapper, repositório…).</summary>
    public HolidayCalendarBuilder AddDelegate(Func<CancellationToken, Task<IEnumerable<Holiday>>> load, string name, bool optional = false) =>
        AddSource(new DelegateHolidaySource(load, name), optional);

    /// <summary>Inclui uma API HTTP que devolve um array JSON (formato em <see cref="JsonHolidaySource"/>).</summary>
    public HolidayCalendarBuilder AddHttp(HttpClient httpClient, Uri requestUri, Func<JsonElement, Holiday?>? mapper = null, bool optional = false) =>
        AddSource(new HttpHolidaySource(httpClient, requestUri, mapper), optional);

    /// <summary>Inclui os feriados nacionais da BrasilAPI, uma fonte por ano.</summary>
    public HolidayCalendarBuilder AddBrasilApi(HttpClient httpClient, IEnumerable<int> years, bool optional = false)
    {
        Guard.NotNull(httpClient);
        foreach (int year in Guard.NotEmpty(years?.Distinct().ToList(), nameof(years)))
            AddSource(HttpHolidaySource.BrasilApi(httpClient, year), optional);

        return this;
    }

    /// <summary>Inclui uma fonte qualquer.</summary>
    /// <param name="source">Fonte.</param>
    /// <param name="optional">Se <c>true</c>, uma falha na carga é ignorada (veja <see cref="HolidayCalendar.Failures"/>).</param>
    public HolidayCalendarBuilder AddSource(IHolidaySource source, bool optional = false)
    {
        Guard.NotNull(source);
        _entries.Add(new Entry(source, optional));
        return this;
    }

    /// <summary>Callback chamado para cada fonte opcional que falhar (ex.: registrar log).</summary>
    public HolidayCalendarBuilder OnSourceError(Action<HolidaySourceFailure> handler)
    {
        _onSourceError = Guard.NotNull(handler);
        return this;
    }

    /// <summary>Carrega todas as fontes (em paralelo) e cria o calendário.</summary>
    /// <exception cref="InvalidOperationException">Nenhuma fonte configurada.</exception>
    /// <exception cref="HolidaySourceException">Uma fonte obrigatória falhou.</exception>
    public async Task<HolidayCalendar> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (_entries.Count == 0)
            throw new InvalidOperationException("Nenhuma fonte de feriados foi configurada.");

        var results = await Task.WhenAll(_entries.Select(e => LoadAsync(e, cancellationToken))).ConfigureAwait(false);

        var holidays = new List<HolidayCalendar.RankedHoliday>();
        var failures = new List<HolidaySourceFailure>();
        int nationalPriority = int.MaxValue;

        for (int priority = 0; priority < _entries.Count; priority++)
        {
            var (entry, result) = (_entries[priority], results[priority]);
            if (entry.Source is null)
            {
                nationalPriority = priority;
                continue;
            }

            if (result.Error is not null)
            {
                if (!entry.Optional)
                    throw new HolidaySourceException(entry.Source.Name, result.Error);

                var failure = new HolidaySourceFailure(entry.Source.Name, result.Error);
                failures.Add(failure);
                _onSourceError?.Invoke(failure);
                continue;
            }

            holidays.AddRange(result.Holidays!.Select(h => new HolidayCalendar.RankedHoliday(priority, h)));
        }

        return new HolidayCalendar(holidays, _national, nationalPriority, failures.AsReadOnly());
    }

    private static async Task<LoadResult> LoadAsync(Entry entry, CancellationToken cancellationToken)
    {
        if (entry.Source is null)
            return new LoadResult([], null);

        try
        {
            var holidays = new List<Holiday>();
            await foreach (var holiday in entry.Source.LoadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (holiday is null)
                    throw new InvalidOperationException("A fonte devolveu um feriado nulo.");
                if (holidays.Count == MaxHolidaysPerSource)
                    throw new InvalidOperationException($"A fonte passou do limite de {MaxHolidaysPerSource} feriados.");

                holidays.Add(holiday);
            }

            return new LoadResult(holidays, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Falha de uma fonte não interrompe as demais: o tratamento (obrigatória/opcional) é feito em BuildAsync
            return new LoadResult(null, ex);
        }
    }

    private sealed record Entry(IHolidaySource? Source, bool Optional);

    private sealed record LoadResult(List<Holiday>? Holidays, Exception? Error);
}
