using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TEC.Core.Common.Guards;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Cryptography.Hybrid;
using TEC.Core.Cryptography.Symmetric;
using TEC.Core.Csv;
using TEC.Core.Csv.Abstractions;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;

namespace TEC.Core.DependencyInjection;

/// <summary>
/// Registro dos serviços do TEC.Core no container de injeção de dependência.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra criptografia (simétrica, assimétrica, híbrida, hash de senha) e leitura/escrita de CSV como Singleton.
    /// </summary>
    /// <remarks>
    /// Idempotente: os serviços são registrados com <c>TryAdd</c>, então chamadas repetidas (ou registros prévios do
    /// consumidor) não duplicam nem substituem nada. As opções são validadas aqui, na subida da aplicação.
    /// </remarks>
    /// <param name="services">Coleção de serviços.</param>
    /// <param name="configure">Ajustes nas opções (padrão: <see cref="TecCoreOptions"/> com os valores recomendados).</param>
    /// <exception cref="ArgumentException">Opções inválidas (ex.: iterações fora da faixa, CSV nulo).</exception>
    public static IServiceCollection AddTecCore(this IServiceCollection services, Action<TecCoreOptions>? configure = null)
    {
        Guard.NotNull(services);

        var options = new TecCoreOptions();
        configure?.Invoke(options);

        // Criados já aqui: os construtores validam as opções e a configuração inválida falha na subida
        var csvOptions = Guard.NotNull(options.Csv, nameof(TecCoreOptions.Csv));
        var passwordHasher = new Pbkdf2PasswordHasher(options.PasswordHashIterations);
        var asymmetric = new RsaCryptography(options.RsaSignatureMode);

        services.TryAddSingleton<ISymmetricCryptography, AesGcmCryptography>();
        services.TryAddSingleton<IAsymmetricCryptography>(asymmetric);
        services.TryAddSingleton<IHybridCryptography>(sp => new HybridCryptography(
            sp.GetRequiredService<ISymmetricCryptography>(), sp.GetRequiredService<IAsymmetricCryptography>()));
        services.TryAddSingleton<IPasswordHasher>(passwordHasher);
        services.TryAddSingleton<ICsvReader>(new CsvReader(csvOptions));
        services.TryAddSingleton<ICsvWriter>(new CsvWriter(csvOptions));

        return services;
    }

    /// <summary>Registra a calculadora de dias úteis com um provedor de feriados já carregado (uma única localidade).</summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="IHolidayProvider"/> ou <see cref="IBusinessDayCalculator"/> já registrados: o provedor informado seria
    /// descartado em silêncio. Registre a calculadora uma única vez.
    /// </exception>
    public static IServiceCollection AddBusinessDayCalculator(this IServiceCollection services, IHolidayProvider holidayProvider,
        IEnumerable<DayOfWeek>? nonWorkingDays = null)
    {
        Guard.NotNull(services);
        Guard.NotNull(holidayProvider);
        EnsureNotRegistered(services);

        // Criada já na inicialização: dias não úteis são materializados e validados agora, não na primeira requisição
        var calculator = new BusinessDayCalculator(holidayProvider, nonWorkingDays);

        services.AddSingleton(holidayProvider);
        services.AddSingleton<IBusinessDayCalculator>(calculator);

        return services;
    }

    /// <summary>
    /// Registra o calendário de feriados (já carregado por <see cref="HolidayCalendarBuilder.BuildAsync"/>), a fábrica de
    /// calculadoras por localidade e a calculadora da localidade padrão, todos como Singleton.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <param name="calendar">Calendário carregado.</param>
    /// <param name="defaultLocation">
    /// Localidade de <see cref="IHolidayProvider"/> e <see cref="IBusinessDayCalculator"/> (padrão: <see cref="HolidayLocation.National"/>).
    /// Para outras localidades, use <see cref="IBusinessDayCalculatorFactory"/>.
    /// </param>
    /// <param name="nonWorkingDays">Dias da semana não úteis. Padrão: sábado e domingo.</param>
    /// <exception cref="InvalidOperationException">
    /// Calendário, fábrica, <see cref="IHolidayProvider"/> ou <see cref="IBusinessDayCalculator"/> já registrados.
    /// </exception>
    public static IServiceCollection AddBusinessDayCalculator(this IServiceCollection services, HolidayCalendar calendar,
        HolidayLocation? defaultLocation = null, IEnumerable<DayOfWeek>? nonWorkingDays = null)
    {
        Guard.NotNull(services);
        Guard.NotNull(calendar);
        EnsureNotRegistered(services);

        // Criados já na inicialização: dias não úteis são materializados e validados agora, não na primeira requisição
        var location = defaultLocation ?? HolidayLocation.National;
        var factory = new BusinessDayCalculatorFactory(calendar, nonWorkingDays);

        services.AddSingleton(calendar);
        services.AddSingleton<IBusinessDayCalculatorFactory>(factory);
        services.AddSingleton(calendar.GetProvider(location));
        services.AddSingleton(factory.For(location));

        return services;
    }

    private static void EnsureNotRegistered(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IHolidayProvider) || d.ServiceType == typeof(IBusinessDayCalculator)
                || d.ServiceType == typeof(IBusinessDayCalculatorFactory) || d.ServiceType == typeof(HolidayCalendar)))
        {
            throw new InvalidOperationException(
                "Os serviços de dias úteis (IHolidayProvider, IBusinessDayCalculator, IBusinessDayCalculatorFactory ou HolidayCalendar) " +
                "já estão registrados. Chame AddBusinessDayCalculator uma única vez (o provedor informado seria descartado em silêncio).");
        }
    }
}
