namespace TEC.Core.Dates.Holidays.Sources;

/// <summary>
/// Falha na carga de uma fonte opcional (ignorada pelo <see cref="HolidayCalendar"/>). Útil para logs e health checks.
/// </summary>
/// <param name="SourceName">Nome da fonte (<see cref="IHolidaySource.Name"/>).</param>
/// <param name="Exception">Erro ocorrido.</param>
public sealed record HolidaySourceFailure(string SourceName, Exception Exception);
