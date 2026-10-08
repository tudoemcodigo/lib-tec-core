using TEC.Core.Common.Guards;
using TEC.Core.Dates.Holidays;

namespace TEC.Core.Dates.BusinessDays;

/// <summary>
/// Cria calculadoras de dias úteis por localidade a partir de um <see cref="HolidayCalendar"/>.
/// Sem estado mutável: seguro como Singleton.
/// </summary>
/// <remarks>
/// As calculadoras são leves (não copiam os feriados) e não ficam em cache: o número de localidades vindas de
/// requisições não influencia o consumo de memória.
/// </remarks>
public sealed class BusinessDayCalculatorFactory : IBusinessDayCalculatorFactory
{
    private readonly HolidayCalendar _calendar;
    private readonly DayOfWeek[] _nonWorkingDays;

    /// <summary>Cria a fábrica.</summary>
    /// <param name="calendar">Calendário de feriados.</param>
    /// <param name="nonWorkingDays">Dias da semana não úteis. Padrão: sábado e domingo.</param>
    public BusinessDayCalculatorFactory(HolidayCalendar calendar, IEnumerable<DayOfWeek>? nonWorkingDays = null)
    {
        _calendar = Guard.NotNull(calendar);
        // Valida os dias não úteis já na criação, e não na primeira chamada de For
        _nonWorkingDays = [.. BusinessDayCalculator.CreateNonWorkingDays(nonWorkingDays)];
    }

    /// <inheritdoc />
    public IBusinessDayCalculator For(HolidayLocation location) =>
        new BusinessDayCalculator(_calendar.GetProvider(location), _nonWorkingDays);
}
