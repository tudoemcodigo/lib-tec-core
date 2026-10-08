using TEC.Core.Dates.Holidays;
using TEC.Core.SampleApi;

namespace TEC.Core.LoadTests.Infrastructure;

/// <summary>Calendários compartilhados pelos testes (construídos uma vez por processo).</summary>
public static class Calendars
{
    private static readonly Lazy<Task<HolidayCalendar>> National = new(() => HolidayCalendar.CreateBuilder()
        .AddBrazilianNational()
        .AddHolidays(SampleData.Holidays(MunicipalitiesPerUf, 2026).Concat(SampleData.Holidays(MunicipalitiesPerUf, 2027)), "Exemplo")
        .BuildAsync());

    /// <summary>Municípios sintéticos por UF.</summary>
    public const int MunicipalitiesPerUf = 200;

    /// <summary>Nacionais calculados + ~21,6 mil feriados estaduais e municipais sintéticos de 2026 e 2027.</summary>
    public static Task<HolidayCalendar> SampleAsync() => National.Value;

    /// <summary>Localidade municipal sintética aleatória.</summary>
    public static HolidayLocation RandomLocation(Random random) =>
        HolidayLocation.FromIbgeCode(SampleData.IbgeCode(SampleData.UfCodes[random.Next(SampleData.UfCodes.Length)], random.Next(MunicipalitiesPerUf)));
}
