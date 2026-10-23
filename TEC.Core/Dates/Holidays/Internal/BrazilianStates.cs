namespace TEC.Core.Dates.Holidays.Internal;

/// <summary>
/// UFs e seus códigos IBGE. O código IBGE de um município tem 7 dígitos e começa pelo código da UF.
/// </summary>
internal static class BrazilianStates
{
    private static readonly Dictionary<string, int> CodesByUf = new(StringComparer.Ordinal)
    {
        ["RO"] = 11, ["AC"] = 12, ["AM"] = 13, ["RR"] = 14, ["PA"] = 15, ["AP"] = 16, ["TO"] = 17,
        ["MA"] = 21, ["PI"] = 22, ["CE"] = 23, ["RN"] = 24, ["PB"] = 25, ["PE"] = 26, ["AL"] = 27, ["SE"] = 28, ["BA"] = 29,
        ["MG"] = 31, ["ES"] = 32, ["RJ"] = 33, ["SP"] = 35,
        ["PR"] = 41, ["SC"] = 42, ["RS"] = 43,
        ["MS"] = 50, ["MT"] = 51, ["GO"] = 52, ["DF"] = 53,
    };

    private static readonly Dictionary<int, string> UfsByCode = CodesByUf.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Normaliza a sigla (maiúsculas, sem espaços). Vazio ou nulo resulta em <c>null</c>.</summary>
    /// <exception cref="ArgumentException">Sigla que não é de uma das 27 UFs.</exception>
    public static string? NormalizeUf(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // O valor recebido não entra na mensagem: pode vir de arquivo, banco ou API externa
        var uf = value.Trim().ToUpperInvariant();
        return CodesByUf.ContainsKey(uf)
            ? uf
            : throw new ArgumentException("UF inválida. Informe a sigla de uma das 27 UFs (ex.: SP).", paramName);
    }

    /// <exception cref="ArgumentException">Código fora do formato de 7 dígitos ou com prefixo de UF inexistente.</exception>
    public static void ValidateIbgeCode(int? code, string paramName)
    {
        if (code is null)
            return;

        if (code is < 1_000_000 or > 9_999_999 || !UfsByCode.ContainsKey(code.Value / 100_000))
            throw new ArgumentException("Código IBGE do município inválido: são 7 dígitos, começando pelo código da UF (ex.: 3550308).", paramName);
    }

    /// <summary>UF do município (pelos 2 primeiros dígitos do código IBGE).</summary>
    public static string? GetUf(int? ibgeCode) => ibgeCode is null ? null : UfsByCode[ibgeCode.Value / 100_000];

    /// <exception cref="ArgumentException">O município não pertence à UF.</exception>
    public static void EnsureConsistent(string? uf, int? ibgeCode, string paramName)
    {
        if (uf is not null && ibgeCode is not null && GetUf(ibgeCode) != uf)
            throw new ArgumentException("O código IBGE do município não pertence à UF informada.", paramName);
    }
}
