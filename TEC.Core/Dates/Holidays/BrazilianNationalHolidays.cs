using System.Collections.Concurrent;

namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Feriados nacionais calculados para qualquer ano, sem arquivo nem banco: datas fixas e móveis (a partir da Páscoa).
/// Imutável e seguro para uso concorrente (registre como Singleton).
/// </summary>
/// <remarks>
/// Segue o calendário de dias não úteis adotado pelo mercado financeiro (ANBIMA/FEBRABAN):
/// <list type="bullet">
///   <item>Fixos: 01/01, 21/04, 01/05, 07/09, 12/10 (desde 1980), 02/11, 15/11, 20/11 (desde 2024) e 25/12.</item>
///   <item>Móveis: Carnaval (segunda e terça, Páscoa − 48 e − 47), Paixão de Cristo (Páscoa − 2) e Corpus Christi (Páscoa + 60).</item>
/// </list>
/// Carnaval e Corpus Christi são pontos facultativos na esfera federal; desligue-os com os parâmetros do construtor
/// se a empresa trabalha nesses dias. Quarta-feira de Cinzas (meio expediente) não entra.
/// Além das vigências de 12/10 e 20/11, as regras atuais valem para todos os anos: para datas históricas, cadastre
/// os feriados em uma fonte.
/// </remarks>
/// <example>
/// <code>
/// var calc = new BusinessDayCalculator(new BrazilianNationalHolidays());
/// var easter = BrazilianNationalHolidays.GetEaster(2026); // 05/04/2026
/// </code>
/// </example>
public sealed class BrazilianNationalHolidays : IHolidayProvider
{
    // Anos guardados em cache (cálculo por ano é barato, mas IsHoliday é chamado em laço pela calculadora).
    // Fora da faixa, o ano é recalculado a cada chamada, o que limita a memória com datas extremas.
    private const int MinCachedYear = 1900;
    private const int MaxCachedYear = 2200;

    private readonly ConcurrentDictionary<int, Holiday[]> _cache = new();

    /// <summary>Cria o provedor.</summary>
    /// <param name="includeCarnival">Inclui a segunda e a terça-feira de Carnaval. Padrão: <c>true</c>.</param>
    /// <param name="includeCorpusChristi">Inclui Corpus Christi. Padrão: <c>true</c>.</param>
    public BrazilianNationalHolidays(bool includeCarnival = true, bool includeCorpusChristi = true)
    {
        IncludeCarnival = includeCarnival;
        IncludeCorpusChristi = includeCorpusChristi;
    }

    /// <summary>Indica se a segunda e a terça-feira de Carnaval são feriados.</summary>
    public bool IncludeCarnival { get; }

    /// <summary>Indica se Corpus Christi é feriado.</summary>
    public bool IncludeCorpusChristi { get; }

    /// <summary>Domingo de Páscoa do ano (calendário gregoriano, algoritmo de Meeus/Jones/Butcher).</summary>
    public static DateOnly GetEaster(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, DateOnly.MinValue.Year);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, DateOnly.MaxValue.Year);

        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = ((19 * a) + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        int m = (a + (11 * h) + (22 * l)) / 451;
        int month = (h + l - (7 * m) + 114) / 31;
        int day = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }

    /// <inheritdoc />
    public bool IsHoliday(DateOnly date) => GetHoliday(date) is not null;

    /// <inheritdoc />
    public Holiday? GetHoliday(DateOnly date)
    {
        foreach (var holiday in GetYear(date.Year))
        {
            if (holiday.Date == date)
                return holiday;
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<Holiday> GetHolidays(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, DateOnly.MinValue.Year);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, DateOnly.MaxValue.Year);
        return [.. GetYear(year)];
    }

    /// <inheritdoc />
    public IReadOnlyList<Holiday> GetHolidays(DateOnly start, DateOnly end)
    {
        if (start > end)
            (start, end) = (end, start);

        var result = new List<Holiday>();
        for (int year = start.Year; year <= end.Year; year++)
        {
            foreach (var holiday in GetYear(year))
            {
                if (holiday.Date >= start && holiday.Date <= end)
                    result.Add(holiday);
            }
        }

        return result;
    }

    // Array interno (nunca exposto): ordenado por data, sem datas repetidas
    private Holiday[] GetYear(int year) => year is >= MinCachedYear and <= MaxCachedYear
        ? _cache.GetOrAdd(year, Calculate)
        : Calculate(year);

    private Holiday[] Calculate(int year)
    {
        var easter = GetEaster(year);
        var holidays = new List<Holiday>
        {
            new(new DateOnly(year, 1, 1), "Confraternização Universal"),
            new(new DateOnly(year, 4, 21), "Tiradentes"),
            new(new DateOnly(year, 5, 1), "Dia do Trabalho"),
            new(new DateOnly(year, 9, 7), "Independência do Brasil"),
            new(new DateOnly(year, 11, 2), "Finados"),
            new(new DateOnly(year, 11, 15), "Proclamação da República"),
            new(new DateOnly(year, 12, 25), "Natal"),
            new(easter.AddDays(-2), "Paixão de Cristo"),
        };

        // Lei 6.802/1980
        if (year >= 1980)
            holidays.Add(new(new DateOnly(year, 10, 12), "Nossa Senhora Aparecida"));

        // Lei 14.759/2023
        if (year >= 2024)
            holidays.Add(new(new DateOnly(year, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra"));

        if (IncludeCarnival)
        {
            holidays.Add(new(easter.AddDays(-48), "Carnaval"));
            holidays.Add(new(easter.AddDays(-47), "Carnaval"));
        }

        if (IncludeCorpusChristi)
            holidays.Add(new(easter.AddDays(60), "Corpus Christi"));

        // A Paixão de Cristo pode cair em 21/04 (ex.: 2000): mantém Tiradentes, que vem antes na lista
        return [.. holidays.DistinctBy(h => h.Date).OrderBy(h => h.Date)];
    }
}
