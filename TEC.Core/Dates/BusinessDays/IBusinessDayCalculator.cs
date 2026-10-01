namespace TEC.Core.Dates.BusinessDays;

/// <summary>
/// Cálculos de dias úteis considerando fins de semana e feriados.
/// As sobrecargas com <see cref="DateTime"/> preservam o horário e o <see cref="DateTimeKind"/>.
/// </summary>
/// <remarks>
/// <see cref="DateTime"/> com <see cref="DateTimeKind.Utc"/> é avaliado na data de Brasília (ex.: sábado 02:00 UTC é
/// sexta-feira 23:00 em Brasília, portanto dia útil); o resultado preserva o horário de Brasília e volta em UTC.
/// <see cref="DateTimeKind.Local"/> e <see cref="DateTimeKind.Unspecified"/> usam a data como está.
/// </remarks>
public interface IBusinessDayCalculator
{
    /// <summary>Indica se a data é dia útil.</summary>
    bool IsBusinessDay(DateOnly date);

    /// <inheritdoc cref="IsBusinessDay(DateOnly)"/>
    bool IsBusinessDay(DateTime date);

    /// <summary>Próximo dia útil estritamente posterior à data.</summary>
    DateOnly NextBusinessDay(DateOnly date);

    /// <inheritdoc cref="NextBusinessDay(DateOnly)"/>
    DateTime NextBusinessDay(DateTime date);

    /// <summary>Dia útil anterior, estritamente anterior à data.</summary>
    DateOnly PreviousBusinessDay(DateOnly date);

    /// <inheritdoc cref="PreviousBusinessDay(DateOnly)"/>
    DateTime PreviousBusinessDay(DateTime date);

    /// <summary>Retorna a própria data se for dia útil; caso contrário, o próximo dia útil (ex.: vencimentos que caem em feriado).</summary>
    DateOnly NextOrSameBusinessDay(DateOnly date);

    /// <inheritdoc cref="NextOrSameBusinessDay(DateOnly)"/>
    DateTime NextOrSameBusinessDay(DateTime date);

    /// <summary>Retorna a própria data se for dia útil; caso contrário, o dia útil anterior.</summary>
    DateOnly PreviousOrSameBusinessDay(DateOnly date);

    /// <inheritdoc cref="PreviousOrSameBusinessDay(DateOnly)"/>
    DateTime PreviousOrSameBusinessDay(DateTime date);

    /// <summary>
    /// Soma (ou subtrai, se negativo) dias úteis. Com <paramref name="businessDays"/> = 0, retorna a própria data.
    /// </summary>
    DateOnly AddBusinessDays(DateOnly date, int businessDays);

    /// <inheritdoc cref="AddBusinessDays(DateOnly, int)"/>
    DateTime AddBusinessDays(DateTime date, int businessDays);

    /// <summary>
    /// Conta os dias úteis no período, incluindo as duas datas. A ordem das datas não importa.
    /// </summary>
    int CountBusinessDays(DateOnly start, DateOnly end);

    /// <inheritdoc cref="CountBusinessDays(DateOnly, DateOnly)"/>
    int CountBusinessDays(DateTime start, DateTime end);

    /// <summary>Lista os dias úteis do período (inclusive), em ordem cronológica.</summary>
    IReadOnlyList<DateOnly> GetBusinessDays(DateOnly start, DateOnly end);

    /// <summary>Retorna o N-ésimo dia útil do mês (ex.: 5º dia útil para pagamento de salário).</summary>
    DateOnly GetNthBusinessDayOfMonth(int year, int month, int n);

    /// <summary>Primeiro dia útil do mês.</summary>
    DateOnly GetFirstBusinessDayOfMonth(int year, int month);

    /// <summary>Último dia útil do mês.</summary>
    DateOnly GetLastBusinessDayOfMonth(int year, int month);

    /// <summary>Quantidade de dias úteis do mês.</summary>
    int CountBusinessDaysInMonth(int year, int month);
}
