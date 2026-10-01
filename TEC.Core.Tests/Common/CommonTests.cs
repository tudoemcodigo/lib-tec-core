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
        var json = new { NomeCompleto = "João", Cor = Color.DarkBlue, Nulo = (string?)null }.ToJson();
        await Assert.That(json).IsEqualTo("{\"nomeCompleto\":\"João\",\"cor\":\"darkBlue\"}");
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
    public async Task AddBusinessDayCalculator_FromCsvFiles_LoadsOnFirstResolution()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tec-core-feriados-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "Data;Descricao\n02/11/2026;Finados\n");
        try
        {
            using var provider = new ServiceCollection()
                .AddBusinessDayCalculator([path], [DayOfWeek.Sunday])
                .BuildServiceProvider();

            var calculator = provider.GetRequiredService<IBusinessDayCalculator>();
            await Assert.That(calculator.IsBusinessDay(new DateOnly(2026, 11, 2))).IsFalse();
            await Assert.That(calculator.IsBusinessDay(new DateOnly(2026, 11, 7))).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }

        // Nome do parâmetro correto (antes vinha "holidayCsvFiles?.ToList()")
        var ex = await Assert.That(() => new ServiceCollection().AddBusinessDayCalculator(Array.Empty<string>())).ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.ParamName).IsEqualTo("holidayCsvFiles");
    }

    // Regressão: o ParamName vinha como a expressão "filePaths?.ToList()"
    [Test]
    public async Task CsvHolidayProvider_EmptyFileList_HasCleanParamName()
    {
        var ex = await Assert.That(async () => { await CsvHolidayProvider.FromFilesAsync([]); }).ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.ParamName).IsEqualTo("filePaths");
    }

    // Regressão: Holiday era mutável (instâncias compartilhadas pelo provedor podiam ser alteradas)
    [Test]
    public async Task Holiday_IsImmutable()
    {
        foreach (var property in typeof(Holiday).GetProperties())
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
