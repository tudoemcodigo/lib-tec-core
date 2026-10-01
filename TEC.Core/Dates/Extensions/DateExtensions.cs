namespace TEC.Core.Dates.Extensions;

/// <summary>
/// Extensões para <see cref="DateTime"/> e <see cref="DateOnly"/>.
/// </summary>
public static class DateExtensions
{
    /// <summary>Converte para <see cref="DateOnly"/> (descarta o horário).</summary>
    public static DateOnly ToDateOnly(this DateTime date) => DateOnly.FromDateTime(date);

    /// <summary>Converte para <see cref="DateTime"/> com o horário informado (padrão: 00:00).</summary>
    public static DateTime ToDateTime(this DateOnly date, TimeOnly? time = null, DateTimeKind kind = DateTimeKind.Unspecified)
    {
        ValidateKind(kind);
        return date.ToDateTime(time ?? TimeOnly.MinValue, kind);
    }

    /// <summary>Indica se é sábado ou domingo.</summary>
    public static bool IsWeekend(this DateOnly date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <inheritdoc cref="IsWeekend(DateOnly)"/>
    public static bool IsWeekend(this DateTime date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Indica se a data está no período (inclusive).</summary>
    /// <exception cref="ArgumentException">Início posterior ao fim.</exception>
    public static bool IsBetween(this DateOnly date, DateOnly start, DateOnly end)
    {
        ValidateRange(start, end);
        return date >= start && date <= end;
    }

    /// <inheritdoc cref="IsBetween(DateOnly, DateOnly, DateOnly)"/>
    public static bool IsBetween(this DateTime date, DateTime start, DateTime end)
    {
        ValidateRange(start, end);
        return date >= start && date <= end;
    }

    /// <summary>Primeiro dia do mês.</summary>
    public static DateOnly StartOfMonth(this DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>Primeiro dia do mês às 00:00.</summary>
    public static DateTime StartOfMonth(this DateTime date) => new(date.Year, date.Month, 1, 0, 0, 0, date.Kind);

    /// <summary>Último dia do mês.</summary>
    public static DateOnly EndOfMonth(this DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    /// <summary>Último instante do mês (23:59:59.9999999). Funciona inclusive em dezembro de 9999.</summary>
    public static DateTime EndOfMonth(this DateTime date) =>
        new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 0, 0, 0, date.Kind).EndOfDay();

    /// <summary>Primeiro dia do ano.</summary>
    public static DateOnly StartOfYear(this DateOnly date) => new(date.Year, 1, 1);

    /// <summary>Último dia do ano.</summary>
    public static DateOnly EndOfYear(this DateOnly date) => new(date.Year, 12, 31);

    /// <summary>Início do dia (00:00).</summary>
    public static DateTime StartOfDay(this DateTime date) => date.Date;

    /// <summary>Último instante do dia (23:59:59.9999999). Funciona inclusive em <see cref="DateTime.MaxValue"/>.</summary>
    public static DateTime EndOfDay(this DateTime date) => date.Date.AddTicks(TimeSpan.TicksPerDay - 1);

    /// <summary>Primeiro dia da semana (padrão: domingo, como no calendário brasileiro).</summary>
    public static DateOnly StartOfWeek(this DateOnly date, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
    {
        ValidateDayOfWeek(firstDayOfWeek);
        int diff = (7 + (date.DayOfWeek - firstDayOfWeek)) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>Último dia da semana.</summary>
    public static DateOnly EndOfWeek(this DateOnly date, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday) =>
        date.StartOfWeek(firstDayOfWeek).AddDays(6);

    /// <summary>Quantidade de dias do mês.</summary>
    public static int DaysInMonth(this DateOnly date) => DateTime.DaysInMonth(date.Year, date.Month);

    /// <summary>Trimestre (1 a 4).</summary>
    public static int Quarter(this DateOnly date) => (date.Month - 1) / 3 + 1;

    /// <summary>Semestre (1 ou 2).</summary>
    public static int Semester(this DateOnly date) => date.Month <= 6 ? 1 : 2;

    /// <summary>Calcula a idade em anos completos na data de referência (padrão: hoje).</summary>
    /// <remarks>Nascidos em 29/02 completam anos em 28/02 nos anos não bissextos.</remarks>
    /// <exception cref="ArgumentException">Data de nascimento posterior à data de referência.</exception>
    public static int CalculateAge(this DateOnly birthDate, DateOnly? referenceDate = null)
    {
        var reference = referenceDate ?? DateOnly.FromDateTime(DateTime.Today);
        if (birthDate > reference)
            throw new ArgumentException("A data de nascimento não pode ser posterior à data de referência.", nameof(birthDate));

        int age = reference.Year - birthDate.Year;
        if (reference < birthDate.AddYears(age))
            age--;

        return age;
    }

    /// <inheritdoc cref="CalculateAge(DateOnly, DateOnly?)"/>
    public static int CalculateAge(this DateTime birthDate, DateTime? referenceDate = null) =>
        DateOnly.FromDateTime(birthDate).CalculateAge(referenceDate is null ? null : DateOnly.FromDateTime(referenceDate.Value));

    private static void ValidateRange<T>(T start, T end) where T : IComparable<T>
    {
        if (start.CompareTo(end) > 0)
            throw new ArgumentException("A data inicial não pode ser posterior à data final.", nameof(start));
    }

    private static void ValidateKind(DateTimeKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), "DateTimeKind inválido.");
    }

    private static void ValidateDayOfWeek(DayOfWeek dayOfWeek)
    {
        if (!Enum.IsDefined(dayOfWeek))
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), "Dia da semana inválido.");
    }
}
