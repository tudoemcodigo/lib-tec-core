namespace TEC.Core.Dates.TimeZones;

/// <summary>
/// Utilitários de fuso horário de Brasília. Útil quando o servidor roda em UTC (containers, nuvem).
/// </summary>
public static class BrazilTimeZone
{
    /// <summary>Identificador IANA do horário de Brasília.</summary>
    public const string TimeZoneId = "America/Sao_Paulo";

    private const string WindowsTimeZoneId = "E. South America Standard Time";

    private static readonly Lazy<TimeZoneInfo> LazyInfo = new(Resolve);

    /// <summary>
    /// Fuso horário de Brasília. Se a base de fusos do sistema não estiver disponível
    /// (ex.: container sem tzdata), usa UTC-03:00 fixo — o Brasil não adota horário de verão desde 2019.
    /// </summary>
    public static TimeZoneInfo Info => LazyInfo.Value;

    /// <summary>Data e hora atual em Brasília (horário de parede, com <see cref="DateTimeKind.Unspecified"/>).</summary>
    /// <remarks>
    /// O valor <b>já está</b> no horário de Brasília: não chame <see cref="ToBrasiliaTime(DateTime)"/> sobre ele
    /// (lança <see cref="ArgumentException"/> justamente para evitar o desconto duplo de 3 horas).
    /// Para obter o instante em UTC, use <see cref="FromBrasiliaTimeToUtc"/>, que trata <see cref="DateTimeKind.Unspecified"/>
    /// como horário de Brasília.
    /// </remarks>
    public static DateTime Now => GetNow(TimeProvider.System);

    /// <summary>Data atual em Brasília (independente do fuso do servidor).</summary>
    public static DateOnly Today => GetToday(TimeProvider.System);

