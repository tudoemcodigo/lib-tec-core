using TEC.Core.Common.Guards;

namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Provedor de feriados em memória. Imutável e seguro para uso concorrente (registre como Singleton).
/// Não filtra por localidade: todos os feriados informados valem (para várias localidades, use <see cref="HolidayCalendar"/>).
/// </summary>
public sealed class InMemoryHolidayProvider : IHolidayProvider
{
    private readonly Dictionary<DateOnly, Holiday> _holidays;
    private readonly Holiday[] _ordered;

    /// <summary>Cria o provedor a partir de uma lista de feriados. Datas duplicadas mantêm a primeira ocorrência.</summary>
    public InMemoryHolidayProvider(IEnumerable<Holiday> holidays)
    {
        Guard.NotNull(holidays);
        _holidays = [];
        foreach (var holiday in holidays)
        {
            if (holiday is null)
                throw new ArgumentException("A lista de feriados não pode conter itens nulos.", nameof(holidays));

            _holidays.TryAdd(holiday.Date, holiday);
        }

        _ordered = [.. _holidays.Values.OrderBy(h => h.Date)];
    }

    /// <summary>Quantidade de feriados carregados.</summary>
    public int Count => _ordered.Length;

    /// <inheritdoc />
    public bool IsHoliday(DateOnly date) => _holidays.ContainsKey(date);

    /// <inheritdoc />
    public Holiday? GetHoliday(DateOnly date) => _holidays.GetValueOrDefault(date);

    /// <inheritdoc />
    public IReadOnlyList<Holiday> GetHolidays(int year) =>
        GetHolidays(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));

    /// <inheritdoc />
    public IReadOnlyList<Holiday> GetHolidays(DateOnly start, DateOnly end)
    {
        if (start > end)
            (start, end) = (end, start);

        return _ordered.Where(h => h.Date >= start && h.Date <= end).ToList();
    }
}
