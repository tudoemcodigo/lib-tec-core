namespace TEC.Core.Csv;

/// <summary>
/// Forma como propriedades do tipo enum são escritas no CSV.
/// Na leitura, os três formatos são aceitos automaticamente.
/// </summary>
public enum CsvEnumFormat
{
    /// <summary>Nome do membro (ex.: "Ativo").</summary>
    Name = 0,

    /// <summary>Valor numérico (ex.: "1").</summary>
    Code = 1,

    /// <summary>Descrição do atributo <c>[Description]</c>/<c>[Display]</c> (ex.: "Cliente ativo").</summary>
    Description = 2
}
