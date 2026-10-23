namespace TEC.Core.Dates.Holidays.Sources;

/// <summary>
/// Origem dos feriados (arquivo, banco, API…), lida uma única vez por <see cref="HolidayCalendarBuilder.BuildAsync"/>.
/// </summary>
/// <remarks>
/// A consulta dos feriados é feita depois, em memória, pelo <see cref="HolidayCalendar"/>: a fonte só é lida na carga.
/// Implemente esta interface para fontes que não estejam prontas no TEC.Core.
/// </remarks>
public interface IHolidaySource
{
    /// <summary>Nome usado em mensagens de erro e em <see cref="HolidaySourceFailure"/>. Não deve conter segredos.</summary>
    string Name { get; }

    /// <summary>Lê os feriados da fonte.</summary>
    IAsyncEnumerable<Holiday> LoadAsync(CancellationToken cancellationToken = default);
}
