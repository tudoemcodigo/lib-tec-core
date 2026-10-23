namespace TEC.Core.Csv.Internal;

/// <summary>
/// Proteção contra injeção de fórmulas (CSV Injection / Formula Injection – OWASP).
/// Textos que o Excel/LibreOffice interpretariam como fórmula recebem um apóstrofo no início.
/// </summary>
/// <remarks>
/// Para a ida e volta ser exata, textos que já começam com apóstrofo(s) seguido(s) de caractere de fórmula
/// (<c>'=abc</c>) também recebem um apóstrofo a mais na escrita (<c>''=abc</c>); a leitura remove só um.
/// Apóstrofos seguidos de outro caractere (<c>'texto</c>) não são alterados em nenhum sentido.
/// </remarks>
internal static class CsvFormulaGuard
{
    private const char Prefix = '\'';

    /// <summary>Indica se o texto começa com caractere que dispara fórmula (inclui as variantes de largura total).</summary>
    public static bool StartsWithFormulaChar(ReadOnlySpan<char> value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '＝' or '＋' or '－' or '＠';

    /// <summary>Neutraliza o valor para escrita.</summary>
    public static string Sanitize(string value) =>
        StartsWithFormulaChar(value.AsSpan().TrimStart(Prefix)) ? Prefix + value : value;

    /// <summary>Remove a neutralização aplicada por <see cref="Sanitize"/> (leitura).</summary>
    public static string Unsanitize(string value) =>
        value.Length > 1 && value[0] == Prefix && StartsWithFormulaChar(value.AsSpan().TrimStart(Prefix)) ? value[1..] : value;
}
