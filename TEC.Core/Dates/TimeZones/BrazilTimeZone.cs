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

    /// <summary>Data e hora atual em Brasília.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Info);

    /// <summary>Data atual em Brasília.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>
    /// Converte uma data UTC (ou local do servidor) para o horário de Brasília (resultado com <see cref="DateTimeKind.Unspecified"/>).
    /// <see cref="DateTimeKind.Unspecified"/> é tratado como UTC.
    /// </summary>
    public static DateTime ToBrasiliaTime(this DateTime dateTime)
    {
        var utc = dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Info);
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
