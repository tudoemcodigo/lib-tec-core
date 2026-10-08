using System.Security.Cryptography;
using System.Text;
using TEC.Core.Common.Guards;
using TEC.Core.Common.Results;
using TEC.Core.Csv;
using TEC.Core.Csv.Attributes;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Cryptography.Symmetric;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Extensions;
using TEC.Core.Dates.Formatting;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;
using TEC.Core.Enums;
using TEC.Core.Enums.Extensions;
using TEC.Core.Numbers.Extensions;
using TEC.Core.Responses;
using TEC.Core.Responses.Pagination;
using TEC.Core.Text.Extensions;
using TEC.Core.Text.Masking;
using TEC.Core.Text.Validation;

namespace TEC.Core.Tests.Validation;

/// <summary>
/// Testes de regressão da validação de parâmetros: entradas inválidas devem falhar de forma clara,
/// nunca produzir resultado incorreto em silêncio.
/// </summary>
public class ParameterValidationTests
{
    // ---------- Common ----------

    [Test]
    public async Task Error_RejectsInvalidArguments()
    {
        await Assert.That(() => new Error("", "mensagem")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new Error("COD", null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => new Error("COD", "mensagem", (ErrorType)99)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Result_MapAndMatch_RejectNullDelegates()
    {
        var result = Result.Success(1);
        await Assert.That(() => result.Map<int>(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => result.Match<int>(null!, _ => 0)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Guard_InRange_RejectsInvertedLimits() =>
        await Assert.That(() => Guard.InRange(5, 10, 1)).ThrowsExactly<ArgumentException>();

    // ---------- Criptografia ----------

    [Test]
    public async Task Aes_Streams_AreValidated()
    {
        var aes = new AesGcmCryptography();
        var key = aes.GenerateKey();
        using var same = new MemoryStream();
        using var readOnly = new MemoryStream([1, 2, 3], writable: false);

        await Assert.That(() => aes.EncryptAsync(same, same, key)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => aes.EncryptAsync(new MemoryStream(), readOnly, key)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Aes_Base64KeyWithWrongSize_ReportsCorrectParameter()
    {
        var ex = await Assert.That(() => new AesGcmCryptography().Encrypt("x", Convert.ToBase64String(new byte[10])))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.ParamName).IsEqualTo("base64Key");
    }

    [Test]
    public async Task Rsa_DataTooLarge_ClearError()
    {
        var rsa = new RsaCryptography();
        var keys = rsa.GenerateKeyPair();

        var ex = await Assert.That(() => rsa.Encrypt(new byte[RsaCryptography.GetMaxDataLength(2048) + 1], keys.PublicKeyPem))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(ex!.Message).Contains("HybridCryptography");
    }

    [Test]
    public async Task Rsa_PublicKeyWherePrivateRequired_ClearError()
    {
        var rsa = new RsaCryptography();
        var keys = rsa.GenerateKeyPair();

        await Assert.That(() => rsa.SignData("x", keys.PublicKeyPem)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => rsa.Decrypt(new byte[256], keys.PublicKeyPem)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Rsa_HugeSignature_ReturnsFalseWithoutOverflow()
    {
        var rsa = new RsaCryptography();
        var keys = rsa.GenerateKeyPair();
        await Assert.That(rsa.VerifyData("x", new string('A', 100_000), keys.PublicKeyPem)).IsFalse();
    }

    [Test]
    public async Task Hmac_RejectsEmptyKey()
    {
        await Assert.That(() => HashHelper.ComputeHmac("dados", "")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => HashHelper.ComputeHmac([1], [])).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task PasswordHasher_RejectsEmptyPassword()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        await Assert.That(() => hasher.Hash("")).ThrowsExactly<ArgumentException>();
        await Assert.That(hasher.Verify("", hasher.Hash("x"))).IsFalse();
    }

    // ---------- Texto ----------

    [Test]
    [Arguments("usuario@empresa.com\n")]
    [Arguments(".usuario@empresa.com")]
    [Arguments("usu..ario@empresa.com")]
    [Arguments("usuario.@empresa.com")]
    public async Task Email_RejectsTrailingNewlineAndInvalidDots(string email) =>
        await Assert.That(DocumentValidator.IsValidEmail(email)).IsFalse();

    [Test]
    [Arguments("529.982.247-25\n")]
    [Arguments("529982247\n25")]
    [Arguments("529.982.247-25\t")]
    public async Task Documents_RejectLineBreaksAndTabs(string cpf) =>
        await Assert.That(DocumentValidator.IsValidCpf(cpf)).IsFalse();

    [Test]
    public async Task Documents_RejectOversizedInput()
    {
        var huge = "529.982.247-25" + new string(' ', 1000);
        await Assert.That(DocumentValidator.IsValidCpf(huge)).IsFalse();
        await Assert.That(DocumentValidator.IsValidCpfOrCnpj(huge)).IsFalse();
    }

    [Test]
    [Arguments("(20) 98765-4321", false)] // DDD inexistente
    [Arguments("(23) 98765-4321", false)]
    [Arguments("(21) 98765-4321", true)]
    public async Task Phone_ValidatesRealAreaCodes(string phone, bool expected) =>
        await Assert.That(DocumentValidator.IsValidPhone(phone)).IsEqualTo(expected);

    [Test]
    public async Task Mask_HugeVisibleCounts_DoNotOverflowOrExposeValue()
    {
        await Assert.That(SensitiveDataMasker.Mask("12345", int.MaxValue, 1)).IsEqualTo("*****");
        await Assert.That(() => SensitiveDataMasker.Mask("", -1)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => SensitiveDataMasker.Mask("abc", 1, 0, '\0')).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Truncate_DoesNotSplitEmoji()
    {
        var text = "Olá 😀 mundo";   // o emoji ocupa 2 posições (par surrogate)
        var cut = text.Left(5);

        await Assert.That(cut).IsEqualTo("Olá ");
        await Assert.That(char.IsHighSurrogate(text.Truncate(6, "")[^1])).IsFalse();
        await Assert.That(() => "abc".Truncate(2, null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(text.Right(7)).IsEqualTo(" mundo");
    }

    [Test]
    public async Task FromBase64_RejectsInvalidUtf8()
    {
        var invalidUtf8 = Convert.ToBase64String([0xFF, 0xFE, 0xFD]);

        await Assert.That(invalidUtf8.TryFromBase64(out _)).IsFalse();
        await Assert.That(() => invalidUtf8.FromBase64()).ThrowsExactly<FormatException>();
        await Assert.That(() => "***".FromBase64()).ThrowsExactly<FormatException>();
    }

    // ---------- Números ----------

    [Test]
    public async Task Numbers_ValidateDecimalsAndNonFiniteValues()
    {
        await Assert.That(() => 1m.ToBrazilianNumber(-1)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => 1m.RoundHalfUp(29)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => double.NaN.ToCurrency()).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => double.PositiveInfinity.ToCurrency()).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(new string('1', 1000).TryParseBrazilianDecimal(out _)).IsFalse();
    }

    // ---------- Datas ----------

    [Test]
    public async Task DateFormatter_ValidatesArguments()
    {
        await Assert.That(() => DateFormatter.GetMonthName(13)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => DateFormatter.GetDayOfWeekName((DayOfWeek)9)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => DateTime.UtcNow.ToRelativeTime(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local)))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task DateExtensions_HandleLimitsAndInvalidArguments()
    {
        await Assert.That(new DateTime(9999, 12, 15).EndOfMonth()).IsEqualTo(DateTime.MaxValue);
        await Assert.That(DateTime.MaxValue.EndOfDay()).IsEqualTo(DateTime.MaxValue);
        await Assert.That(() => new DateOnly(2026, 1, 1).IsBetween(new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1)))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(() => new DateOnly(2026, 1, 1).StartOfWeek((DayOfWeek)10)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new DateOnly(2030, 1, 1).CalculateAge(new DateOnly(2026, 1, 1))).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task BusinessDays_HandleCalendarLimits()
    {
        var calc = new BusinessDayCalculator(new InMemoryHolidayProvider([]));

        await Assert.That(calc.CountBusinessDays(new DateOnly(9999, 12, 1), DateOnly.MaxValue)).IsGreaterThan(0);
        await Assert.That(calc.GetLastBusinessDayOfMonth(9999, 12)).IsEqualTo(new DateOnly(9999, 12, 31));
        await Assert.That(() => calc.NextBusinessDay(DateOnly.MaxValue)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => calc.GetFirstBusinessDayOfMonth(2026, 13)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => calc.CountBusinessDaysInMonth(0, 1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Holidays_RejectNullsAndEmptyFileLists()
    {
        await Assert.That(() => new InMemoryHolidayProvider([null!])).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new Holiday(new DateOnly(2026, 1, 1), null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => new CsvHolidaySource(" ")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new JsonHolidaySource("")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => HolidayCalendar.CreateBuilder().AddHolidays([null!])).ThrowsExactly<ArgumentException>();
        await Assert.That(() => HolidayCalendar.CreateBuilder().AddBrasilApi(new HttpClient(), [])).ThrowsExactly<ArgumentException>();
    }

    // ---------- CSV ----------

    [Test]
    public async Task CsvOptions_RejectInvalidConfiguration()
    {
        await Assert.That(() => new CsvOptions { Delimiter = '"' }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new CsvOptions { Delimiter = '\n' }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new CsvOptions { Delimiter = 'a' }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new CsvOptions { BufferSize = 0 }).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new CsvOptions { MaxColumns = 0 }).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new CsvOptions { NewLine = "" }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new CsvOptions { Encoding = null! }).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => new CsvOptions { EnumFormat = (CsvEnumFormat)9 }).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(new CsvOptions { Delimiter = '\t' }.Delimiter).IsEqualTo('\t');
    }

    public sealed class DuplicateColumns
    {
        [CsvColumn("Nome")] public string A { get; set; } = "";
        [CsvColumn("NOME")] public string B { get; set; } = "";
    }

    public sealed class ComplexProperty
    {
        public string Name { get; set; } = "";
        public List<int> Values { get; set; } = [];
    }

    public sealed class NoProperties;

    [Test]
    public async Task Csv_RejectsInvalidTypeMappings()
    {
        await Assert.That(() => new CsvWriter().WriteAsync(new MemoryStream(), new[] { new DuplicateColumns() }))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => new CsvWriter().WriteAsync(new MemoryStream(), new[] { new ComplexProperty() }))
            .ThrowsExactly<NotSupportedException>();
        await Assert.That(() => new CsvWriter().WriteAsync(new MemoryStream(), new[] { new NoProperties() }))
            .ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => new CsvColumnAttribute(" ")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Csv_ValidatesStreamCapabilities()
    {
        using var readOnly = new MemoryStream([1], writable: false);
        await Assert.That(() => new CsvWriter().WriteAsync(readOnly, new[] { new ComplexProperty() }))
            .ThrowsExactly<ArgumentException>();
    }

    // ---------- Respostas / paginação ----------

    [Test]
    public async Task ApiResponse_ValidatesFailureArguments()
    {
        await Assert.That(() => ApiResponse.Fail(200, "x")).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => ApiResponse.Fail(400, " ")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => ApiResponse.Fail(400, "x", (IEnumerable<ApiError>)null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => ApiResponse.Fail(400, "x", [null!])).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new ApiError("", "x")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => ApiResponse.Ok() with { StatusCode = 1000 }).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Pagination_ValidatesConsistency()
    {
        await Assert.That(() => new PagedResult<int>([], 0, 10, 0)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new PagedResult<int>(null!, 1, 10, 0)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => PagedResponse<int>.Create([1, 2, 3], 1, 2, 3)).ThrowsExactly<ArgumentException>();   // mais itens que o tamanho
        await Assert.That(() => PagedResponse<int>.Create([1, 2], 1, 10, 1)).ThrowsExactly<ArgumentException>();     // mais itens que o total
    }

    // ---------- Enums ----------

    public enum Huge : ulong { Small = 1, Big = ulong.MaxValue }

    [Test]
    public async Task Enum_UlongValues_DoNotOverflow()
    {
        await Assert.That(Huge.Big.GetCode()).IsEqualTo(-1);
        await Assert.That(EnumHelper.GetItems<Huge>().Count).IsEqualTo(2);
        await Assert.That(() => EnumHelper.GetCode(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => ((string)null!).ToEnum<Huge>()).ThrowsExactly<ArgumentNullException>();
    }
}
