using TEC.Core.Dates.Extensions;
using TEC.Core.Dates.Formatting;
using TEC.Core.Dates.TimeZones;

namespace TEC.Core.Tests.Dates;

public class DateFormattingTests
{
    private static readonly DateOnly Date = new(2026, 9, 30);

    [Test]
    public async Task Formatting()
    {
        await Assert.That(Date.ToBrazilianDate()).IsEqualTo("30/09/2026");
        await Assert.That(Date.ToIso8601()).IsEqualTo("2026-09-30");
        await Assert.That(Date.ToLongDate()).IsEqualTo("30 de setembro de 2026");
        await Assert.That(Date.ToFullDate()).IsEqualTo("quarta-feira, 30 de setembro de 2026");
        await Assert.That(Date.ToMonthYear()).IsEqualTo("setembro/2026");
        await Assert.That(new DateTime(2026, 9, 30, 14, 5, 9).ToBrazilianDateTime()).IsEqualTo("30/09/2026 14:05");
        await Assert.That(DateFormatter.GetMonthName(9, capitalize: true)).IsEqualTo("Setembro");
    }

    [Test]
    [Arguments("30/09/2026")]
    [Arguments("30-09-2026")]
    [Arguments("30092026")]
    [Arguments("2026-09-30")]
    public async Task TryParseDate(string text)
    {
        await Assert.That(DateFormatter.TryParseDate(text, out var date)).IsTrue();
        await Assert.That(date).IsEqualTo(Date);
    }

    [Test]
    public async Task TryParseDate_Invalid() => await Assert.That(DateFormatter.TryParseDate("31/02/2026", out _)).IsFalse();

    [Test]
    public async Task RelativeTime()
    {
        var now = new DateTime(2026, 9, 30, 12, 0, 0);
        await Assert.That(now.AddSeconds(-10).ToRelativeTime(now)).IsEqualTo("agora");
        await Assert.That(now.AddMinutes(-5).ToRelativeTime(now)).IsEqualTo("há 5 minutos");
        await Assert.That(now.AddDays(2).ToRelativeTime(now)).IsEqualTo("em 2 dias");
        await Assert.That(now.AddYears(-1).ToRelativeTime(now)).IsEqualTo("há 1 ano");
    }

    [Test]
    public async Task Extensions()
    {
        await Assert.That(Date.StartOfMonth()).IsEqualTo(new DateOnly(2026, 9, 1));
        await Assert.That(new DateOnly(2026, 2, 10).EndOfMonth()).IsEqualTo(new DateOnly(2026, 2, 28));
        await Assert.That(Date.StartOfWeek()).IsEqualTo(new DateOnly(2026, 9, 27));
        await Assert.That(Date.EndOfWeek()).IsEqualTo(new DateOnly(2026, 10, 3));
        await Assert.That(Date.Quarter()).IsEqualTo(3);
        await Assert.That(Date.Semester()).IsEqualTo(2);
        await Assert.That(new DateOnly(1990, 10, 1).CalculateAge(Date)).IsEqualTo(35);
        await Assert.That(new DateOnly(1990, 9, 30).CalculateAge(Date)).IsEqualTo(36);
        await Assert.That(Date.IsWeekend()).IsFalse();
    }

    [Test]
    public async Task BrazilTimeZone_ConvertsFromUtc()
    {
        var utc = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
        await Assert.That(utc.ToBrasiliaTime()).IsEqualTo(new DateTime(2026, 9, 30, 12, 0, 0));
        await Assert.That(new DateTime(2026, 9, 30, 12, 0, 0).FromBrasiliaTimeToUtc()).IsEqualTo(utc);
    }

    // Regressão: DateTime já em UTC era tratado como horário de Brasília e deslocado 3 horas
    [Test]
    public async Task FromBrasiliaTimeToUtc_UtcKind_IsReturnedUnchanged()
    {
        var utc = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
        var result = utc.FromBrasiliaTimeToUtc();

        await Assert.That(result).IsEqualTo(utc);
        await Assert.That(result.Kind).IsEqualTo(DateTimeKind.Utc);
        await Assert.That(new DateTime(2026, 9, 30, 12, 0, 0).FromBrasiliaTimeToUtc().Kind).IsEqualTo(DateTimeKind.Utc);
    }

    // Regressão: horário inexistente no início do horário de verão histórico lançava ArgumentException
    [Test]
    public async Task FromBrasiliaTimeToUtc_InvalidAndAmbiguousTimes_AreDefined()
    {
        var gap = new DateTime(2018, 11, 4, 0, 30, 0);       // 00:00 → 01:00 (início do horário de verão)
        var ambiguous = new DateTime(2019, 2, 16, 23, 30, 0); // 00:00 → 23:00 (fim do horário de verão)

        // Sem a base histórica de fusos (fallback UTC-03:00 fixo) não há lacuna nem ambiguidade: ambos usam -03:00
        await Assert.That(gap.FromBrasiliaTimeToUtc()).IsEqualTo(new DateTime(2018, 11, 4, 3, 30, 0, DateTimeKind.Utc));
        await Assert.That(ambiguous.FromBrasiliaTimeToUtc()).IsEqualTo(new DateTime(2019, 2, 17, 2, 30, 0, DateTimeKind.Utc));

        // Horário de verão em vigor (válido e não ambíguo): -02:00 quando a base histórica está disponível
        if (BrazilTimeZone.Info.IsDaylightSavingTime(new DateTime(2018, 12, 1, 12, 0, 0)))
            await Assert.That(new DateTime(2018, 12, 1, 12, 0, 0).FromBrasiliaTimeToUtc()).IsEqualTo(new DateTime(2018, 12, 1, 14, 0, 0, DateTimeKind.Utc));
    }

    // Regressão: de 360 a 364 dias o texto era "há 12 meses"
    [Test]
    public async Task RelativeTime_360To364Days_IsElevenMonths()
    {
        var now = new DateTime(2026, 9, 30, 12, 0, 0);
        await Assert.That(now.AddDays(-362).ToRelativeTime(now)).IsEqualTo("há 11 meses");
        await Assert.That(now.AddDays(364).ToRelativeTime(now)).IsEqualTo("em 11 meses");
        await Assert.That(now.AddDays(-365).ToRelativeTime(now)).IsEqualTo("há 1 ano");
    }
}
