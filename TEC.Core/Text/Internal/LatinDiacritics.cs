using System.Globalization;
using System.Text;

namespace TEC.Core.Text.Internal;

/// <summary>
/// Tratamento de acentos latinos independente de ICU.
/// </summary>
/// <remarks>
/// Com <c>InvariantGlobalization</c> habilitado, <see cref="string.Normalize(NormalizationForm)"/> não faz nada
/// (sem lançar erro). Esta classe garante o mesmo resultado em qualquer ambiente para as letras acentuadas
/// do Latin-1 (todas as usadas em português), tanto na forma composta ("é") quanto decomposta ("e" + acento).
/// </remarks>
internal static class LatinDiacritics
{
    private static readonly Dictionary<char, char> BaseLetters = [];
    private static readonly Dictionary<(char Base, char Mark), char> Compositions = [];

    static LatinDiacritics()
    {
        // (letra maiúscula acentuada, letra base, acento combinante); as minúsculas ficam 0x20 acima
        (int Code, char Base, int Mark)[] upper =
        [
            (0xC0, 'A', 0x300), (0xC1, 'A', 0x301), (0xC2, 'A', 0x302), (0xC3, 'A', 0x303), (0xC4, 'A', 0x308), (0xC5, 'A', 0x30A),
            (0xC7, 'C', 0x327),
            (0xC8, 'E', 0x300), (0xC9, 'E', 0x301), (0xCA, 'E', 0x302), (0xCB, 'E', 0x308),
            (0xCC, 'I', 0x300), (0xCD, 'I', 0x301), (0xCE, 'I', 0x302), (0xCF, 'I', 0x308),
            (0xD1, 'N', 0x303),
            (0xD2, 'O', 0x300), (0xD3, 'O', 0x301), (0xD4, 'O', 0x302), (0xD5, 'O', 0x303), (0xD6, 'O', 0x308),
            (0xD9, 'U', 0x300), (0xDA, 'U', 0x301), (0xDB, 'U', 0x302), (0xDC, 'U', 0x308),
            (0xDD, 'Y', 0x301)
        ];

        foreach (var (code, baseLetter, mark) in upper)
        {
            Add((char)code, baseLetter, (char)mark);
            Add((char)(code + 0x20), char.ToLowerInvariant(baseLetter), (char)mark);
        }

        Add('ÿ', 'y', '̈'); // ÿ
    }

    /// <summary>Remove acentos: "Ação" / "Ação" → "Acao".</summary>
    public static string RemoveDiacritics(string value)
    {
        // Quando o ICU está disponível, a decomposição cobre também alfabetos além do Latin-1
        var decomposed = Decompose(value);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(BaseLetters.TryGetValue(c, out var baseLetter) ? baseLetter : c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Compõe letra + acento combinante na forma pré-composta ("e" + "́" → "é").
    /// Resultado idêntico com ou sem ICU (equivalente ao NFC para letras do Latin-1).
    /// </summary>
    public static string Compose(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i + 1 < value.Length && Compositions.TryGetValue((current, value[i + 1]), out var composed))
            {
                builder.Append(composed);
                i++;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    // Texto UTF-16 inválido (surrogate isolado) faz o Normalize lançar com ICU e não fazer nada sem ICU: segue sem
    // decompor, como no ambiente sem ICU (as letras do Latin-1 são tratadas pela tabela)
    private static string Decompose(string value)
    {
        try
        {
            return value.Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            return value;
        }
    }

    private static void Add(char precomposed, char baseLetter, char mark)
    {
        BaseLetters[precomposed] = baseLetter;
        Compositions[(baseLetter, mark)] = precomposed;
    }
}
