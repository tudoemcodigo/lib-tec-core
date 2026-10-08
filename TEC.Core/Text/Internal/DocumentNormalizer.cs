using System.Buffers;

namespace TEC.Core.Text.Internal;

/// <summary>
/// Normalização de documentos (remoção de máscara) antes de validar/formatar.
/// </summary>
internal static class DocumentNormalizer
{
    private static readonly SearchValues<char> NumericMaskCharacters = SearchValues.Create("0123456789. -");
    private static readonly SearchValues<char> PhoneMaskCharacters = SearchValues.Create("0123456789+(). -");

    /// <summary>Mantém apenas dígitos.</summary>
    public static string Digits(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value.Where(char.IsAsciiDigit));

    /// <summary>Mantém apenas letras ASCII e dígitos, convertendo letras para maiúsculas (usado no CNPJ alfanumérico).</summary>
    public static string AlphaNumericUpper(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Concat(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant));

    // Fonte única dos caracteres de máscara: DocumentValidator e DocumentFormatter usam estes métodos, para que formatar
    // nunca descarte em silêncio caracteres que a validação recusaria ("abc12345678901" não é um CPF com máscara).
    // Apenas espaço comum é aceito (não \r, \n ou \t).

    /// <summary>Só dígitos e caracteres de máscara numérica (<c>. -</c> e espaço): CPF, PIS, CEP.</summary>
    public static bool HasOnlyNumericMaskCharacters(string value) =>
        value.AsSpan().IndexOfAnyExcept(NumericMaskCharacters) < 0;

    /// <summary>Só letras ASCII, dígitos e caracteres de máscara (<c>. / -</c> e espaço): CNPJ alfanumérico.</summary>
    public static bool HasOnlyMaskCharacters(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '/' or '-' or ' '))
                return false;
        }

        return true;
    }

    /// <summary>Só dígitos e caracteres de máscara de telefone (<c>+ ( ) . -</c> e espaço).</summary>
    public static bool HasOnlyPhoneMaskCharacters(string value) =>
        value.AsSpan().IndexOfAnyExcept(PhoneMaskCharacters) < 0;
}
