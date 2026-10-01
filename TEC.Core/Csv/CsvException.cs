namespace TEC.Core.Csv;

/// <summary>
/// Erro de leitura ou conversão de CSV, com a linha e a coluna onde ocorreu.
/// </summary>
/// <remarks>
/// A mensagem nunca contém o conteúdo do arquivo. O valor original só é preenchido em <see cref="RawValue"/>
/// quando <see cref="CsvOptions.IncludeRawValueInErrors"/> estiver habilitado.
/// </remarks>
public sealed class CsvException : Exception
{
    public CsvException(string message, long lineNumber, string? columnName = null, string? rawValue = null, Exception? innerException = null)
        : base(BuildMessage(message, lineNumber, columnName), innerException)
    {
        LineNumber = lineNumber;
        ColumnName = columnName;
        RawValue = rawValue;
    }

    /// <summary>Número da linha no arquivo (começando em 1).</summary>
    public long LineNumber { get; }

    /// <summary>Nome da coluna, quando aplicável.</summary>
    public string? ColumnName { get; }

    /// <summary>Valor original que não pôde ser convertido (somente com <see cref="CsvOptions.IncludeRawValueInErrors"/>).</summary>
    public string? RawValue { get; }

    private static string BuildMessage(string message, long lineNumber, string? columnName) =>
        columnName is null
            ? $"{message} (linha {lineNumber})"
            : $"{message} (linha {lineNumber}, coluna '{columnName}')";
}
