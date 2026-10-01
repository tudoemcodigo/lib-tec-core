using TEC.Core.Text.Internal;

namespace TEC.Core.Text.Masking;

/// <summary>
/// Mascaramento de dados sensíveis para exibição e logs (LGPD).
/// </summary>
public static class SensitiveDataMasker
{
    /// <summary>Caractere padrão de mascaramento.</summary>
    public const char DefaultMaskChar = '*';

    /// <summary>
    /// Mascara o valor mantendo visíveis os primeiros e/ou últimos caracteres.
    /// </summary>
    /// <example><c>Mask("123456789", 2, 2)</c> retorna <c>"12*****89"</c>.</example>
    public static string Mask(string? value, int visibleStart = 0, int visibleEnd = 0, char maskChar = DefaultMaskChar)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(visibleStart);
        ArgumentOutOfRangeException.ThrowIfNegative(visibleEnd);
        if (char.IsControl(maskChar) || char.IsSurrogate(maskChar))
            throw new ArgumentException("Caractere de máscara inválido.", nameof(maskChar));

        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Se o valor for curto demais, mascara tudo para não expor o dado inteiro (soma em long evita estouro)
        if ((long)visibleStart + visibleEnd >= value.Length)
            return new string(maskChar, value.Length);

        // Não corta pares surrogate (emojis e caracteres fora do BMP): se a fronteira cair no meio de um par,
        // o caractere inteiro passa a ser mascarado (nunca fica meio caractere visível, o que geraria texto UTF-16 inválido)
        if (visibleStart > 0 && char.IsHighSurrogate(value[visibleStart - 1]) && char.IsLowSurrogate(value[visibleStart]))
            visibleStart--;
        int endBoundary = value.Length - visibleEnd;
        if (visibleEnd > 0 && char.IsHighSurrogate(value[endBoundary - 1]) && char.IsLowSurrogate(value[endBoundary]))
            visibleEnd--;

        return string.Create(value.Length, (value, visibleStart, visibleEnd, maskChar), static (span, state) =>
        {
            state.value.AsSpan().CopyTo(span);
            span[state.visibleStart..^state.visibleEnd].Fill(state.maskChar);
        });
    }

    /// <summary>Mascara CPF no padrão adotado pelo governo: ***.456.789-**.</summary>
    public static string MaskCpf(string? cpf)
    {
        var digits = DocumentNormalizer.Digits(cpf);
        return digits.Length == 11 ? $"***.{digits[3..6]}.{digits[6..9]}-**" : Mask(cpf);
    }

    /// <summary>Mascara CNPJ: **.345.678/****-**.</summary>
    public static string MaskCnpj(string? cnpj)
    {
        var value = DocumentNormalizer.AlphaNumericUpper(cnpj);
        return value.Length == 14 ? $"**.{value[2..5]}.{value[5..8]}/****-**" : Mask(cnpj);
    }

    /// <summary>Mascara e-mail mantendo os 2 primeiros caracteres e o domínio: jo******@dominio.com.</summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        // O domínio fica visível: com caractere de controle (ex.: quebra de linha) o resultado forjaria linhas de log
        if (email.Any(char.IsControl))
            return Mask(email);

        int at = email.LastIndexOf('@');
        if (at <= 0)
            return Mask(email, 1);

        var local = email[..at];
        var visible = local.Length <= 2 ? 1 : 2;
        return string.Concat(Mask(local, visible), email.AsSpan(at));
    }

    /// <summary>Mascara telefone mantendo DDD e os 4 últimos dígitos: (11) *****-4444.</summary>
    public static string MaskPhone(string? phone)
    {
        var digits = DocumentNormalizer.Digits(phone);
        return digits.Length switch
        {
            10 => $"({digits[..2]}) ****-{digits[^4..]}",
            11 => $"({digits[..2]}) *****-{digits[^4..]}",
            _ => Mask(digits, 0, Math.Min(4, digits.Length / 2))
        };
    }

    /// <summary>Mascara cartão de crédito mantendo os 4 últimos dígitos: **** **** **** 1234.</summary>
    public static string MaskCreditCard(string? cardNumber)
    {
        var digits = DocumentNormalizer.Digits(cardNumber);
        return digits.Length >= 12 ? $"**** **** **** {digits[^4..]}" : Mask(digits);
    }
}
