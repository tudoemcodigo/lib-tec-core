namespace TEC.Core.Text.Internal;

/// <summary>
/// Normalização de documentos (remoção de máscara) antes de validar/formatar.
/// </summary>
internal static class DocumentNormalizer
{
    /// <summary>Mantém apenas dígitos.</summary>
    public static string Digits(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value.Where(char.IsAsciiDigit));

    /// <summary>Mantém apenas letras ASCII e dígitos, convertendo letras para maiúsculas (usado no CNPJ alfanumérico).</summary>
    public static string AlphaNumericUpper(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Concat(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant));
}
