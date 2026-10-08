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
    /// <summary>Cria a exceção; a linha e a coluna são acrescentadas ao final da mensagem.</summary>
    /// <param name="message">Descrição do erro. Não inclua o conteúdo do arquivo (pode conter dados pessoais).</param>
    /// <param name="lineNumber">Número da linha no arquivo (começando em 1).</param>
    /// <param name="columnName">Nome da coluna, quando aplicável.</param>
    /// <param name="rawValue">Valor original (preencha somente com <see cref="CsvOptions.IncludeRawValueInErrors"/>).</param>
    /// <param name="innerException">Exceção original.</param>
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
