using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using TEC.Core.Text.Internal;

namespace TEC.Core.Text.Validation;

/// <summary>
/// Validação de documentos e dados brasileiros. Aceita valores com ou sem máscara.
/// </summary>
public static partial class DocumentValidator
{
    // Tamanho máximo aceito para documentos com máscara (ex.: "12.ABC.345/01DE-35" tem 18 caracteres)
    private const int MaxDocumentLength = 32;

    // DDDs válidos no Brasil (Anatel)
    private static readonly HashSet<int> ValidAreaCodes =
    [
        11, 12, 13, 14, 15, 16, 17, 18, 19, 21, 22, 24, 27, 28, 31, 32, 33, 34, 35, 37, 38,
        41, 42, 43, 44, 45, 46, 47, 48, 49, 51, 53, 54, 55, 61, 62, 63, 64, 65, 66, 67, 68, 69,
        71, 73, 74, 75, 77, 79, 81, 82, 83, 84, 85, 86, 87, 88, 89, 91, 92, 93, 94, 95, 96, 97, 98, 99
    ];

    /// <summary>Valida um CPF (dígitos verificadores e sequências repetidas).</summary>
    public static bool IsValidCpf(string? cpf)
    {
        if (!HasOnlyNumericMaskCharacters(cpf))
            return false;

        var digits = DocumentNormalizer.Digits(cpf);
        if (digits.Length != 11 || AllCharsEqual(digits))
            return false;

        return CheckDigitCalculator.CalculateCpf(digits.AsSpan(0, 9)) == digits[9..];
    }

    /// <summary>
    /// Valida um CNPJ numérico ou alfanumérico (novo formato a partir de julho/2026).
    /// </summary>
    public static bool IsValidCnpj(string? cnpj)
    {
        if (!HasOnlyMaskCharacters(cnpj))
            return false;

        var value = DocumentNormalizer.AlphaNumericUpper(cnpj);
        if (value.Length != 14 || AllCharsEqual(value))
            return false;

        // Os 12 primeiros caracteres podem ser alfanuméricos; os 2 dígitos verificadores são sempre numéricos
        if (!char.IsAsciiDigit(value[12]) || !char.IsAsciiDigit(value[13]))
            return false;

        return CheckDigitCalculator.CalculateCnpj(value.AsSpan(0, 12)) == value[12..];
    }

    /// <summary>Valida o documento como CPF ou CNPJ, conforme a quantidade de caracteres.</summary>
    public static bool IsValidCpfOrCnpj(string? document)
    {
        if (document is null || document.Length > MaxDocumentLength)
            return false;

        var length = DocumentNormalizer.AlphaNumericUpper(document).Length;
        return length switch
        {
            11 => IsValidCpf(document),
            14 => IsValidCnpj(document),
            _ => false
        };
    }

    /// <summary>Valida um PIS/PASEP/NIT.</summary>
    public static bool IsValidPis(string? pis)
    {
        if (!HasOnlyNumericMaskCharacters(pis))
            return false;

        var digits = DocumentNormalizer.Digits(pis);
        if (digits.Length != 11 || AllCharsEqual(digits))
            return false;

        return CheckDigitCalculator.CalculatePis(digits.AsSpan(0, 10)) == digits[10] - '0';
    }

    /// <summary>Valida um CEP (8 dígitos, não repetidos).</summary>
    public static bool IsValidCep(string? cep)
    {
        // Tamanho e caracteres conferidos antes de qualquer alocação (entrada não confiável pode ser enorme)
        if (!HasOnlyNumericMaskCharacters(cep))
            return false;

        var digits = DocumentNormalizer.Digits(cep);
        return digits.Length == 8 && !AllCharsEqual(digits);
    }

    /// <summary>
    /// Valida um telefone brasileiro com DDD: fixo (10 dígitos) ou celular (11 dígitos iniciando com 9).
    /// Aceita o prefixo do país (+55).
    /// </summary>
    public static bool IsValidPhone(string? phone)
    {
        if (!HasOnlyPhoneMaskCharacters(phone))
            return false;

        var digits = DocumentNormalizer.Digits(phone);
        if (digits.Length is 12 or 13 && digits.StartsWith("55", StringComparison.Ordinal))
            digits = digits[2..];

        if (digits.Length is not (10 or 11))
            return false;

        if (!ValidAreaCodes.Contains((digits[0] - '0') * 10 + (digits[1] - '0')))
            return false;

        return digits.Length == 10
            ? digits[2] is >= '2' and <= '5'
            : digits[2] == '9';
    }

    /// <summary>
    /// Valida o formato de um e-mail (RFC 5321): até 254 caracteres, parte local até 64,
    /// sem pontos no início, no fim ou consecutivos.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !EmailRegex().IsMatch(email))
            return false;

        var local = email.AsSpan(0, email.IndexOf('@'));
        return local.Length <= 64 && local[0] != '.' && local[^1] != '.' && !local.Contains("..", StringComparison.Ordinal);
    }

    private static bool AllCharsEqual(string value) => value.AsSpan().IndexOfAnyExcept(value[0]) < 0;

    // Os conjuntos de caracteres aceitos vêm do DocumentNormalizer, os mesmos usados pelo DocumentFormatter:
    // validar e formatar nunca divergem sobre o que é máscara válida

    // Garante que o valor contenha apenas caracteres de documento e de máscara (evita aceitar "abc123.456...")
    private static bool HasOnlyMaskCharacters(string? value) =>
        IsWithinLength(value) && DocumentNormalizer.HasOnlyMaskCharacters(value);

    // Mesma verificação para documentos exclusivamente numéricos (CPF, PIS, CEP)
    private static bool HasOnlyNumericMaskCharacters(string? value) =>
        IsWithinLength(value) && DocumentNormalizer.HasOnlyNumericMaskCharacters(value);

    private static bool HasOnlyPhoneMaskCharacters(string? value) =>
        IsWithinLength(value) && DocumentNormalizer.HasOnlyPhoneMaskCharacters(value);

    private static bool IsWithinLength([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxDocumentLength;

    // Observação: \z (fim absoluto) em vez de $, que em .NET aceita uma quebra de linha no final
    [GeneratedRegex(@"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+\z", RegexOptions.NonBacktracking)]
    private static partial Regex EmailRegex();
}
