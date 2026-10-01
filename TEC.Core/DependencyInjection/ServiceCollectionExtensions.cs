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
    /// <param name="services">Coleção de serviços.</param>
    /// <param name="csvOptions">Opções de CSV (padrão: <see cref="CsvOptions.Default"/>).</param>
    /// <param name="passwordHashIterations">Iterações do PBKDF2.</param>
    public static IServiceCollection AddTecCore(this IServiceCollection services, CsvOptions? csvOptions = null,
        int passwordHashIterations = Pbkdf2PasswordHasher.DefaultIterations)
    {
        Guard.NotNull(services);

        services.TryAddSingleton<ISymmetricCryptography, AesGcmCryptography>();
        services.TryAddSingleton<IAsymmetricCryptography, RsaCryptography>();
        services.TryAddSingleton<IHybridCryptography>(sp => new HybridCryptography(
            sp.GetRequiredService<ISymmetricCryptography>(), sp.GetRequiredService<IAsymmetricCryptography>()));
        services.TryAddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher(passwordHashIterations));
        services.TryAddSingleton<ICsvReader>(new CsvReader(csvOptions));
        services.TryAddSingleton<ICsvWriter>(new CsvWriter(csvOptions));

        return services;
    }

    /// <summary>Registra a calculadora de dias úteis com um provedor de feriados já carregado.</summary>
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
    /// Registra a calculadora de dias úteis carregando os feriados de arquivo(s) CSV.
    /// Os arquivos são lidos uma única vez, na primeira resolução do serviço.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="IHolidayProvider"/> ou <see cref="IBusinessDayCalculator"/> já registrados. Registre a calculadora uma única vez.
    /// </exception>
    public static IServiceCollection AddBusinessDayCalculator(this IServiceCollection services, IEnumerable<string> holidayCsvFiles,
        IEnumerable<DayOfWeek>? nonWorkingDays = null, CsvOptions? csvOptions = null)
    {
        Guard.NotNull(services);
        var files = Guard.NotEmpty(holidayCsvFiles?.ToList(), nameof(holidayCsvFiles));
        EnsureNotRegistered(services);

        // Validação imediata (na inicialização), e não apenas na primeira requisição em produção
        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("A lista de arquivos contém caminhos vazios.", nameof(holidayCsvFiles));
            if (!File.Exists(file))
                throw new FileNotFoundException("Arquivo de feriados não encontrado.", file);
        }

        // Materializa e valida os dias não úteis agora: a coleção do chamador pode mudar até a primeira resolução
        DayOfWeek[]? days = nonWorkingDays?.ToArray();
        _ = new BusinessDayCalculator(new InMemoryHolidayProvider([]), days);

        // Leitura bloqueante na criação do Singleton (uma única vez, no primeiro uso). É segura contra deadlock porque
        // toda a cadeia de leitura usa ConfigureAwait(false) e não depende de SynchronizationContext
        services.AddSingleton<IHolidayProvider>(_ =>
            CsvHolidayProvider.FromFilesAsync(files, csvOptions).ConfigureAwait(false).GetAwaiter().GetResult());
        services.AddSingleton<IBusinessDayCalculator>(sp =>
            new BusinessDayCalculator(sp.GetRequiredService<IHolidayProvider>(), days));

        return services;
    }

    private static void EnsureNotRegistered(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IHolidayProvider) || d.ServiceType == typeof(IBusinessDayCalculator)))
            throw new InvalidOperationException(
                "IHolidayProvider ou IBusinessDayCalculator já estão registrados. Chame AddBusinessDayCalculator uma única vez " +
                "(o provedor informado seria descartado em silêncio).");
    }
}
