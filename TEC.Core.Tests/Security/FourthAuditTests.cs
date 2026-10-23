using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TEC.Core.Common.Guards;
using TEC.Core.Common.Results;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Csv;
using TEC.Core.Csv.Abstractions;
using TEC.Core.Dates.Extensions;
using TEC.Core.Dates.Formatting;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.TimeZones;
using TEC.Core.DependencyInjection;
using TEC.Core.Text.Validation;

namespace TEC.Core.Tests.Security;

/// <summary>
/// Regressões da quarta revisão: guardas que vazavam o valor ou usavam a expressão como nome de parâmetro, relógio
/// injetável (TimeProvider), opções do AddTecCore validadas na subida e superfície pública mínima.
/// </summary>
public class FourthAuditTests
{
    // 2026-09-30 02:30 UTC = 2026-09-29 23:30 em Brasília (o dia ainda não virou)
    private static readonly DateTimeOffset UtcInstant = new(2026, 9, 30, 2, 30, 0, TimeSpan.Zero);

    // Regressão: a mensagem de ArgumentOutOfRangeException incluía o valor recebido ("Actual value was ...")
    [Test]
    public async Task Guard_InRange_DoesNotEchoTheValue()
    {
        // Variável, não literal: o nome do parâmetro vem da expressão do argumento (CallerArgumentExpression)
        var secret = "segredo-123";
        var ex = await Assert.That(() => Guard.InRange(secret, "a", "b")).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(ex!.Message).DoesNotContain("segredo-123");
        await Assert.That(ex.ActualValue).IsNull();
    }

    // Regressão: sem nameof, o nome do parâmetro virava o texto da condição ("items.Count == 0")
    [Test]
    public async Task Guard_Against_DoesNotUseTheConditionAsParameterName()
    {
        int[] items = [];
        var ex = await Assert.That(() => Guard.Against(items.Length == 0, "Vazio.")).ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.ParamName).IsNull();

        var named = await Assert.That(() => Guard.Against(items.Length == 0, "Vazio.", nameof(items))).ThrowsExactly<ArgumentException>();
        await Assert.That(named!.ParamName).IsEqualTo("items");
    }

    // Regressão: o construtor era protected, permitindo subclasses externas de Result fora das regras de sucesso/falha
    [Test]
    public async Task Result_CannotBeSubclassedOutsideTheLibrary()
    {
        var constructors = typeof(Result).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        await Assert.That(constructors.All(c => c.IsAssembly || c.IsFamilyAndAssembly || c.IsPrivate)).IsTrue();
    }

    [Test]
    public async Task InMemoryHolidayProvider_IsSealed()
    {
        await Assert.That(typeof(InMemoryHolidayProvider).IsSealed).IsTrue();
    }

    // Regressão: "agora" vinha de DateTime.UtcNow, sem relógio injetável
    [Test]
    public async Task BrazilTimeZone_UsesTheInjectedClock()
    {
        var clock = new FixedTimeProvider(UtcInstant);

        await Assert.That(BrazilTimeZone.GetNow(clock)).IsEqualTo(new DateTime(2026, 9, 29, 23, 30, 0));
        await Assert.That(BrazilTimeZone.GetNow(clock).Kind).IsEqualTo(DateTimeKind.Unspecified);
        await Assert.That(BrazilTimeZone.GetToday(clock)).IsEqualTo(new DateOnly(2026, 9, 29));
        await Assert.That(() => BrazilTimeZone.GetNow(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task CalculateAge_UsesTheInjectedClock_InBrasilia()
    {
        var clock = new FixedTimeProvider(UtcInstant);

        // Em UTC já é 30/09, mas em Brasília ainda é 29/09: o aniversário de 30/09 ainda não chegou
        await Assert.That(new DateOnly(2000, 9, 30).CalculateAge(clock)).IsEqualTo(25);
        await Assert.That(new DateOnly(2000, 9, 29).CalculateAge(clock)).IsEqualTo(26);
    }

    [Test]
    public async Task ToRelativeTime_UsesTheInjectedClock()
    {
        var clock = new FixedTimeProvider(UtcInstant);

        await Assert.That(UtcInstant.UtcDateTime.AddMinutes(-5).ToRelativeTime(clock)).IsEqualTo("há 5 minutos");
        await Assert.That(UtcInstant.UtcDateTime.AddDays(2).ToRelativeTime(clock)).IsEqualTo("em 2 dias");
        await Assert.That(() => DateTime.UtcNow.ToRelativeTime((TimeProvider)null!)).ThrowsExactly<ArgumentNullException>();
    }

    // Regressão: AddTecCore recebia parâmetros soltos; agora segue o padrão Action<TecCoreOptions> e valida na subida
    [Test]
    public async Task AddTecCore_InvalidOptions_FailAtRegistration()
    {
        await Assert.That(() => new ServiceCollection().AddTecCore(o => o.PasswordHashIterations = 1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new ServiceCollection().AddTecCore(o => o.Csv = null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => new ServiceCollection().AddTecCore(o => o.RsaSignatureMode = (RsaSignatureMode)99))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddTecCore_AppliesOptions_AndIsIdempotent()
    {
        var csv = new CsvOptions { Delimiter = ',' };
        var services = new ServiceCollection()
            .AddTecCore(o =>
            {
                o.Csv = csv;
                o.PasswordHashIterations = 100_000;
                o.RsaSignatureMode = RsaSignatureMode.Pkcs1;
            })
            .AddTecCore(o => o.PasswordHashIterations = 900_000);

        await Assert.That(services.Count(d => d.ServiceType == typeof(IPasswordHasher))).IsEqualTo(1);

        using var provider = services.BuildServiceProvider();
        var hash = provider.GetRequiredService<IPasswordHasher>().Hash("x");
        await Assert.That(hash).StartsWith("PBKDF2-SHA256$100000$");

        // Assinatura PKCS#1 v1.5: verificável por uma instância Pkcs1 e recusada por uma PSS
        var rsa = provider.GetRequiredService<IAsymmetricCryptography>();
        var keys = rsa.GenerateKeyPair();
        var signature = rsa.SignData("payload", keys.PrivateKeyPem);
        await Assert.That(new RsaCryptography(RsaSignatureMode.Pkcs1).VerifyData("payload", signature, keys.PublicKeyPem)).IsTrue();
        await Assert.That(new RsaCryptography(RsaSignatureMode.Pss).VerifyData("payload", signature, keys.PublicKeyPem)).IsFalse();

        using var output = new MemoryStream();
        await provider.GetRequiredService<ICsvWriter>().WriteAsync(output, [new Row { A = 1, B = 2 }]);
        await Assert.That(System.Text.Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF')).IsEqualTo("A,B\r\n1,2\r\n");
    }

    // Regressão: o CEP extraía os dígitos de toda a entrada antes de conferir o tamanho e os caracteres
    [Test]
    [Arguments("01310-100", true)]
    [Arguments("01310 100", true)]
    [Arguments("0131O-100", false)]
    [Arguments("01310-1000", false)]
    [Arguments("00000-000", false)]
    public async Task IsValidCep_ChecksCharactersBeforeDigits(string cep, bool expected)
    {
        await Assert.That(DocumentValidator.IsValidCep(cep)).IsEqualTo(expected);
        await Assert.That(DocumentValidator.IsValidCep(new string('1', 10_000_000))).IsFalse();
    }

    public sealed class Row
    {
        public int A { get; set; }

        public int B { get; set; }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
