using TEC.Core.Common.Guards;
using TEC.Core.Dates.Holidays.Internal;

namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Localidade usada para escolher os feriados aplicáveis: nacionais, da UF e do município (pelo código IBGE).
/// </summary>
/// <example>
/// <code>
/// var saoPaulo = new HolidayLocation("SP", 3550308);   // nacionais + estaduais de SP + municipais da capital
/// var parana   = new HolidayLocation("PR");            // nacionais + estaduais do PR
/// var rio      = HolidayLocation.FromIbgeCode(3304557); // UF deduzida do código (RJ)
/// </code>
/// </example>
public sealed record HolidayLocation
{
    private HolidayLocation(string? uf, int? ibgeCode)
    {
        Uf = uf;
        IbgeCode = ibgeCode;
    }

    /// <summary>Localidade da UF (feriados nacionais e estaduais).</summary>
    /// <exception cref="ArgumentException">Sigla vazia ou que não é de uma das 27 UFs.</exception>
    public HolidayLocation(string uf)
        : this(BrazilianStates.NormalizeUf(Guard.NotNullOrWhiteSpace(uf), nameof(uf)), null)
    {
    }

    /// <summary>Localidade do município (feriados nacionais, estaduais e municipais).</summary>
    /// <exception cref="ArgumentException">UF inválida, código IBGE inválido ou município de outra UF.</exception>
    public HolidayLocation(string uf, int ibgeCode)
        : this(uf)
    {
        BrazilianStates.ValidateIbgeCode(ibgeCode, nameof(ibgeCode));
        BrazilianStates.EnsureConsistent(Uf, ibgeCode, nameof(ibgeCode));
        IbgeCode = ibgeCode;
    }

    /// <summary>Somente feriados nacionais.</summary>
    public static HolidayLocation National { get; } = new(null, null);

    /// <summary>Localidade do município, com a UF deduzida do código IBGE.</summary>
    /// <exception cref="ArgumentException">Código IBGE inválido.</exception>
    public static HolidayLocation FromIbgeCode(int ibgeCode)
    {
        BrazilianStates.ValidateIbgeCode(ibgeCode, nameof(ibgeCode));
        return new HolidayLocation(BrazilianStates.GetUf(ibgeCode), (int?)ibgeCode); // (int?): construtor privado, sem revalidar
    }

    /// <summary>Sigla da UF, em maiúsculas (<c>null</c> em <see cref="National"/>).</summary>
    public string? Uf { get; }

    /// <summary>Código IBGE do município, com 7 dígitos (<c>null</c> se a localidade for nacional ou estadual).</summary>
    public int? IbgeCode { get; }

    /// <summary>"Nacional", a sigla da UF ("SP") ou UF e código IBGE ("SP/3550308").</summary>
    public override string ToString() => (Uf, IbgeCode) switch
    {
        (null, _) => "Nacional",
        (_, null) => Uf,
        _ => $"{Uf}/{IbgeCode}",
    };
}
