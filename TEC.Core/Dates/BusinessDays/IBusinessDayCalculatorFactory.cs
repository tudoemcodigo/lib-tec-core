using TEC.Core.Dates.Holidays;

namespace TEC.Core.Dates.BusinessDays;

/// <summary>
/// Calculadoras de dias úteis por localidade (ex.: filiais em cidades diferentes).
/// </summary>
/// <example>
/// <code>
/// public class InvoiceService(IBusinessDayCalculatorFactory calendars)
/// {
///     public DateOnly DueDate(DateOnly date, int ibgeCode) =>
///         calendars.For(HolidayLocation.FromIbgeCode(ibgeCode)).NextOrSameBusinessDay(date);
/// }
/// </code>
/// </example>
public interface IBusinessDayCalculatorFactory
{
    /// <summary>Calculadora com os feriados nacionais, da UF e do município da localidade.</summary>
    IBusinessDayCalculator For(HolidayLocation location);
}
