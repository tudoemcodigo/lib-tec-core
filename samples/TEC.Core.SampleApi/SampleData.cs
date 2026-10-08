using TEC.Core.Dates.Holidays;

namespace TEC.Core.SampleApi;

/// <summary>Dados sintéticos da API de exemplo (feriados por município e códigos IBGE válidos).</summary>
public static class SampleData
{
    /// <summary>Códigos IBGE das 27 UFs.</summary>
    public static readonly int[] UfCodes = [11, 12, 13, 14, 15, 16, 17, 21, 22, 23, 24, 25, 26, 27, 28, 29, 31, 32, 33, 35, 41, 42, 43, 50, 51, 52, 53];

    /// <summary>Código IBGE sintético (7 dígitos, prefixo da UF) do município <paramref name="index"/> da UF.</summary>
    public static int IbgeCode(int ufCode, int index) => ufCode * 100_000 + 10 + index;

    /// <summary>
    /// Feriados estaduais e municipais sintéticos: um estadual por UF e dois por município, espalhados pelo ano.
    /// Com 200 municípios por UF são ~10,8 mil feriados, ordem de grandeza de um cadastro nacional real.
    /// </summary>
    public static IEnumerable<Holiday> Holidays(int municipalitiesPerUf, int year)
    {
        foreach (int uf in UfCodes)
        {
            var stateDate = new DateOnly(year, 1, 1).AddDays(uf * 7 % 360);
            yield return new Holiday(stateDate, $"Data magna da UF {uf}") { Uf = UfOf(uf) };

            for (int i = 0; i < municipalitiesPerUf; i++)
            {
                int code = IbgeCode(uf, i);
                yield return new Holiday(new DateOnly(year, 1, 1).AddDays((code * 31) % 365), "Aniversário do município") { IbgeCode = code };
                yield return new Holiday(new DateOnly(year, 1, 1).AddDays((code * 17 + 90) % 365), "Padroeiro do município") { IbgeCode = code };
            }
        }
    }

    // Sigla da UF a partir do código (HolidayLocation deduz a UF do código IBGE do município)
    private static string UfOf(int ufCode) => HolidayLocation.FromIbgeCode(IbgeCode(ufCode, 0)).Uf!;
}
