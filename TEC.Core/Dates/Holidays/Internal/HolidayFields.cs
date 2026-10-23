using System.Globalization;
using System.Text;
using TEC.Core.Text.Internal;

namespace TEC.Core.Dates.Holidays.Internal;

internal enum HolidayField
{
    Date,
    Description,
    Uf,
    IbgeCode,
}

/// <summary>
/// Associação de nomes de campo (JSON) e de coluna (banco) aos dados do feriado, e conversão dos valores textuais.
/// </summary>
internal static class HolidayFields
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy"];

    /// <summary>
    /// Campo correspondente ao nome, sem diferenciar maiúsculas, acentos, <c>_</c>, <c>-</c> e espaços.
    /// Aceita nomes em português e em inglês (o formato da BrasilAPI, <c>date</c>/<c>name</c>, é lido diretamente).
    /// </summary>
    public static HolidayField? Resolve(string name) => Normalize(name) switch
    {
        "data" or "date" => HolidayField.Date,
        "descricao" or "description" or "nome" or "name" => HolidayField.Description,
        "uf" => HolidayField.Uf,
        "codigoibge" or "codibge" or "ibge" or "ibgecode" => HolidayField.IbgeCode,
        _ => null,
    };

    /// <summary>Data em <c>aaaa-MM-dd</c> ou <c>dd/MM/aaaa</c>.</summary>
    /// <exception cref="FormatException"></exception>
    public static DateOnly ParseDate(string? value) =>
        DateOnly.TryParseExact(value?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new FormatException("Data inválida: use aaaa-MM-dd ou dd/MM/aaaa.");

    /// <summary>Código IBGE em texto. Vazio resulta em <c>null</c>.</summary>
    /// <exception cref="FormatException"></exception>
    public static int? ParseIbgeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int code)
            ? code
            : throw new FormatException("Código IBGE inválido: informe somente os 7 dígitos.");
    }

    private static string Normalize(string name)
    {
        // LatinDiacritics remove os acentos também com InvariantGlobalization (onde string.Normalize não faz nada)
        var withoutDiacritics = LatinDiacritics.RemoveDiacritics(name);
        var builder = new StringBuilder(withoutDiacritics.Length);
        foreach (char c in withoutDiacritics)
        {
            if (c is '_' or '-' or ' ')
                continue;

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
