using TEC.Core.Common.Guards;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.TimeZones;

namespace TEC.Core.Dates.BusinessDays;

/// <summary>
/// Cálculo de dias úteis. Por padrão, sábado e domingo não são úteis; os feriados vêm do <see cref="IHolidayProvider"/>.
/// </summary>
/// <example>
/// <code>
/// var holidays = await CsvHolidayProvider.FromFileAsync("feriados-2026.csv");
/// var calculator = new BusinessDayCalculator(holidays);
/// var vencimento = calculator.NextOrSameBusinessDay(new DateOnly(2026, 11, 15));
/// </code>
/// </example>
public sealed class BusinessDayCalculator : IBusinessDayCalculator
{
    // Limite de segurança para evitar laços infinitos com cadastro de feriados inconsistente (~10 anos)
    private const int MaxSearchDays = 3660;

    private readonly IHolidayProvider _holidayProvider;
    private readonly HashSet<DayOfWeek> _nonWorkingDays;

    /// <summary>Cria a calculadora.</summary>
    /// <param name="holidayProvider">Fonte dos feriados.</param>
    /// <param name="nonWorkingDays">Dias da semana não úteis. Padrão: sábado e domingo.</param>
    public BusinessDayCalculator(IHolidayProvider holidayProvider, IEnumerable<DayOfWeek>? nonWorkingDays = null)
    {
        _holidayProvider = Guard.NotNull(holidayProvider);
        _nonWorkingDays = [.. nonWorkingDays ?? [DayOfWeek.Saturday, DayOfWeek.Sunday]];
        Guard.Against(_nonWorkingDays.Any(d => !Enum.IsDefined(d)), "Dia da semana inválido.", nameof(nonWorkingDays));
        Guard.Against(_nonWorkingDays.Count >= 7, "Ao menos um dia da semana deve ser útil.", nameof(nonWorkingDays));
    }

    /// <summary>Quantidade máxima de dias úteis somados/subtraídos por chamada (~100 anos). Evita laços excessivos.</summary>
    public const int MaxBusinessDaysToAdd = 26_000;

    /// <summary>
    /// Tamanho máximo, em dias corridos, do intervalo de <see cref="CountBusinessDays(DateOnly, DateOnly)"/> e
    /// <see cref="GetBusinessDays"/> (~100 anos). Evita laços e listas de milhões de itens com datas vindas de fora.
    /// </summary>
    public const int MaxRangeDays = 36_600;

    /// <inheritdoc />
    public bool IsBusinessDay(DateOnly date) =>
        !_nonWorkingDays.Contains(date.DayOfWeek) && !_holidayProvider.IsHoliday(date);

    /// <inheritdoc />
    public bool IsBusinessDay(DateTime date) => IsBusinessDay(ToCalendarDate(date));

    /// <inheritdoc />
    public DateOnly NextBusinessDay(DateOnly date) => FindBusinessDay(Step(date, 1), step: 1);

    /// <inheritdoc />
    public DateTime NextBusinessDay(DateTime date) => WithTime(NextBusinessDay(ToCalendarDate(date)), date);

    /// <inheritdoc />
    public DateOnly PreviousBusinessDay(DateOnly date) => FindBusinessDay(Step(date, -1), step: -1);

    /// <inheritdoc />
    public DateTime PreviousBusinessDay(DateTime date) => WithTime(PreviousBusinessDay(ToCalendarDate(date)), date);

    /// <inheritdoc />
    public DateOnly NextOrSameBusinessDay(DateOnly date) => FindBusinessDay(date, step: 1);

    /// <inheritdoc />
    public DateTime NextOrSameBusinessDay(DateTime date) => WithTime(NextOrSameBusinessDay(ToCalendarDate(date)), date);

    /// <inheritdoc />
    public DateOnly PreviousOrSameBusinessDay(DateOnly date) => FindBusinessDay(date, step: -1);

    /// <inheritdoc />
    public DateTime PreviousOrSameBusinessDay(DateTime date) => WithTime(PreviousOrSameBusinessDay(ToCalendarDate(date)), date);

    /// <inheritdoc />
    public DateOnly AddBusinessDays(DateOnly date, int businessDays)
    {
        Guard.InRange(businessDays, -MaxBusinessDaysToAdd, MaxBusinessDaysToAdd);
        int step = Math.Sign(businessDays);
        int remaining = Math.Abs(businessDays);
        var current = date;

        while (remaining > 0)
        {
            current = FindBusinessDay(Step(current, step), step);
            remaining--;
        }

        return current;
    }

    /// <inheritdoc />
    public DateTime AddBusinessDays(DateTime date, int businessDays) =>
        WithTime(AddBusinessDays(ToCalendarDate(date), businessDays), date);

