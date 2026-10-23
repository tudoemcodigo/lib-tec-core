namespace TEC.Core.Csv.Attributes;

/// <summary>
/// Configura o mapeamento de uma propriedade para uma coluna do CSV.
/// </summary>
/// <example>
/// <code>
/// [CsvColumn("Data de Nascimento", Order = 2, Format = "dd/MM/yyyy")]
/// public DateOnly BirthDate { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CsvColumnAttribute : Attribute
{
    /// <summary>Mapeia a propriedade usando o próprio nome como cabeçalho.</summary>
    public CsvColumnAttribute()
    {
    }

    /// <summary>Mapeia a propriedade para a coluna com o nome informado.</summary>
    public CsvColumnAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>Nome da coluna no cabeçalho. Se não informado, usa o nome da propriedade.</summary>
    public string? Name { get; }

    /// <summary>Posição da coluna (menor primeiro). Sem valor, segue a ordem de declaração das propriedades.</summary>
    public int Order { get; set; } = int.MaxValue;

    /// <summary>
    /// Formato de leitura/escrita. Ex.: "dd/MM/yyyy" para datas, "N2" para números
    /// ou "Sim|Não" para booleanos (texto verdadeiro|texto falso).
    /// </summary>
    public string? Format { get; set; }
}
