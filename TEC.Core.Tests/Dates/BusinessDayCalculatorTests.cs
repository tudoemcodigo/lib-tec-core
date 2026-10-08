using System.Text;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;

namespace TEC.Core.Tests.Dates;

public class BusinessDayCalculatorTests
{
    // Novembro/2026: dia 1 é domingo; feriados em 02 (segunda), 15 (domingo) e 20 (sexta)
    private const string HolidaysCsv = """
        Data;Descrição
        02/11/2026;Finados
        15/11/2026;Proclamação da República
        20/11/2026;Dia da Consciência Negra
        2026-12-25;Natal
        """;

    private static CsvHolidaySource CsvSource() =>
        new(() => new MemoryStream(Encoding.UTF8.GetBytes(HolidaysCsv)), "teste");

    private static async Task<BusinessDayCalculator> CreateCalculatorAsync()
    {
        var calendar = await HolidayCalendar.CreateBuilder().AddSource(CsvSource()).BuildAsync();
        var provider = calendar.National;
        return new BusinessDayCalculator(provider);
    }

    [Test]
    public async Task CsvHolidaySource_LoadsHolidays()
    {
        var calendar = await HolidayCalendar.CreateBuilder().AddSource(CsvSource()).BuildAsync();
        var provider = calendar.National;

        await Assert.That(calendar.Count).IsEqualTo(4);
        await Assert.That(provider.GetHoliday(new DateOnly(2026, 11, 2))?.Description).IsEqualTo("Finados");
        await Assert.That(provider.GetHolidays(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)).Count).IsEqualTo(3);
        await Assert.That(provider.IsHoliday(new DateOnly(2026, 12, 25))).IsTrue();
    }

    [Test]
    public async Task IsBusinessDay()
    {
        var calc = await CreateCalculatorAsync();

        await Assert.That(calc.IsBusinessDay(new DateOnly(2026, 11, 3))).IsTrue();
        await Assert.That(calc.IsBusinessDay(new DateOnly(2026, 11, 2))).IsFalse();  // feriado
        await Assert.That(calc.IsBusinessDay(new DateOnly(2026, 11, 7))).IsFalse();  // sábado
    }

    [Test]
    public async Task NextAndPreviousBusinessDay()
    {
        var calc = await CreateCalculatorAsync();

        await Assert.That(calc.NextBusinessDay(new DateOnly(2026, 10, 30))).IsEqualTo(new DateOnly(2026, 11, 3));
        await Assert.That(calc.PreviousBusinessDay(new DateOnly(2026, 11, 3))).IsEqualTo(new DateOnly(2026, 10, 30));
        await Assert.That(calc.NextOrSameBusinessDay(new DateOnly(2026, 11, 15))).IsEqualTo(new DateOnly(2026, 11, 16));
        await Assert.That(calc.NextOrSameBusinessDay(new DateOnly(2026, 11, 16))).IsEqualTo(new DateOnly(2026, 11, 16));
        await Assert.That(calc.PreviousOrSameBusinessDay(new DateOnly(2026, 11, 15))).IsEqualTo(new DateOnly(2026, 11, 13));
    }

    [Test]
    public async Task AddBusinessDays()
    {
        var calc = await CreateCalculatorAsync();

        await Assert.That(calc.AddBusinessDays(new DateOnly(2026, 11, 19), 1)).IsEqualTo(new DateOnly(2026, 11, 23));
        await Assert.That(calc.AddBusinessDays(new DateOnly(2026, 11, 23), -1)).IsEqualTo(new DateOnly(2026, 11, 19));
        await Assert.That(calc.AddBusinessDays(new DateOnly(2026, 10, 30), 6)).IsEqualTo(new DateOnly(2026, 11, 10));
        // D+0 em dia não útil (domingo e feriado): próximo dia útil
        await Assert.That(calc.AddBusinessDays(new DateOnly(2026, 11, 15), 0)).IsEqualTo(new DateOnly(2026, 11, 16));
        await Assert.That(calc.AddBusinessDays(new DateOnly(2026, 11, 16), 0)).IsEqualTo(new DateOnly(2026, 11, 16));
    }

    [Test]
    public async Task DateTimeOverloads_PreserveTime()
    {
        var calc = await CreateCalculatorAsync();
        var date = new DateTime(2026, 10, 30, 14, 30, 0, DateTimeKind.Utc);

        var next = calc.NextBusinessDay(date);

        await Assert.That(next).IsEqualTo(new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc));
        await Assert.That(next.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    // Regressão: DateTime UTC era avaliado na data UTC, e não na data de Brasília
    [Test]
    public async Task DateTimeOverloads_Utc_UseBrasiliaDate()
    {
        var calc = await CreateCalculatorAsync();

        // Sábado 07/11/2026 02:00 UTC = sexta 06/11/2026 23:00 em Brasília (dia útil)
        var saturdayUtc = new DateTime(2026, 11, 7, 2, 0, 0, DateTimeKind.Utc);
        await Assert.That(calc.IsBusinessDay(saturdayUtc)).IsTrue();
        await Assert.That(calc.NextOrSameBusinessDay(saturdayUtc)).IsEqualTo(saturdayUtc);

        // Segunda 02/11/2026 (feriado) 01:00 UTC = domingo 01/11 22:00 em Brasília; o próximo dia útil é terça 03/11 22:00 BRT
        var mondayUtc = new DateTime(2026, 11, 2, 1, 0, 0, DateTimeKind.Utc);
        var next = calc.NextBusinessDay(mondayUtc);
        await Assert.That(next).IsEqualTo(new DateTime(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc));
        await Assert.That(next.Kind).IsEqualTo(DateTimeKind.Utc);

        await Assert.That(calc.CountBusinessDays(saturdayUtc, saturdayUtc)).IsEqualTo(1);

        // Unspecified continua usando a própria data
        await Assert.That(calc.IsBusinessDay(new DateTime(2026, 11, 7, 2, 0, 0))).IsFalse();
    }

    [Test]
    public async Task MonthCalculations()
    {
        var calc = await CreateCalculatorAsync();

        await Assert.That(calc.CountBusinessDaysInMonth(2026, 11)).IsEqualTo(19);
        await Assert.That(calc.CountBusinessDays(new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 1))).IsEqualTo(19);
        await Assert.That(calc.GetFirstBusinessDayOfMonth(2026, 11)).IsEqualTo(new DateOnly(2026, 11, 3));
        await Assert.That(calc.GetNthBusinessDayOfMonth(2026, 11, 5)).IsEqualTo(new DateOnly(2026, 11, 9));
        await Assert.That(calc.GetLastBusinessDayOfMonth(2026, 11)).IsEqualTo(new DateOnly(2026, 11, 30));
        await Assert.That(() => calc.GetNthBusinessDayOfMonth(2026, 11, 20)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CustomNonWorkingDays()
    {
        // Ex.: operação que funciona aos sábados
        var calc = new BusinessDayCalculator(new InMemoryHolidayProvider([]), [DayOfWeek.Sunday]);
        await Assert.That(calc.IsBusinessDay(new DateOnly(2026, 11, 7))).IsTrue();
        await Assert.That(() => new BusinessDayCalculator(new InMemoryHolidayProvider([]), Enum.GetValues<DayOfWeek>()))
            .ThrowsExactly<ArgumentException>();
    }
}
