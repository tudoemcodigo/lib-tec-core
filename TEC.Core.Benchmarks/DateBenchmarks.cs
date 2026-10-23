using BenchmarkDotNet.Attributes;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;

namespace TEC.Core.Benchmarks;

/// <summary>
/// Consultas de feriados e dias úteis num calendário com feriados municipais de todas as UFs.
/// <see cref="Municipalities"/> controla o tamanho do calendário (municípios por UF, 2 feriados cada).
/// </summary>
[MemoryDiagnoser]
public class DateBenchmarks
{
    private static readonly int[] UfCodes = [11, 12, 13, 14, 15, 16, 17, 21, 22, 23, 24, 25, 26, 27, 28, 29, 31, 32, 33, 35, 41, 42, 43, 50, 51, 52, 53];

    private HolidayCalendar _calendar = null!;
    private BusinessDayCalculatorFactory _factory = null!;
    private IHolidayProvider _provider = null!;
    private IBusinessDayCalculator _calculator = null!;
    private HolidayLocation _location = null!;

    [Params(200, 5_000)]
    public int Municipalities { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var holidays = UfCodes.SelectMany(uf => Enumerable.Range(0, Municipalities).SelectMany(m =>
        {
            int code = uf * 100_000 + 10 + m;
            return new[]
            {
                new Holiday(new DateOnly(2026, 1, 1).AddDays(code * 31 % 365), "Aniversário") { IbgeCode = code },
                new Holiday(new DateOnly(2026, 1, 1).AddDays((code * 17 + 90) % 365), "Padroeiro") { IbgeCode = code },
            };
        }));

        _calendar = HolidayCalendar.CreateBuilder().AddBrazilianNational().AddHolidays(holidays).BuildAsync().GetAwaiter().GetResult();
        _factory = new BusinessDayCalculatorFactory(_calendar);
        _location = HolidayLocation.FromIbgeCode(3500010);
        _provider = _calendar.GetProvider(_location);
        _calculator = _factory.For(_location);
    }

    [Benchmark]
    public bool IsHoliday() => _provider.IsHoliday(new DateOnly(2026, 4, 21));

    [Benchmark]
    public int HolidaysOfYear() => _provider.GetHolidays(2026).Count;

    [Benchmark]
    public int HolidaysOfMonth() => _provider.GetHolidays(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)).Count;

    [Benchmark]
    public DateOnly AddThirtyBusinessDays() => _factory.For(_location).AddBusinessDays(new DateOnly(2026, 1, 20), 30);

    [Benchmark]
    public int CountBusinessDaysInYear() => _calculator.CountBusinessDays(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    // Ano em campo (não constante): evita que o JIT calcule o resultado em tempo de compilação
    private int _year = 2026;

    [Benchmark]
    public DateOnly Easter() => BrazilianNationalHolidays.GetEaster(_year);
}
