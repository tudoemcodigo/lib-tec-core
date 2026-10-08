using TEC.Core.Text.Internal;

namespace TEC.Core.Text.Formatting;

/// <summary>
/// Formatação de documentos e dados brasileiros.
/// Quando o valor não possui a quantidade de caracteres esperada, ou contém caracteres que não são do documento
/// nem de máscara (ex.: letras em CPF), é retornado sem alteração — nada é descartado em silêncio.
/// </summary>
/// <remarks>
/// Caracteres de máscara aceitos (os mesmos do <see cref="Validation.DocumentValidator"/>): <c>.</c>, <c>-</c> e espaço
/// em CPF, PIS e CEP; também <c>/</c> e letras ASCII no CNPJ; <c>+</c>, <c>(</c>, <c>)</c>, <c>.</c>, <c>-</c> e espaço
/// no telefone.
/// </remarks>
public static class DocumentFormatter
{
    /// <summary>Formata CPF: 000.000.000-00.</summary>
    public static string FormatCpf(string? cpf) =>
        FormatNumeric(cpf, 11, "###.###.###-##");

    /// <summary>Formata CNPJ (numérico ou alfanumérico): AA.AAA.AAA/AAAA-00.</summary>
    public static string FormatCnpj(string? cnpj)
    {
        if (string.IsNullOrEmpty(cnpj) || !DocumentNormalizer.HasOnlyMaskCharacters(cnpj))
            return cnpj ?? string.Empty;

        var value = DocumentNormalizer.AlphaNumericUpper(cnpj);
        return value.Length == 14 ? MaskFormatter.Apply(value, "##.###.###/####-##") : cnpj;
    }

    /// <summary>Formata como CPF ou CNPJ conforme a quantidade de caracteres.</summary>
    public static string FormatCpfOrCnpj(string? document) =>
        DocumentNormalizer.AlphaNumericUpper(document).Length switch
        {
            11 => FormatCpf(document),
            14 => FormatCnpj(document),
            _ => document ?? string.Empty
        };

    /// <summary>Formata CEP: 00000-000.</summary>
    public static string FormatCep(string? cep) =>
        FormatNumeric(cep, 8, "#####-###");

    /// <summary>Formata PIS/PASEP/NIT: 000.00000.00-0.</summary>
    public static string FormatPis(string? pis) =>
        FormatNumeric(pis, 11, "###.#####.##-#");

    /// <summary>
    /// Formata telefone brasileiro:
    /// 8 dígitos → 0000-0000; 9 → 00000-0000; 10 → (00) 0000-0000; 11 → (00) 00000-0000;
    /// 12/13 com prefixo 55 → +55 (00) 0000-0000 / +55 (00) 00000-0000.
    /// </summary>
    public static string FormatPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || !DocumentNormalizer.HasOnlyPhoneMaskCharacters(phone))
            return phone ?? string.Empty;

        var digits = DocumentNormalizer.Digits(phone);
        return digits.Length switch
        {
            8 => MaskFormatter.Apply(digits, "####-####"),
            9 => MaskFormatter.Apply(digits, "#####-####"),
            10 => MaskFormatter.Apply(digits, "(##) ####-####"),
            11 => MaskFormatter.Apply(digits, "(##) #####-####"),
            12 when digits.StartsWith("55", StringComparison.Ordinal) => MaskFormatter.Apply(digits, "+## (##) ####-####"),
            13 when digits.StartsWith("55", StringComparison.Ordinal) => MaskFormatter.Apply(digits, "+## (##) #####-####"),
            _ => phone
        };
    }

    /// <summary>Remove a máscara, mantendo apenas letras e dígitos (em maiúsculas).</summary>
    public static string RemoveMask(string? value) => DocumentNormalizer.AlphaNumericUpper(value);

    private static string FormatNumeric(string? value, int length, string mask)
    {
        if (string.IsNullOrEmpty(value) || !DocumentNormalizer.HasOnlyNumericMaskCharacters(value))
            return value ?? string.Empty;

        var digits = DocumentNormalizer.Digits(value);
        return digits.Length == length ? MaskFormatter.Apply(digits, mask) : value;
    }
}
