using TEC.Core.Text.Internal;

namespace TEC.Core.Text.Formatting;

/// <summary>
/// Formatação de documentos e dados brasileiros.
/// Quando o valor não possui a quantidade de caracteres esperada, é retornado sem alteração.
/// </summary>
public static class DocumentFormatter
{
    /// <summary>Formata CPF: 000.000.000-00.</summary>
    public static string FormatCpf(string? cpf)
    {
        var digits = DocumentNormalizer.Digits(cpf);
        return digits.Length == 11 ? MaskFormatter.Apply(digits, "###.###.###-##") : cpf ?? string.Empty;
    }

    /// <summary>Formata CNPJ (numérico ou alfanumérico): AA.AAA.AAA/AAAA-00.</summary>
    public static string FormatCnpj(string? cnpj)
    {
        var value = DocumentNormalizer.AlphaNumericUpper(cnpj);
        return value.Length == 14 ? MaskFormatter.Apply(value, "##.###.###/####-##") : cnpj ?? string.Empty;
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
    public static string FormatCep(string? cep)
    {
        var digits = DocumentNormalizer.Digits(cep);
        return digits.Length == 8 ? MaskFormatter.Apply(digits, "#####-###") : cep ?? string.Empty;
    }

    /// <summary>Formata PIS/PASEP/NIT: 000.00000.00-0.</summary>
    public static string FormatPis(string? pis)
    {
        var digits = DocumentNormalizer.Digits(pis);
        return digits.Length == 11 ? MaskFormatter.Apply(digits, "###.#####.##-#") : pis ?? string.Empty;
    }

    /// <summary>
    /// Formata telefone brasileiro:
    /// 8 dígitos → 0000-0000; 9 → 00000-0000; 10 → (00) 0000-0000; 11 → (00) 00000-0000;
    /// 12/13 com prefixo 55 → +55 (00) 0000-0000 / +55 (00) 00000-0000.
    /// </summary>
    public static string FormatPhone(string? phone)
    {
        var digits = DocumentNormalizer.Digits(phone);
        return digits.Length switch
        {
            8 => MaskFormatter.Apply(digits, "####-####"),
            9 => MaskFormatter.Apply(digits, "#####-####"),
            10 => MaskFormatter.Apply(digits, "(##) ####-####"),
            11 => MaskFormatter.Apply(digits, "(##) #####-####"),
            12 when digits.StartsWith("55", StringComparison.Ordinal) => MaskFormatter.Apply(digits, "+## (##) ####-####"),
            13 when digits.StartsWith("55", StringComparison.Ordinal) => MaskFormatter.Apply(digits, "+## (##) #####-####"),
            _ => phone ?? string.Empty
        };
    }

    /// <summary>Remove a máscara, mantendo apenas letras e dígitos (em maiúsculas).</summary>
    public static string RemoveMask(string? value) => DocumentNormalizer.AlphaNumericUpper(value);
}
