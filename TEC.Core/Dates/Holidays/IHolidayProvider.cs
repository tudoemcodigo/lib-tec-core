namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Fonte de feriados utilizada no cálculo de dias úteis.
/// </summary>
public interface IHolidayProvider
{
    /// <summary>Indica se a data é feriado.</summary>
    bool IsHoliday(DateOnly date);

    /// <summary>Retorna o feriado da data, ou <c>null</c> se não houver.</summary>
    Holiday? GetHoliday(DateOnly date);

    /// <summary>Lista os feriados do ano, em ordem cronológica.</summary>
    IReadOnlyList<Holiday> GetHolidays(int year);

    /// <summary>Lista os feriados do período (inclusive), em ordem cronológica.</summary>
    IReadOnlyList<Holiday> GetHolidays(DateOnly start, DateOnly end);
}
