using TEC.Core.Common.Guards;
using TEC.Core.Dates.Holidays.Sources;

namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Calendário de feriados de várias localidades, carregado de uma ou mais fontes por <see cref="HolidayCalendarBuilder"/>.
/// Imutável e seguro para uso concorrente (registre como Singleton).
/// </summary>
/// <remarks>
/// Os feriados de todas as fontes ficam em um único índice em memória; <see cref="GetProvider"/> devolve uma visão com
/// os aplicáveis à localidade (nacionais + da UF + do município). Quando duas fontes têm feriado na mesma data para a
/// localidade, vale o da fonte registrada primeiro no builder (a descrição muda; a data continua não útil).
/// </remarks>
/// <example>
/// <code>
/// var calendar = await HolidayCalendar.CreateBuilder()
///     .AddBrazilianNational()
///     .AddJsonFile("feriados/locais.json")
///     .BuildAsync();
///
/// var saoPauloHolidays = calendar.GetProvider(new HolidayLocation("SP", 3550308));
/// </code>
/// </example>
public sealed class HolidayCalendar
{
    private readonly Dictionary<DateOnly, RankedHoliday[]> _byDate;
    private readonly RankedHoliday[] _ordered;
    private readonly BrazilianNationalHolidays? _national;
    private readonly int _nationalPriority;

    internal HolidayCalendar(IEnumerable<RankedHoliday> holidays, BrazilianNationalHolidays? national, int nationalPriority,
        IReadOnlyList<HolidaySourceFailure> failures)
    {
        // Ordem estável: por data e, na mesma data, pela prioridade (ordem de registro da fonte)
        _ordered = [.. holidays.OrderBy(h => h.Holiday.Date).ThenBy(h => h.Priority)];
        _byDate = _ordered.GroupBy(h => h.Holiday.Date).ToDictionary(g => g.Key, g => g.ToArray());
        _national = national;
        _nationalPriority = nationalPriority;
        Failures = failures;
        National = new LocationHolidayProvider(this, HolidayLocation.National);
    }

    /// <summary>Cria o builder para configurar as fontes de feriados.</summary>
    public static HolidayCalendarBuilder CreateBuilder() => new();

    /// <summary>Quantidade de feriados carregados das fontes (os nacionais calculados não entram na conta).</summary>
    public int Count => _ordered.Length;

    /// <summary>Indica se os feriados nacionais calculados (<see cref="BrazilianNationalHolidays"/>) fazem parte do calendário.</summary>
    public bool IncludesBrazilianNational => _national is not null;

    /// <summary>Fontes opcionais que falharam na carga e foram ignoradas (vazia se todas carregaram).</summary>
    public IReadOnlyList<HolidaySourceFailure> Failures { get; }

    /// <summary>Feriados nacionais (equivale a <c>GetProvider(HolidayLocation.National)</c>).</summary>
    public IHolidayProvider National { get; }

    /// <summary>
    /// Provedor com os feriados aplicáveis à localidade. Leve (não copia os dados): pode ser criado a cada requisição.
    /// </summary>
    public IHolidayProvider GetProvider(HolidayLocation location)
    {
        Guard.NotNull(location);
        return location == HolidayLocation.National ? National : new LocationHolidayProvider(this, location);
    }

    internal Holiday? Find(DateOnly date, HolidayLocation location)
    {
        Holiday? best = null;
        int bestPriority = int.MaxValue;

        if (_byDate.TryGetValue(date, out var candidates))
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Holiday.AppliesTo(location))
                {
                    (best, bestPriority) = (candidate.Holiday, candidate.Priority);
                    break;
                }
            }
        }

        if (_national is not null && _nationalPriority < bestPriority && _national.GetHoliday(date) is { } nationalHoliday)
            best = nationalHoliday;

        return best;
    }

    internal IReadOnlyList<Holiday> FindRange(DateOnly start, DateOnly end, HolidayLocation location)
    {
        if (start > end)
            (start, end) = (end, start);

        // _ordered está ordenado por data: busca binária até o início do intervalo e para no fim dele
        var byDate = new Dictionary<DateOnly, RankedHoliday>();
        for (int i = FirstIndexOnOrAfter(start); i < _ordered.Length && _ordered[i].Holiday.Date <= end; i++)
        {
            var candidate = _ordered[i];
            if (candidate.Holiday.AppliesTo(location))
                byDate.TryAdd(candidate.Holiday.Date, candidate); // _ordered já vem por prioridade dentro da data
        }

        if (_national is not null)
        {
            foreach (var holiday in _national.GetHolidays(start, end))
            {
                if (!byDate.TryGetValue(holiday.Date, out var existing) || _nationalPriority < existing.Priority)
                    byDate[holiday.Date] = new RankedHoliday(_nationalPriority, holiday);
            }
        }

        return [.. byDate.Values.Select(h => h.Holiday).OrderBy(h => h.Date)];
    }

    // Primeiro índice de _ordered com data >= date (ou _ordered.Length se não houver)
    private int FirstIndexOnOrAfter(DateOnly date)
    {
        int low = 0, high = _ordered.Length;
        while (low < high)
        {
            int mid = low + ((high - low) >> 1);
            if (_ordered[mid].Holiday.Date < date)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    /// <summary>Feriado com a prioridade da fonte (menor = registrada antes no builder).</summary>
    internal readonly record struct RankedHoliday(int Priority, Holiday Holiday);

    private sealed class LocationHolidayProvider(HolidayCalendar calendar, HolidayLocation location) : IHolidayProvider
    {
        public bool IsHoliday(DateOnly date) => calendar.Find(date, location) is not null;

        public Holiday? GetHoliday(DateOnly date) => calendar.Find(date, location);

        public IReadOnlyList<Holiday> GetHolidays(int year)
        {
            Guard.InRange(year, DateOnly.MinValue.Year, DateOnly.MaxValue.Year);
            return calendar.FindRange(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), location);
        }

        public IReadOnlyList<Holiday> GetHolidays(DateOnly start, DateOnly end) => calendar.FindRange(start, end, location);

        public override string ToString() => $"Feriados: {location}";
    }
}
