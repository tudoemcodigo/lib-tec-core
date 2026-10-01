using System.Globalization;

namespace TEC.Core.Common.Globalization;

/// <summary>
/// Validação do separador de milhar em textos numéricos.
/// </summary>
/// <remarks>
/// O <c>decimal.TryParse</c> com <see cref="NumberStyles.AllowThousands"/> aceita o separador de milhar em qualquer
/// posição: em pt-BR, "1.5" vira 15 e "1.2.3" vira 123, o que corrompe valores digitados no padrão americano.
/// Esta validação só aceita o separador entre grupos completos (ex.: "1.234.567,89"), conforme
/// <see cref="NumberFormatInfo.NumberGroupSizes"/> da cultura (3 dígitos em pt-BR).
/// </remarks>
internal static class NumberGrouping
{
    /// <summary>
    /// Indica se o texto não usa o separador de milhar da cultura ou se o usa somente entre grupos válidos
    /// da parte inteira. Não valida o restante do número (isso fica a cargo do <c>TryParse</c>).
    /// </summary>
    public static bool HasValidGrouping(ReadOnlySpan<char> text, NumberFormatInfo format)
    {
        var separator = format.NumberGroupSeparator;
        if (string.IsNullOrEmpty(separator) || text.IndexOf(separator, StringComparison.Ordinal) < 0)
            return true;

        // Parte inteira: do primeiro dígito até o primeiro caractere que não seja dígito nem separador de milhar
        int start = text.IndexOfAnyInRange('0', '9');
        if (start < 0)
            return false;

        int end = start;
        while (end < text.Length)
        {
            if (char.IsAsciiDigit(text[end]))
                end++;
            else if (text[end..].StartsWith(separator, StringComparison.Ordinal))
                end += separator.Length;
            else
                break;
        }

        // O separador não pode aparecer antes dos dígitos nem depois da parte inteira (ex.: nas decimais)
        if (text[..start].IndexOf(separator, StringComparison.Ordinal) >= 0
            || text[end..].IndexOf(separator, StringComparison.Ordinal) >= 0)
            return false;

        return HasValidGroups(text[start..end], separator, format.NumberGroupSizes);
    }

    // Confere os grupos da direita para a esquerda: cada grupo deve ter exatamente o tamanho da cultura
    // (o último tamanho se repete; 0 = sem agrupamento a partir dali) e o grupo mais à esquerda, de 1 até esse tamanho
    private static bool HasValidGroups(ReadOnlySpan<char> integerPart, string separator, int[] groupSizes)
    {
        if (groupSizes.Length == 0)
            return false;

        int sizeIndex = 0;
        var remaining = integerPart;
        while (true)
        {
            int last = remaining.LastIndexOf(separator, StringComparison.Ordinal);
            var group = last < 0 ? remaining : remaining[(last + separator.Length)..];
            int expected = groupSizes[Math.Min(sizeIndex, groupSizes.Length - 1)];

            if (last < 0)
                return group.Length >= 1 && (expected == 0 || group.Length <= expected);

            if (expected == 0 || group.Length != expected)
                return false;

            remaining = remaining[..last];
            sizeIndex++;
        }
    }
}
