namespace TEC.Core.Numbers.Words;

/// <summary>
/// Conversão de números e valores monetários para extenso em português (Brasil).
/// </summary>
public static class NumberToWordsConverter
{
    /// <summary>Maior valor suportado (999 trilhões...).</summary>
    public const long MaxValue = 999_999_999_999_999;

    private static readonly string[] Units =
    [
        "zero", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove",
        "dez", "onze", "doze", "treze", "catorze", "quinze", "dezesseis", "dezessete", "dezoito", "dezenove"
    ];

    private static readonly string[] Tens =
        ["", "", "vinte", "trinta", "quarenta", "cinquenta", "sessenta", "setenta", "oitenta", "noventa"];

    private static readonly string[] Hundreds =
        ["", "cento", "duzentos", "trezentos", "quatrocentos", "quinhentos", "seiscentos", "setecentos", "oitocentos", "novecentos"];

    private static readonly (string Singular, string Plural)[] Scales =
    [
        ("", ""), ("mil", "mil"), ("milhão", "milhões"), ("bilhão", "bilhões"), ("trilhão", "trilhões")
    ];

    /// <summary>Converte um número inteiro para extenso: 1234 → "mil duzentos e trinta e quatro".</summary>
    public static string ToWords(long number)
    {
        if (number is > MaxValue or < -MaxValue)
            throw new ArgumentOutOfRangeException(nameof(number), $"O valor deve estar entre -{MaxValue} e {MaxValue}.");

        if (number == 0)
            return Units[0];

        return number < 0 ? $"menos {IntegerToWords(-number)}" : IntegerToWords(number);
    }

    /// <summary>
    /// Converte um valor monetário (R$) para extenso:
    /// 1234.56 → "mil duzentos e trinta e quatro reais e cinquenta e seis centavos".
    /// </summary>
    /// <remarks>O valor é arredondado para 2 casas decimais (arredondamento comercial).</remarks>
    public static string ToCurrencyWords(decimal value)
    {
        value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        bool negative = value < 0;
        value = Math.Abs(value);

        if (value > MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), $"O valor deve ser no máximo {MaxValue}.");

        long reais = (long)Math.Truncate(value);
        int centavos = (int)((value - reais) * 100);

        var parts = new List<string>(2);
        if (reais > 0)
        {
            // "de reais" quando o valor termina em milhão/bilhão/trilhão exato: "um milhão de reais"
            var connector = reais >= 1_000_000 && reais % 1_000_000 == 0 ? " de " : " ";
            parts.Add(IntegerToWords(reais) + connector + (reais == 1 ? "real" : "reais"));
        }

        if (centavos > 0)
            parts.Add(IntegerToWords(centavos) + (centavos == 1 ? " centavo" : " centavos"));

        if (parts.Count == 0)
            return "zero real";

        var result = string.Join(" e ", parts);
        return negative ? $"menos {result}" : result;
    }

    private static string IntegerToWords(long number)
    {
        // Separa o número em grupos de 3 dígitos (unidades, milhares, milhões...)
        var groups = new List<(int Value, int Scale)>();
        for (int scale = 0; number > 0; scale++, number /= 1000)
        {
            int value = (int)(number % 1000);
            if (value > 0)
                groups.Add((value, scale));
        }

        groups.Reverse();
        var parts = new List<string>(groups.Count);

        for (int i = 0; i < groups.Count; i++)
        {
            var (value, scale) = groups[i];
            string words = scale == 1 && value == 1 ? "mil" : GroupToWords(value) + ScaleSuffix(value, scale);

            if (i > 0)
            {
                // Usa "e" antes do último grupo quando ele é menor que 100 ou centena exata: "mil e cem", "mil e vinte"
                bool isLast = i == groups.Count - 1;
                parts.Add(isLast && (value < 100 || value % 100 == 0) ? " e " : " ");
            }

            parts.Add(words);
        }

        return string.Concat(parts);
    }

    private static string ScaleSuffix(int value, int scale)
    {
        if (scale == 0)
            return string.Empty;

        var (singular, plural) = Scales[scale];
        return " " + (value == 1 ? singular : plural);
    }

    private static string GroupToWords(int value)
    {
        if (value == 100)
            return "cem";

        int hundreds = value / 100;
        int rest = value % 100;
        var parts = new List<string>(3);

        if (hundreds > 0)
            parts.Add(Hundreds[hundreds]);

        if (rest > 0)
        {
            if (rest < 20)
            {
                parts.Add(Units[rest]);
            }
            else
            {
                int units = rest % 10;
                parts.Add(units > 0 ? $"{Tens[rest / 10]} e {Units[units]}" : Tens[rest / 10]);
            }
        }

        return string.Join(" e ", parts);
    }
}