    /// <summary>
    /// Data e hora atual em Brasília segundo o relógio informado (horário de parede, com <see cref="DateTimeKind.Unspecified"/>).
    /// Use com o <see cref="TimeProvider"/> injetado para que o código seja testável (ex.: <c>FakeTimeProvider</c>).
    /// </summary>
    /// <param name="timeProvider">Relógio (em produção, <see cref="TimeProvider.System"/>).</param>
    /// <remarks>Mesmas regras de <see cref="Now"/>: o valor já está no horário de Brasília.</remarks>
    public static DateTime GetNow(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, Info);
    }

    /// <summary>Data atual em Brasília segundo o relógio informado.</summary>
    /// <param name="timeProvider">Relógio (em produção, <see cref="TimeProvider.System"/>).</param>
    public static DateOnly GetToday(TimeProvider timeProvider) => DateOnly.FromDateTime(GetNow(timeProvider));

    /// <summary>
    /// Converte um instante em UTC ou no fuso do servidor para o horário de Brasília
    /// (resultado com <see cref="DateTimeKind.Unspecified"/>, como <see cref="Now"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="DateTimeKind.Utc"/> é convertido diretamente; <see cref="DateTimeKind.Local"/> passa antes por
    /// <see cref="DateTime.ToUniversalTime"/>. <see cref="DateTimeKind.Unspecified"/> é recusado: o valor pode já estar em
    /// Brasília (ex.: <see cref="Now"/>, colunas de banco lidas sem fuso) e tratá-lo como UTC descontaria 3 horas indevidamente.
    /// Nesse caso, declare a interpretação com <see cref="ToBrasiliaTime(DateTime, DateTimeKind)"/>.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="dateTime"/> com <see cref="DateTimeKind.Unspecified"/>.</exception>
    public static DateTime ToBrasiliaTime(this DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            throw new ArgumentException(
                "DateTime com Kind = Unspecified é ambíguo: pode ser UTC, horário do servidor ou já estar em Brasília " +
                "(como BrazilTimeZone.Now), e convertê-lo poderia deslocar o horário indevidamente. " +
                "Use DateTime.SpecifyKind, ou a sobrecarga ToBrasiliaTime(dateTime, DateTimeKind.Utc/Local) para declarar a interpretação; " +
                "se o valor já está em Brasília, não converta.",
                nameof(dateTime));
        }

        return ToBrasiliaTimeCore(dateTime, dateTime.Kind);
    }

    /// <summary>
    /// Converte para o horário de Brasília declarando como interpretar valores com <see cref="DateTimeKind.Unspecified"/>
    /// (resultado com <see cref="DateTimeKind.Unspecified"/>).
    /// </summary>
    /// <param name="dateTime">Data e hora. Se o <see cref="DateTime.Kind"/> for <see cref="DateTimeKind.Utc"/> ou
    /// <see cref="DateTimeKind.Local"/>, ele prevalece e <paramref name="unspecifiedKind"/> é ignorado.</param>
    /// <param name="unspecifiedKind">Interpretação de <see cref="DateTimeKind.Unspecified"/>:
    /// <see cref="DateTimeKind.Utc"/> (ex.: colunas gravadas em UTC) ou <see cref="DateTimeKind.Local"/> (fuso do servidor).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="unspecifiedKind"/> diferente de
    /// <see cref="DateTimeKind.Utc"/> e <see cref="DateTimeKind.Local"/>.</exception>
    public static DateTime ToBrasiliaTime(this DateTime dateTime, DateTimeKind unspecifiedKind)
    {
        if (unspecifiedKind is not (DateTimeKind.Utc or DateTimeKind.Local))
        {
            throw new ArgumentOutOfRangeException(nameof(unspecifiedKind), unspecifiedKind,
                "Informe DateTimeKind.Utc ou DateTimeKind.Local. Um valor que já está em Brasília não precisa de conversão.");
        }

        return ToBrasiliaTimeCore(dateTime, dateTime.Kind == DateTimeKind.Unspecified ? unspecifiedKind : dateTime.Kind);
    }

    /// <summary>Converte um horário de Brasília para UTC (resultado com <see cref="DateTimeKind.Utc"/>).</summary>
    /// <remarks>
    /// <para>O <see cref="DateTime.Kind"/> define a interpretação:
    /// <see cref="DateTimeKind.Unspecified"/> é tratado como horário de parede de Brasília;
    /// <see cref="DateTimeKind.Utc"/> já está em UTC e é devolvido sem alteração (nunca é deslocado 3 horas);
    /// <see cref="DateTimeKind.Local"/> representa um instante no fuso do servidor e é convertido com
    /// <see cref="DateTime.ToUniversalTime"/>.</para>
    /// <para>Horários de verão históricos (até fev/2019) são tratados de forma definida, sem exceção:
    /// horário inexistente (lacuna no início do horário de verão, ex.: 04/11/2018 00:30) é interpretado com o
    /// deslocamento padrão (-03:00), o que equivale a avançar o relógio em 1 hora (00:30 → 03:30 UTC = 01:30 de verão);
    /// horário ambíguo (repetido no fim do horário de verão, ex.: 16/02/2019 23:30) é interpretado como horário padrão
    /// (-03:00), isto é, a segunda ocorrência.</para>
    /// </remarks>
    public static DateTime FromBrasiliaTimeToUtc(this DateTime brasiliaTime)
    {
        switch (brasiliaTime.Kind)
        {
            case DateTimeKind.Utc:
                return brasiliaTime;
            case DateTimeKind.Local:
                return brasiliaTime.ToUniversalTime();
        }

        // GetUtcOffset devolve o deslocamento padrão para horários inexistentes e ambíguos
        // (ConvertTimeToUtc lançaria ArgumentException para horários inexistentes)
        var offset = Info.GetUtcOffset(brasiliaTime);
        return DateTime.SpecifyKind(brasiliaTime - offset, DateTimeKind.Utc);
    }

    private static DateTime ToBrasiliaTimeCore(DateTime dateTime, DateTimeKind kind)
    {
        var utc = kind == DateTimeKind.Local
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Local).ToUniversalTime()
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Info);
    }

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in (string[])[TimeZoneId, WindowsTimeZoneId])
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Tenta o próximo identificador
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(TimeZoneId, TimeSpan.FromHours(-3), "Horário de Brasília", "BRT");
    }
}
