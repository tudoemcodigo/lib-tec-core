namespace TEC.Core.Csv.Internal;

/// <summary>
/// Proteção contra injeção de fórmulas (CSV Injection / Formula Injection – OWASP).
/// Textos que o Excel/LibreOffice interpretariam como fórmula recebem um apóstrofo no início.
/// </summary>
internal static class CsvFormulaGuard
{
    private const char Prefix = '\'';

    /// <summary>Indica se o texto começa com caractere que dispara fórmula (inclui as variantes de largura total).</summary>
    public static bool StartsWithFormulaChar(ReadOnlySpan<char> value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '＝' or '＋' or '－' or '＠';

    /// <summary>Neutraliza o valor para escrita.</summary>
    public static string Sanitize(string value) =>
        StartsWithFormulaChar(value) ? Prefix + value : value;

    /// <summary>Remove a neutralização aplicada por <see cref="Sanitize"/> (leitura).</summary>
    public static string Unsanitize(string value) =>
        value.Length > 1 && value[0] == Prefix && StartsWithFormulaChar(value.AsSpan(1)) ? value[1..] : value;
}
