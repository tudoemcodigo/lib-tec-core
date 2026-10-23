using FsCheck;
using FsCheck.Fluent;

namespace TEC.Core.Tests.Security.Fuzzing;

/// <summary>
/// Geradores de entradas hostis para fuzzing: textos montados com pedaços que costumam quebrar parsers e validadores
/// (separadores, aspas, quebras de linha, caracteres de fórmula, espaços Unicode, BOM, NUL, emojis, marcas de combinação).
/// </summary>
internal static class Hostile
{
    private static readonly string[] Tokens =
    [
        "a", "Z", "0", "9", "ç", "ã", "É", " ", "  ", ";", ",", "|", "\t", "\"", "\"\"", "'", "\r", "\n", "\r\n",
        "=", "+", "-", "@", "＝", "＋", "－", "＠", "\u00A0", "\u2028", "\u3000", "\uFEFF", "\0", "\u0301", "\u202E",
        "😀", "🇧🇷", "R$", "1.234,56", "-1", "1E999", "NaN", "{", "}", "[", "]", ":", "null", "true", "\\", "/",
        "<script>", "%00", "01/01/2026", "2026-13-45", "3550308", "SP", "-----BEGIN PUBLIC KEY-----",
    ];

    // Caracteres isolados (sem surrogates, que não existem sozinhos em UTF-8 válido)
    private static readonly char[] Chars = [.. Tokens.Where(t => t.Length == 1).Select(t => t[0])];

    /// <summary>Texto UTF-16 válido (sem surrogates isolados), de 0 a ~100 pedaços.</summary>
    public static Gen<string> Text { get; } = Gen.Elements(Tokens).ListOf().Select(string.Concat);

    /// <summary>Texto que pode conter surrogates isolados (UTF-16 inválido), como vem de entradas corrompidas.</summary>
    public static Gen<string> AnyText { get; } = Gen.OneOf(
        Text,
        Text.Select(s => s + "\uD800"),
        Text.Select(s => "\uDC00" + s),
        Gen.Elements(Tokens).ListOf().Select(parts => string.Join("\uD83D", parts)));

    /// <summary>Caractere isolado hostil.</summary>
    public static Gen<char> Char { get; } = Gen.Elements(Chars);

    /// <summary>Bytes aleatórios.</summary>
    public static Gen<byte[]> Bytes(int maxLength) =>
        Gen.Choose(0, maxLength).SelectMany(length => Gen.Choose(0, 255).ArrayOf(length).Select(values => values.Select(v => (byte)v).ToArray()));

    /// <summary>Configuração padrão: falha com o contraexemplo na mensagem; <paramref name="maxTest"/> casos por propriedade.</summary>
    public static Config Config(int maxTest = 300) => FsCheck.Config.QuickThrowOnFailure.WithMaxTest(maxTest).WithQuietOnSuccess(true);

    /// <summary>Texto visível no relatório de falha (caracteres de controle e invisíveis escapados).</summary>
    public static string Show(string? value) =>
        value is null ? "null" : string.Concat(value.Select(c => c is < ' ' or > '~' ? $"\\u{(int)c:X4}" : c.ToString()));
}
