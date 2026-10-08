using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Csv;

namespace TEC.Core.DependencyInjection;

/// <summary>
/// Opções de <see cref="ServiceCollectionExtensions.AddTecCore"/>. Validadas na chamada de <c>AddTecCore</c>:
/// configuração inválida falha na subida da aplicação, não na primeira requisição.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddTecCore(options =>
/// {
///     options.PasswordHashIterations = 800_000;
///     options.Csv = new CsvOptions { Delimiter = ',' };
/// });
/// </code>
/// </example>
public sealed class TecCoreOptions
{
    /// <summary>Opções de leitura e escrita de CSV (<see cref="CsvReader"/> e <see cref="CsvWriter"/>). Padrão: <see cref="CsvOptions.Default"/>.</summary>
    public CsvOptions Csv { get; set; } = CsvOptions.Default;

    /// <summary>
    /// Iterações do PBKDF2 do <see cref="Pbkdf2PasswordHasher"/> (100.000 a 10.000.000).
    /// Padrão: <see cref="Pbkdf2PasswordHasher.DefaultIterations"/> (recomendação OWASP).
    /// </summary>
    public int PasswordHashIterations { get; set; } = Pbkdf2PasswordHasher.DefaultIterations;

    /// <summary>Esquema de assinatura do <see cref="RsaCryptography"/>. Padrão: <see cref="RsaSignatureMode.Pss"/>.</summary>
    public RsaSignatureMode RsaSignatureMode { get; set; } = RsaSignatureMode.Pss;
}