    /// <inheritdoc />
    public int CountBusinessDays(DateOnly start, DateOnly end)
    {
        if (start > end)
            (start, end) = (end, start);
        ValidateRange(start, end);

        // Laço por número do dia: evita estouro ao chegar em DateOnly.MaxValue
        int count = 0;
        for (int dayNumber = start.DayNumber; dayNumber <= end.DayNumber; dayNumber++)
        {
            if (IsBusinessDay(DateOnly.FromDayNumber(dayNumber)))
                count++;
        }

        return count;
    }

    /// <inheritdoc />
    public int CountBusinessDays(DateTime start, DateTime end) =>
        CountBusinessDays(ToCalendarDate(start), ToCalendarDate(end));

    /// <inheritdoc />
    public IReadOnlyList<DateOnly> GetBusinessDays(DateOnly start, DateOnly end)
    {
        if (start > end)
            (start, end) = (end, start);
        ValidateRange(start, end);

        var result = new List<DateOnly>();
        for (int dayNumber = start.DayNumber; dayNumber <= end.DayNumber; dayNumber++)
        {
            var day = DateOnly.FromDayNumber(dayNumber);
            if (IsBusinessDay(day))
                result.Add(day);
        }

        return result;
    }

    /// <inheritdoc />
    public DateOnly GetNthBusinessDayOfMonth(int year, int month, int n)
    {
        ValidateYearMonth(year, month);
        Guard.Positive(n);
        var businessDays = GetBusinessDaysInMonth(year, month);
        if (n > businessDays.Count)
            throw new ArgumentOutOfRangeException(nameof(n), $"O mês {month:00}/{year} possui apenas {businessDays.Count} dias úteis.");

        return businessDays[n - 1];
    }

    /// <inheritdoc />
    public DateOnly GetFirstBusinessDayOfMonth(int year, int month) => GetNthBusinessDayOfMonth(year, month, 1);

    /// <inheritdoc />
    public DateOnly GetLastBusinessDayOfMonth(int year, int month)
    {
        var businessDays = GetBusinessDaysInMonth(year, month);
        return businessDays.Count > 0
            ? businessDays[^1]
            : throw new InvalidOperationException($"O mês {month:00}/{year} não possui dias úteis.");
    }

    /// <inheritdoc />
    public int CountBusinessDaysInMonth(int year, int month) => GetBusinessDaysInMonth(year, month).Count;

    private IReadOnlyList<DateOnly> GetBusinessDaysInMonth(int year, int month)
    {
        ValidateYearMonth(year, month);
        return GetBusinessDays(new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
    }

    private static void ValidateRange(DateOnly start, DateOnly end)
    {
        if (end.DayNumber - start.DayNumber > MaxRangeDays)
            throw new ArgumentOutOfRangeException(nameof(end), $"O intervalo não pode passar de {MaxRangeDays} dias.");
    }

    private static void ValidateYearMonth(int year, int month)
    {
        Guard.InRange(year, DateOnly.MinValue.Year, DateOnly.MaxValue.Year);
        Guard.InRange(month, 1, 12);
    }

    // Avança "step" dias com verificação dos limites do calendário (mensagem clara em vez de estouro interno)
    private static DateOnly Step(DateOnly date, int step)
    {
        long next = (long)date.DayNumber + step;
        if (next < DateOnly.MinValue.DayNumber || next > DateOnly.MaxValue.DayNumber)
            throw new ArgumentOutOfRangeException(nameof(date), "O cálculo ultrapassa o limite do calendário (01/01/0001 a 31/12/9999).");

        return DateOnly.FromDayNumber((int)next);
    }

    private DateOnly FindBusinessDay(DateOnly start, int step)
    {
        var current = start;
        for (int i = 0; i < MaxSearchDays; i++)
        {
            if (IsBusinessDay(current))
                return current;

            current = Step(current, step);
        }

        throw new InvalidOperationException($"Nenhum dia útil encontrado em {MaxSearchDays} dias a partir de {start:dd/MM/yyyy}. Verifique o cadastro de feriados.");
    }

    // DateTime em UTC representa um instante: o dia útil é avaliado na data de Brasília (23:00 UTC de sexta ainda é sexta
    // em Brasília, mas 02:00 UTC de sábado é sexta 23:00 em Brasília). Local/Unspecified usam a própria data do calendário.
    private static DateOnly ToCalendarDate(DateTime date) =>
        DateOnly.FromDateTime(date.Kind == DateTimeKind.Utc ? date.ToBrasiliaTime() : date);

    // Mantém o horário de parede: em UTC, o horário de Brasília é preservado e o resultado volta para UTC
    private static DateTime WithTime(DateOnly date, DateTime original)
    {
        if (original.Kind != DateTimeKind.Utc)
            return date.ToDateTime(TimeOnly.FromDateTime(original), original.Kind);

        var brasiliaTime = date.ToDateTime(TimeOnly.FromDateTime(original.ToBrasiliaTime()), DateTimeKind.Unspecified);
        return brasiliaTime.FromBrasiliaTimeToUtc();
    }
}
