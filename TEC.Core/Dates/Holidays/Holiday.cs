using TEC.Core.Csv.Attributes;

namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Feriado ou dia não útil.
/// </summary>
/// <remarks>
/// Layout esperado no CSV (separador <c>;</c>, cabeçalho obrigatório, acentos opcionais):
/// <code>
/// Data;Descricao
/// 01/01/2026;Confraternização Universal
/// 16/02/2026;Carnaval
/// </code>
/// A data também é aceita no formato ISO (2026-01-01).
/// <para>Imutável: as propriedades só podem ser definidas na criação (construtor ou inicializador de objeto),
/// o que torna seguro compartilhar as instâncias devolvidas pelos provedores de feriados.</para>
/// </remarks>
public sealed class Holiday
{
    public Holiday()
    {
    }

    public Holiday(DateOnly date, string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        Date = date;
        Description = description;
    }

    /// <summary>Data do feriado.</summary>
    [CsvColumn("Data", Order = 1)]
    public DateOnly Date { get; init; }

    /// <summary>Descrição do feriado (não nula).</summary>
    [CsvColumn("Descricao", Order = 2)]
    public string Description
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(Description));
    } = string.Empty;

    public override string ToString() => $"{Date:dd/MM/yyyy} - {Description}";
}
