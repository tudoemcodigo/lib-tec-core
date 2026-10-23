using System.Globalization;
using TEC.Core.Common.Globalization;
using TEC.Core.Common.Guards;
using TEC.Core.Numbers.Words;

namespace TEC.Core.Numbers.Extensions;

/// <summary>
/// Extensões para formatação e arredondamento de valores numéricos (padrão brasileiro).
/// </summary>
public static class NumericExtensions
{
    /// <summary>Máximo de casas decimais suportado pelo tipo <see cref="decimal"/>.</summary>
    public const int MaxDecimals = 28;

    private const char NonBreakingSpace = (char)0xA0;
    private const int MaxParseLength = 64;

    private static CultureInfo BrazilianCulture => Common.Globalization.BrazilianCulture.Instance;

    /// <summary>Formata como moeda brasileira: 1234.5 → "R$ 1.234,50".</summary>
    /// <remarks>O espaço não separável gerado pela cultura é substituído por espaço comum.</remarks>
    public static string ToCurrency(this decimal value) =>
        value.ToString("C2", BrazilianCulture).Replace(NonBreakingSpace, ' ');

    /// <summary>Formata como moeda brasileira: 1234.5 → "R$ 1.234,50".</summary>
    /// <exception cref="ArgumentOutOfRangeException">Valor NaN, infinito ou fora do intervalo de <see cref="decimal"/>.</exception>
    public static string ToCurrency(this double value) => ToDecimal(value).ToCurrency();

    /// <summary>Formata o número no padrão brasileiro: 1234.5 → "1.234,50".</summary>
    public static string ToBrazilianNumber(this decimal value, int decimals = 2)
    {
        ValidateDecimals(decimals);
        return value.ToString($"N{decimals}", BrazilianCulture);
    }

    /// <summary>Formata como percentual. O valor deve estar em fração: 0.1575 → "15,75%".</summary>
    public static string ToPercentage(this decimal value, int decimals = 2)
    {
        ValidateDecimals(decimals);
        return value.ToString($"P{decimals}", BrazilianCulture).Replace(NonBreakingSpace.ToString(), string.Empty).Replace(" ", string.Empty);
    }

    /// <summary>Arredondamento comercial (meio para cima): 2.345 → 2.35.</summary>
    public static decimal RoundHalfUp(this decimal value, int decimals = 2)
    {
        ValidateDecimals(decimals);
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>Arredondamento bancário (meio para o par): 2.345 → 2.34; 2.355 → 2.36.</summary>
    public static decimal RoundBankers(this decimal value, int decimals = 2)
    {
        ValidateDecimals(decimals);
        return Math.Round(value, decimals, MidpointRounding.ToEven);
    }

    /// <summary>Trunca as casas decimais sem arredondar: 2.349 → 2.34.</summary>
    public static decimal Truncate(this decimal value, int decimals = 2)
    {
        ValidateDecimals(decimals);
        return Math.Round(value, decimals, MidpointRounding.ToZero);
    }

    /// <summary>Calcula o percentual do valor: 200.PercentOf(15) → 30.</summary>
    /// <exception cref="OverflowException">Resultado fora do intervalo de <see cref="decimal"/>.</exception>
    public static decimal PercentOf(this decimal value, decimal percent) => value / 100m * percent;

    /// <summary>Valor monetário por extenso: 1234.56 → "mil duzentos e trinta e quatro reais e cinquenta e seis centavos".</summary>
    public static string ToCurrencyWords(this decimal value) => NumberToWordsConverter.ToCurrencyWords(value);

    /// <summary>Número inteiro por extenso: 21 → "vinte e um".</summary>
    public static string ToWords(this long value) => NumberToWordsConverter.ToWords(value);

    /// <summary>Número inteiro por extenso: 21 → "vinte e um".</summary>
    public static string ToWords(this int value) => NumberToWordsConverter.ToWords(value);

    /// <summary>Tenta converter texto no padrão brasileiro ("1.234,56" ou "R$ 1.234,56") para decimal.</summary>
    /// <remarks>
    /// O ponto (separador de milhar) só é aceito entre grupos de 3 dígitos da parte inteira: "1.234,56" e "1234,56"
    /// são válidos; "1.5", "1.2.3" e "12.34" são recusados (em vez de virarem 15, 123 e 1234).
    /// Observação: "1.234" é sempre interpretado como mil duzentos e trinta e quatro.
    /// </remarks>
    public static bool TryParseBrazilianDecimal(this string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxParseLength)
            return false;

        var clean = value.Replace(NonBreakingSpace, ' ').AsSpan().Trim();

        // Sinal opcional e, depois dele, o símbolo "R$" uma única vez ("R$ 1,00", "-R$ 1,00", "- 1,00"); nada no meio ou no fim
        bool negative = clean.StartsWith("-");
        if (negative || clean.StartsWith("+"))
            clean = clean[1..].TrimStart();
        if (clean.StartsWith("R$", StringComparison.OrdinalIgnoreCase))
            clean = clean[2..].TrimStart();
        if (clean.IsEmpty || !NumberGrouping.HasValidGrouping(clean, BrazilianCulture.NumberFormat))
            return false;

        if (!decimal.TryParse(clean, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, BrazilianCulture, out var parsed))
            return false;

        result = negative ? -parsed : parsed;
        return true;
    }

    private static void ValidateDecimals(int decimals) => Guard.InRange(decimals, 0, MaxDecimals, nameof(decimals));

    private static decimal ToDecimal(double value)
    {
        // (double)decimal.MaxValue arredonda para 2^96, que já não cabe em decimal: os próprios limites ficam de fora
        if (!double.IsFinite(value) || value >= (double)decimal.MaxValue || value <= (double)decimal.MinValue)
            throw new ArgumentOutOfRangeException(nameof(value), "O valor deve ser um número finito dentro do intervalo de decimal.");

        return (decimal)value;
    }
}
