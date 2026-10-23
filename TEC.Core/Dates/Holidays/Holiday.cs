using TEC.Core.Csv.Attributes;
using TEC.Core.Dates.Holidays.Internal;

namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Feriado ou dia não útil, nacional, estadual ou municipal.
/// </summary>
/// <remarks>
/// Layout esperado no CSV (separador <c>;</c>, cabeçalho obrigatório, acentos opcionais). As colunas <c>Uf</c> e
/// <c>CodigoIbge</c> são opcionais: sem elas, o feriado é nacional.
/// <code>
/// Data;Descricao;Uf;CodigoIbge
/// 01/01/2026;Confraternização Universal;;
/// 09/07/2026;Revolução Constitucionalista;SP;
/// 25/01/2026;Aniversário de São Paulo;SP;3550308
/// </code>
/// A data também é aceita no formato ISO (2026-01-01). Com o código IBGE, a UF pode ficar vazia (é deduzida dele).
/// <para>Imutável: as propriedades só podem ser definidas na criação (construtor ou inicializador de objeto),
/// o que torna seguro compartilhar as instâncias devolvidas pelos provedores de feriados.</para>
/// </remarks>
public sealed class Holiday
{
    private readonly string? _uf;
    private readonly int? _ibgeCode;

    /// <summary>
    /// Cria um feriado vazio, a ser preenchido por inicializador de objeto (usado também pela leitura de CSV).
    /// </summary>
    public Holiday()
    {
    }

    /// <summary>Cria um feriado nacional.</summary>
    /// <param name="date">Data do feriado.</param>
    /// <param name="description">Descrição (não nula).</param>
    /// <exception cref="ArgumentNullException"><paramref name="description"/> nula.</exception>
    public Holiday(DateOnly date, string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        Date = date;
        Description = description;
    }

    /// <summary>Cria um feriado restrito à localidade (estadual ou municipal; nacional com <see cref="HolidayLocation.National"/>).</summary>
    public Holiday(DateOnly date, string description, HolidayLocation location)
        : this(date, description)
    {
        ArgumentNullException.ThrowIfNull(location);
        _uf = location.Uf;
        _ibgeCode = location.IbgeCode;
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

    /// <summary>
    /// Sigla da UF, em maiúsculas, para feriados estaduais e municipais (<c>null</c> nos nacionais).
    /// Em feriados municipais sem UF informada, é deduzida do <see cref="IbgeCode"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Sigla inválida ou diferente da UF do <see cref="IbgeCode"/>.</exception>
    [CsvColumn("Uf", Order = 3)]
    public string? Uf
    {
        get => _uf ?? BrazilianStates.GetUf(_ibgeCode);
        init
        {
            var uf = BrazilianStates.NormalizeUf(value, nameof(Uf));
            BrazilianStates.EnsureConsistent(uf, _ibgeCode, nameof(Uf));
            _uf = uf;
        }
    }

    /// <summary>Código IBGE do município (7 dígitos) para feriados municipais.</summary>
    /// <exception cref="ArgumentException">Código inválido ou de outra UF.</exception>
    [CsvColumn("CodigoIbge", Order = 4)]
    public int? IbgeCode
    {
        get => _ibgeCode;
        init
        {
            BrazilianStates.ValidateIbgeCode(value, nameof(IbgeCode));
            BrazilianStates.EnsureConsistent(_uf, value, nameof(IbgeCode));
            _ibgeCode = value;
        }
    }

    /// <summary>Abrangência, deduzida de <see cref="Uf"/> e <see cref="IbgeCode"/>.</summary>
    [CsvIgnore]
    public HolidayScope Scope => _ibgeCode is not null ? HolidayScope.Municipal
        : _uf is not null ? HolidayScope.State
        : HolidayScope.National;

    /// <summary>Indica se o feriado vale na localidade (nacional sempre; estadual na mesma UF; municipal no mesmo município).</summary>
    public bool AppliesTo(HolidayLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return Scope switch
        {
            HolidayScope.Municipal => location.IbgeCode == _ibgeCode,
            HolidayScope.State => location.Uf == _uf,
            _ => true,
        };
    }

    /// <summary>Data, descrição e localidade: "25/01/2026 - Aniversário de São Paulo (SP/3550308)".</summary>
    public override string ToString() => Scope switch
    {
        HolidayScope.Municipal => $"{Date:dd/MM/yyyy} - {Description} ({Uf}/{IbgeCode})",
        HolidayScope.State => $"{Date:dd/MM/yyyy} - {Description} ({Uf})",
        _ => $"{Date:dd/MM/yyyy} - {Description}",
    };
}
