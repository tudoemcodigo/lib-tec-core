using System.Text;

namespace TEC.Core.Text.Formatting;

/// <summary>
/// Aplicação de máscaras genéricas.
/// </summary>
public static class MaskFormatter
{
    /// <summary>Caractere que representa uma posição a ser preenchida pelo valor.</summary>
    public const char Placeholder = '#';

    /// <summary>
    /// Aplica uma máscara ao valor. Cada <c>#</c> é substituído pelo próximo caractere do valor;
    /// os demais caracteres da máscara são mantidos literalmente.
    /// </summary>
    /// <example><c>MaskFormatter.Apply("12345678", "#####-###")</c> retorna <c>"12345-678"</c>.</example>
    /// <remarks>Se o valor for menor que a máscara, a formatação é interrompida no último caractere disponível.</remarks>
    public static string Apply(string? value, string mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(mask.Length);
        int valueIndex = 0;

        foreach (var maskChar in mask)
        {
            if (valueIndex >= value.Length)
                break;

            builder.Append(maskChar == Placeholder ? value[valueIndex++] : maskChar);
        }

        return builder.ToString();
    }
}
