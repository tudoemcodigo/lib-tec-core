using Microsoft.Extensions.DependencyInjection;
using TEC.Core.Common.Guards;
using TEC.Core.Common.Results;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Csv.Abstractions;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.DependencyInjection;

namespace TEC.Core.Tests.Common;

public class CommonTests
{
    private enum Color { DarkBlue }

    [Test]
    public async Task Guard_ThrowsWithParameterName()
    {
        string? name = null;
        var ex = await Assert.That(() => Guard.NotNullOrWhiteSpace(name)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(ex!.ParamName).IsEqualTo("name");
        await Assert.That(() => Guard.InRange(11, 1, 10)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Result_SuccessAndFailure()
    {
        Result<int> success = 10;
        Result<int> failure = Error.NotFound("X", "Não encontrado");

        await Assert.That(success.Map(v => v * 2).Value).IsEqualTo(20);
        await Assert.That(failure.IsFailure).IsTrue();
        await Assert.That(failure.Error!.Type).IsEqualTo(ErrorType.NotFound);
        await Assert.That(() => failure.Value).ThrowsExactly<InvalidOperationException>();
        await Assert.That(failure.Match(_ => "ok", _ => "erro")).IsEqualTo("erro");
    }

    [Test]
    public async Task Json_UsesCamelCaseEnumsAsStringAndKeepsAccents()
    {
        var json = new { FullName = "João", Color = Color.DarkBlue, Missing = (string?)null }.ToJson();
        await Assert.That(json).IsEqualTo("{\"fullName\":\"João\",\"color\":\"darkBlue\"}");
    }

    [Test]
    public async Task TryFromJson_InvalidJson_ReturnsFalse()
    {
        await Assert.That("{invalido".TryFromJson<Dictionary<string, int>>(out _)).IsFalse();
    }

    [Test]
    public async Task AddTecCore_RegistersServices()
    {
        using var provider = new ServiceCollection()
            .AddTecCore()
            .AddBusinessDayCalculator(new InMemoryHolidayProvider([]))
            .BuildServiceProvider();

        await Assert.That(provider.GetService<ISymmetricCryptography>()).IsNotNull();
        await Assert.That(provider.GetService<IAsymmetricCryptography>()).IsNotNull();
        await Assert.That(provider.GetService<IHybridCryptography>()).IsNotNull();
        await Assert.That(provider.GetService<IPasswordHasher>()).IsNotNull();
        await Assert.That(provider.GetService<ICsvReader>()).IsNotNull();
        await Assert.That(provider.GetService<ICsvWriter>()).IsNotNull();
        await Assert.That(provider.GetService<IBusinessDayCalculator>()).IsNotNull();
    }

    // Regressão: TryAddSingleton descartava em silêncio o provedor informado se já houvesse um registrado
    [Test]
    public async Task AddBusinessDayCalculator_AlreadyRegistered_Throws()
    {
        var services = new ServiceCollection().AddBusinessDayCalculator(new InMemoryHolidayProvider([]));

        await Assert.That(() => services.AddBusinessDayCalculator(new InMemoryHolidayProvider([new Holiday(new DateOnly(2026, 1, 1), "Ano Novo")])))
            .ThrowsExactly<InvalidOperationException>();

        var withProvider = new ServiceCollection().AddSingleton<IHolidayProvider>(new InMemoryHolidayProvider([]));
        await Assert.That(() => withProvider.AddBusinessDayCalculator(new InMemoryHolidayProvider([])))
            .ThrowsExactly<InvalidOperationException>();
    }

    // Regressão: a coleção de dias não úteis era avaliada tardiamente (na primeira resolução)
    [Test]
    public async Task AddBusinessDayCalculator_MaterializesAndValidatesNonWorkingDaysImmediately()
    {
        var days = new List<DayOfWeek> { DayOfWeek.Sunday };
        using var provider = new ServiceCollection()
            .AddBusinessDayCalculator(new InMemoryHolidayProvider([]), days)
            .BuildServiceProvider();
        days.Add(DayOfWeek.Saturday);

        var calculator = provider.GetRequiredService<IBusinessDayCalculator>();
        await Assert.That(calculator.IsBusinessDay(new DateOnly(2026, 11, 7))).IsTrue(); // sábado continua útil

        await Assert.That(() => new ServiceCollection().AddBusinessDayCalculator(new InMemoryHolidayProvider([]), Enum.GetValues<DayOfWeek>()))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task AddBusinessDayCalculator_FromCalendar_RegistersFactoryAndDefaultLocation()
    {
        var calendar = await HolidayCalendar.CreateBuilder()
            .AddHolidays([new Holiday(new DateOnly(2026, 1, 26), "Feriado municipal", new HolidayLocation("SP", 3550308))])
            .AddBrazilianNational()
            .BuildAsync();

        using var provider = new ServiceCollection()
            .AddBusinessDayCalculator(calendar, new HolidayLocation("SP", 3550308), [DayOfWeek.Sunday])
            .BuildServiceProvider();

        var calculator = provider.GetRequiredService<IBusinessDayCalculator>();
        await Assert.That(calculator.IsBusinessDay(new DateOnly(2026, 1, 26))).IsFalse();  // municipal (localidade padrão)
        await Assert.That(calculator.IsBusinessDay(new DateOnly(2026, 11, 2))).IsFalse();  // Finados (calculado)
        await Assert.That(calculator.IsBusinessDay(new DateOnly(2026, 11, 7))).IsTrue();   // sábado útil
        await Assert.That(provider.GetRequiredService<IHolidayProvider>().IsHoliday(new DateOnly(2026, 1, 26))).IsTrue();
        await Assert.That(provider.GetRequiredService<HolidayCalendar>()).IsSameReferenceAs(calendar);

        var rio = provider.GetRequiredService<IBusinessDayCalculatorFactory>().For(new HolidayLocation("RJ"));
        await Assert.That(rio.IsBusinessDay(new DateOnly(2026, 1, 26))).IsTrue();

        await Assert.That(() => new ServiceCollection().AddSingleton(calendar).AddBusinessDayCalculator(calendar))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => new ServiceCollection().AddBusinessDayCalculator(calendar, nonWorkingDays: Enum.GetValues<DayOfWeek>()))
            .ThrowsExactly<ArgumentException>();
    }

    // Regressão: Holiday era mutável (instâncias compartilhadas pelo provedor podiam ser alteradas)
    [Test]
    public async Task Holiday_IsImmutable()
    {
        foreach (var property in typeof(Holiday).GetProperties().Where(p => p.CanWrite))
        {
            var modifiers = property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers();
            await Assert.That(modifiers).Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
        }

        await Assert.That(() => new Holiday { Description = null! }).ThrowsExactly<ArgumentNullException>();
    }

    // Regressão: TryFromJson deixava escapar NotSupportedException
    [Test]
    public async Task TryFromJson_UnsupportedType_ReturnsFalse()
    {
        await Assert.That("{\"a\":1}".TryFromJson<Stream>(out var value)).IsFalse();
        await Assert.That(value).IsNull();
    }

    // Regressão: a validação do construtor era burlável com "with"
    [Test]
    public async Task Error_With_IsValidated()
    {
        var error = Error.Validation("CODIGO", "Mensagem", "campo");

        await Assert.That(() => error with { Code = "" }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => error with { Message = " " }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => error with { Type = (ErrorType)999 }).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That((error with { Code = "OUTRO" }).Code).IsEqualTo("OUTRO");
    }
}
