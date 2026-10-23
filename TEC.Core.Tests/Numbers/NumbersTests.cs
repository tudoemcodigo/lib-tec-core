using TEC.Core.Numbers.Extensions;
using TEC.Core.Numbers.Words;

namespace TEC.Core.Tests.Numbers;

public class NumbersTests
{
    [Test]
    [Arguments(0, "zero")]
    [Arguments(1, "um")]
    [Arguments(16, "dezesseis")]
    [Arguments(21, "vinte e um")]
    [Arguments(100, "cem")]
    [Arguments(101, "cento e um")]
    [Arguments(999, "novecentos e noventa e nove")]
    [Arguments(1000, "mil")]
    [Arguments(1100, "mil e cem")]
    [Arguments(1234, "mil duzentos e trinta e quatro")]
    [Arguments(2020, "dois mil e vinte")]
    [Arguments(1_000_001, "um milhão e um")]
    [Arguments(2_500_000, "dois milhões e quinhentos mil")]
    [Arguments(-15, "menos quinze")]
    public async Task ToWords(long value, string expected) =>
        await Assert.That(NumberToWordsConverter.ToWords(value)).IsEqualTo(expected);

    [Test]
    [Arguments("1234.56", "mil duzentos e trinta e quatro reais e cinquenta e seis centavos")]
    [Arguments("1.01", "um real e um centavo")]
    [Arguments("0.50", "cinquenta centavos")]
    [Arguments("1000000", "um milhão de reais")]
    [Arguments("0", "zero real")]
    public async Task ToCurrencyWords(string value, string expected) =>
        await Assert.That(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture).ToCurrencyWords()).IsEqualTo(expected);

    [Test]
    public async Task Formatting()
    {
        await Assert.That(1234.5m.ToCurrency()).IsEqualTo("R$ 1.234,50");
        await Assert.That(1234.5678m.ToBrazilianNumber(3)).IsEqualTo("1.234,568");
        await Assert.That(0.1575m.ToPercentage()).IsEqualTo("15,75%");
    }

    [Test]
    public async Task Rounding()
    {
        await Assert.That(2.345m.RoundHalfUp()).IsEqualTo(2.35m);
        await Assert.That(2.345m.RoundBankers()).IsEqualTo(2.34m);
        await Assert.That(2.349m.Truncate()).IsEqualTo(2.34m);
        await Assert.That(200m.PercentOf(15)).IsEqualTo(30m);
    }

    [Test]
    [Arguments("R$ 1.234,56", 1234.56)]
    [Arguments("1.234,56", 1234.56)]
    [Arguments("10", 10)]
    [Arguments("1234,56", 1234.56)]
    [Arguments("1.234", 1234)]
    [Arguments("1.234.567,8", 1234567.8)]
    [Arguments("-1.234,5", -1234.5)]
    [Arguments("1,5", 1.5)]
    public async Task TryParseBrazilianDecimal(string text, double expected)
    {
        await Assert.That(text.TryParseBrazilianDecimal(out var value)).IsTrue();
        await Assert.That(value).IsEqualTo((decimal)expected);
    }

    // Regressão: separador de milhar fora de grupos de 3 dígitos era ignorado ("1.5" virava 15 e "1.2.3" virava 123)
    [Test]
    [Arguments("1.5")]
    [Arguments("1.2.3")]
    [Arguments("12.34")]
    [Arguments("1234.567")]
    [Arguments("1.2345")]
    [Arguments(".123")]
    [Arguments("1.")]
    [Arguments("1..234")]
    [Arguments("1,234.5")]
    [Arguments("R$ 1.5")]
    public async Task TryParseBrazilianDecimal_InvalidGrouping_IsRejected(string text)
    {
        await Assert.That(text.TryParseBrazilianDecimal(out var value)).IsFalse();
        await Assert.That(value).IsEqualTo(0m);
    }
}
