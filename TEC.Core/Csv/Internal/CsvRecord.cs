namespace TEC.Core.Csv.Internal;

/// <summary>
/// Linha já separada em campos.
/// </summary>
/// <param name="Fields">Valores dos campos.</param>
/// <param name="LineNumber">Linha inicial do registro no arquivo.</param>
/// <param name="IsEmpty">Indica linha em branco.</param>
/// <param name="QuotedFields">Indica, por campo, se estava entre aspas (<c>null</c> quando nenhum campo tinha aspas).</param>
internal readonly record struct CsvRecord(string[] Fields, long LineNumber, bool IsEmpty, bool[]? QuotedFields = null)
{
    /// <summary>Indica se o campo da posição informada estava entre aspas.</summary>
    public bool IsQuoted(int index) => QuotedFields is not null && QuotedFields[index];
}
