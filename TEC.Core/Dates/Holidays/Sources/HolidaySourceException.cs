namespace TEC.Core.Dates.Holidays.Sources;

/// <summary>
/// Falha na carga de uma fonte de feriados obrigatória. A exceção original fica em <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class HolidaySourceException : Exception
{
    /// <summary>Cria a exceção.</summary>
    /// <param name="sourceName">Nome da fonte que falhou.</param>
    /// <param name="innerException">Erro original.</param>
    public HolidaySourceException(string sourceName, Exception innerException)
        : base($"Falha ao carregar os feriados da fonte '{sourceName}': {innerException?.Message}", innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
        SourceName = sourceName ?? string.Empty;
    }

    /// <summary>Nome da fonte que falhou.</summary>
    public string SourceName { get; }
}
