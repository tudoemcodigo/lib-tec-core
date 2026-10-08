using System.Globalization;
using TEC.Core.Common.Guards;

namespace TEC.Core.Dates.Formatting;

/// <summary>
/// Formatação e interpretação de datas no padrão brasileiro.
/// </summary>
public static class DateFormatter
{
    /// <summary>Formato de data brasileiro: dd/MM/yyyy.</summary>
    public const string BrazilianDateFormat = "dd/MM/yyyy";

    /// <summary>Formato de data e hora brasileiro: dd/MM/yyyy HH:mm:ss.</summary>
    public const string BrazilianDateTimeFormat = "dd/MM/yyyy HH:mm:ss";

    /// <summary>Formato de data ISO 8601: yyyy-MM-dd.</summary>
    public const string IsoDateFormat = "yyyy-MM-dd";

    private static CultureInfo BrazilianCulture => Common.Globalization.BrazilianCulture.Instance;

    private static readonly string[] AcceptedDateFormats =
        ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "dd.MM.yyyy", "ddMMyyyy", "yyyy-MM-dd", "yyyyMMdd"];

    private static readonly string[] AcceptedDateTimeFormats =
        ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm:ss", "d/M/yyyy H:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm"];

    /// <summary>30/09/2026.</summary>
    public static string ToBrazilianDate(this DateTime date) => date.ToString(BrazilianDateFormat, CultureInfo.InvariantCulture);

    /// <summary>30/09/2026.</summary>
    public static string ToBrazilianDate(this DateOnly date) => date.ToString(BrazilianDateFormat, CultureInfo.InvariantCulture);

    /// <summary>30/09/2026 14:35 (ou 30/09/2026 14:35:12 com segundos).</summary>
    public static string ToBrazilianDateTime(this DateTime date, bool includeSeconds = false) =>
        date.ToString(includeSeconds ? BrazilianDateTimeFormat : "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>ISO 8601 completo, com fuso quando disponível: 2026-09-30T14:35:12.0000000Z.</summary>
    public static string ToIso8601(this DateTime date) => date.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>ISO 8601 de data: 2026-09-30.</summary>
    public static string ToIso8601(this DateOnly date) => date.ToString(IsoDateFormat, CultureInfo.InvariantCulture);

    /// <summary>30 de setembro de 2026.</summary>
    public static string ToLongDate(this DateOnly date) => date.ToString("d 'de' MMMM 'de' yyyy", BrazilianCulture);

    /// <inheritdoc cref="ToLongDate(DateOnly)"/>
    public static string ToLongDate(this DateTime date) => DateOnly.FromDateTime(date).ToLongDate();

    /// <summary>quarta-feira, 30 de setembro de 2026.</summary>
    public static string ToFullDate(this DateOnly date) => date.ToString("dddd, d 'de' MMMM 'de' yyyy", BrazilianCulture);

    /// <inheritdoc cref="ToFullDate(DateOnly)"/>
    public static string ToFullDate(this DateTime date) => DateOnly.FromDateTime(date).ToFullDate();

    /// <summary>setembro/2026.</summary>
    public static string ToMonthYear(this DateOnly date) => date.ToString("MMMM/yyyy", BrazilianCulture);

    /// <inheritdoc cref="ToMonthYear(DateOnly)"/>
    public static string ToMonthYear(this DateTime date) => DateOnly.FromDateTime(date).ToMonthYear();

    /// <summary>Nome do mês em português: 9 → "setembro".</summary>
    public static string GetMonthName(int month, bool capitalize = false)
    {
        Guard.InRange(month, 1, 12);
        var name = BrazilianCulture.DateTimeFormat.GetMonthName(month);
        return capitalize ? Capitalize(name) : name;
    }

    /// <summary>Nome do dia da semana em português: Wednesday → "quarta-feira".</summary>
    public static string GetDayOfWeekName(DayOfWeek dayOfWeek, bool capitalize = false)
    {
        if (!Enum.IsDefined(dayOfWeek))
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), "Dia da semana inválido.");

        var name = BrazilianCulture.DateTimeFormat.GetDayName(dayOfWeek);
        return capitalize ? Capitalize(name) : name;
    }

    /// <summary>
    /// Tempo relativo em português: "agora", "há 5 minutos", "em 2 dias", "há 1 ano".
    /// </summary>
    /// <param name="date">Data a comparar com "agora" do relógio do sistema, no mesmo <see cref="DateTimeKind"/> da data.</param>
    public static string ToRelativeTime(this DateTime date) => RelativeTime(date, Now(date.Kind, TimeProvider.System));

    /// <summary>
    /// Tempo relativo em português em relação a uma data de referência: "agora", "há 5 minutos", "em 2 dias".
    /// </summary>
    /// <param name="date">Data a comparar.</param>
    /// <param name="reference">Data de referência.</param>
    /// <exception cref="ArgumentException">Uma data em UTC e a outra em horário local (comparação seria incorreta).</exception>
    public static string ToRelativeTime(this DateTime date, DateTime reference)
    {
        if (IsUtcLocalMix(date.Kind, reference.Kind))
            throw new ArgumentException("A data e a referência devem estar no mesmo fuso (ambas UTC ou ambas locais).", nameof(reference));

        return RelativeTime(date, reference);
    }

    /// <summary>
    /// Tempo relativo em português em relação ao instante atual do relógio informado (no mesmo <see cref="DateTimeKind"/>
    /// da data): "agora", "há 5 minutos", "em 2 dias".
    /// </summary>
    /// <param name="date">Data a comparar.</param>
    /// <param name="timeProvider">Relógio injetado (testável); em produção, <see cref="TimeProvider.System"/>.</param>
    public static string ToRelativeTime(this DateTime date, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return RelativeTime(date, Now(date.Kind, timeProvider));
    }

    // "Agora" no mesmo Kind da data: UTC para UTC; fuso do relógio (servidor) para Local e Unspecified
    private static DateTime Now(DateTimeKind kind, TimeProvider timeProvider) => kind == DateTimeKind.Utc
        ? timeProvider.GetUtcNow().UtcDateTime
        : timeProvider.GetLocalNow().DateTime;

    private static string RelativeTime(DateTime date, DateTime now)
    {
        var diff = date - now;
        bool future = diff > TimeSpan.Zero;
        var span = diff.Duration();

        if (span.TotalSeconds < 60)
            return "agora";

        var (value, singular, plural) = span switch
        {
            { TotalMinutes: < 60 } => ((int)span.TotalMinutes, "minuto", "minutos"),
            { TotalHours: < 24 } => ((int)span.TotalHours, "hora", "horas"),
            { TotalDays: < 30 } => ((int)span.TotalDays, "dia", "dias"),
            // Meses de 30 dias: de 360 a 364 dias daria "12 meses"; limita a 11 (a partir de 365 dias vira "1 ano")
            { TotalDays: < 365 } => (Math.Min((int)(span.TotalDays / 30), 11), "mês", "meses"),
            _ => ((int)(span.TotalDays / 365), "ano", "anos")
        };

        var text = $"{value} {(value == 1 ? singular : plural)}";
        return future ? $"em {text}" : $"há {text}";
    }

    /// <summary>
    /// Interpreta data nos formatos dd/MM/yyyy, d/M/yyyy, dd-MM-yyyy, dd.MM.yyyy, ddMMyyyy, yyyy-MM-dd e yyyyMMdd.
    /// </summary>
    public static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Interpreta data e hora (padrão brasileiro ou ISO). Aceita também somente a data.</summary>
    public static bool TryParseDateTime(string? value, out DateTime dateTime)
    {
        var text = value?.Trim();
        if (DateTime.TryParseExact(text, AcceptedDateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
            return true;

        if (TryParseDate(text, out var date))
        {
            dateTime = date.ToDateTime(TimeOnly.MinValue);
            return true;
        }

        dateTime = default;
        return false;
    }

    private static bool IsUtcLocalMix(DateTimeKind a, DateTimeKind b) =>
        (a == DateTimeKind.Utc && b == DateTimeKind.Local) || (a == DateTimeKind.Local && b == DateTimeKind.Utc);

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpper(value[0], BrazilianCulture) + value[1..];
}
