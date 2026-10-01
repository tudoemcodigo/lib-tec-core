using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TEC.Core.Text.Internal;

namespace TEC.Core.Text.Extensions;

/// <summary>
/// Extensões de manipulação de strings.
/// </summary>
public static partial class StringExtensions
{
    private static CultureInfo BrazilianCulture => Common.Globalization.BrazilianCulture.Instance;

    // Palavras que permanecem em minúsculo no Title Case em português (exceto no início)
    private static readonly HashSet<string> LowercaseConnectors =
        new(["a", "à", "as", "às", "o", "os", "e", "de", "da", "das", "do", "dos", "em", "na", "nas", "no", "nos", "com", "por", "para"],
            StringComparer.OrdinalIgnoreCase);

    /// <summary>Indica se a string possui conteúdo (não nula, não vazia e não só espaços).</summary>
    public static bool HasValue(this string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>Retorna <c>null</c> se a string for nula, vazia ou só espaços; caso contrário, retorna o valor sem espaços nas pontas.</summary>
    public static string? NullIfWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Remove acentos e cedilha: "Ação" → "Acao".</summary>
    /// <remarks>Funciona também com <c>InvariantGlobalization</c> (sem ICU) para todas as letras do português.</remarks>
    public static string RemoveAccents(this string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : LatinDiacritics.RemoveDiacritics(value);

    /// <summary>Mantém apenas os dígitos: "(11) 9999-0000" → "1199990000".</summary>
    public static string OnlyDigits(this string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value.Where(char.IsAsciiDigit));

    /// <summary>Mantém apenas letras e dígitos (inclusive acentuados).</summary>
    public static string OnlyLettersAndDigits(this string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value.Where(char.IsLetterOrDigit));

    /// <summary>Substitui sequências de espaços em branco por um único espaço e remove espaços nas pontas.</summary>
    public static string CollapseWhitespace(this string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : WhitespaceRegex().Replace(value, " ").Trim();

    /// <summary>Trunca a string no tamanho máximo, acrescentando o sufixo (incluído no tamanho).</summary>
    /// <remarks>Nunca corta um emoji/caractere especial ao meio (par surrogate).</remarks>
    public static string Truncate(this string? value, int maxLength, string suffix = "...")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        ArgumentNullException.ThrowIfNull(suffix);
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? string.Empty;

        if (suffix.Length >= maxLength)
            return value[..SafeEnd(value, maxLength)];

        return string.Concat(value.AsSpan(0, SafeEnd(value, maxLength - suffix.Length)), suffix);
    }

    /// <summary>Retorna os primeiros <paramref name="length"/> caracteres (sem cortar pares surrogate).</summary>
    public static string Left(this string? value, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        return string.IsNullOrEmpty(value) ? string.Empty : value[..SafeEnd(value, Math.Min(length, value.Length))];
    }

    /// <summary>Retorna os últimos <paramref name="length"/> caracteres (sem cortar pares surrogate).</summary>
    public static string Right(this string? value, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        int start = value.Length - Math.Min(length, value.Length);
        if (start > 0 && start < value.Length && char.IsLowSurrogate(value[start]))
            start++;

        return value[start..];
    }

    // Ajusta o ponto de corte para não separar um par surrogate (ex.: emoji)
    private static int SafeEnd(string value, int end) =>
        end > 0 && end < value.Length && char.IsHighSurrogate(value[end - 1]) ? end - 1 : end;

    /// <summary>Gera um slug para URL: "Promoção de Verão 2026!" → "promocao-de-verao-2026".</summary>
    public static string ToSlug(this string? value)
    {
        var clean = value.RemoveAccents().ToLowerInvariant();
        clean = NonSlugCharsRegex().Replace(clean, " ");
        return WhitespaceRegex().Replace(clean.Trim(), "-");
    }

    /// <summary>
    /// Converte para Title Case em português, mantendo conectivos em minúsculo:
    /// "MARIA DA SILVA E SOUZA" → "Maria da Silva e Souza".
    /// </summary>
    public static string ToTitleCase(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var words = value.CollapseWhitespace().ToLower(BrazilianCulture).Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (i > 0 && LowercaseConnectors.Contains(words[i]))
                continue;

            words[i] = char.ToUpper(words[i][0], BrazilianCulture) + words[i][1..];
        }

        return string.Join(' ', words);
    }

    /// <summary>Converte para PascalCase: "nome do cliente" → "NomeDoCliente".</summary>
    public static string ToPascalCase(this string? value) =>
        string.Concat(SplitWords(value).Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));

    /// <summary>Converte para camelCase: "Nome do Cliente" → "nomeDoCliente".</summary>
    public static string ToCamelCase(this string? value)
    {
        var pascal = value.ToPascalCase();
        return pascal.Length == 0 ? pascal : char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    /// <summary>Converte para snake_case: "NomeDoCliente" → "nome_do_cliente".</summary>
    public static string ToSnakeCase(this string? value) =>
        string.Join('_', SplitWords(value).Select(w => w.ToLowerInvariant()));

    /// <summary>Converte para kebab-case: "NomeDoCliente" → "nome-do-cliente".</summary>
    public static string ToKebabCase(this string? value) =>
        string.Join('-', SplitWords(value).Select(w => w.ToLowerInvariant()));

    /// <summary>Compara duas strings ignorando maiúsculas/minúsculas e acentos.</summary>
    /// <remarks>Não depende de ICU: funciona também com <c>InvariantGlobalization</c>.</remarks>
    public static bool EqualsIgnoreCaseAndAccents(this string? value, string? other)
    {
        if (value is null || other is null)
            return value is null && other is null;

        return string.Equals(value.RemoveAccents(), other.RemoveAccents(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifica se contém o trecho, ignorando maiúsculas/minúsculas e acentos.</summary>
    public static bool ContainsIgnoreCaseAndAccents(this string? value, string? search)
    {
        if (value is null || search is null)
            return false;

        return value.RemoveAccents().Contains(search.RemoveAccents(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Codifica o texto (UTF-8) em Base64.</summary>
    public static string ToBase64(this string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    /// <summary>Decodifica um texto Base64 para UTF-8.</summary>
    /// <exception cref="FormatException">Base64 inválido ou conteúdo que não é UTF-8 válido.</exception>
    public static string FromBase64(this string? value) =>
        value.TryFromBase64(out var result) ? result : throw new FormatException("O valor não é um Base64 de texto UTF-8 válido.");

    /// <summary>Tenta decodificar um texto Base64 para UTF-8. Retorna <c>false</c> se o Base64 ou o UTF-8 forem inválidos.</summary>
    public static bool TryFromBase64(this string? value, out string result)
    {
        result = string.Empty;
        if (string.IsNullOrEmpty(value))
            return true;

        try
        {
            result = StrictUtf8.GetString(Convert.FromBase64String(value));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
        {
            return false;
        }
    }

    // UTF-8 estrito: bytes inválidos geram erro em vez de serem trocados silenciosamente por '�'
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // Separa palavras por caracteres não alfanuméricos e por transições de minúscula/maiúscula
    private static IEnumerable<string> SplitWords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return WordRegex().Matches(value.RemoveAccents()).Select(m => m.Value);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^a-z0-9\s-]|-")]
    private static partial Regex NonSlugCharsRegex();

    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+")]
    private static partial Regex WordRegex();
}
