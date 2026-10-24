using System.Globalization;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Csv;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Domain;

namespace TEC.Core.Tests;

public class IssueRegressionTests
{
    // Issue #2: Formula injection com delimitador alternativo
    [Test]
    public async Task Csv_FormulaWithAlternativeDelimiter_IsQuoted()
    {
        var writer = new CsvWriter(new CsvOptions { Delimiter = ';', SanitizeFormulas = true });
        using var stream = new MemoryStream();
        var items = new[] { new ItemWithFormula { Id = 1, Value = "x,=cmd|' /C calc'!A0" } };

        await writer.WriteAsync(stream, items);
        string result = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        // Deve conter o campo com fórmula entre aspas para não vazar célula em planilha en-US
        await Assert.That(result).Contains("\"x,=cmd|' /C calc'!A0\"");
    }

    private class ItemWithFormula
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    // Issue #3: PBKDF2 NeedsRehash para iterações/tamanho diferentes e bloqueio de DoS
    [Test]
    public async Task Pbkdf2_NeedsRehash_DetectsDifferentIterationsOrHashSize()
    {
        var hasher = new Pbkdf2PasswordHasher(600_000);

        // Hash válido com 600.000 iterações não precisa de rehash
        string currentHash = hasher.Hash("Senha@123");
        await Assert.That(hasher.NeedsRehash(currentHash)).IsFalse();

        // Hash com 100.000 iterações (menos que o atual) precisa de rehash
        var oldHasher = new Pbkdf2PasswordHasher(100_000);
        string oldHash = oldHasher.Hash("Senha@123");
        await Assert.That(hasher.NeedsRehash(oldHash)).IsTrue();

        // Hash com 10.000.000 de iterações é rejeitado (proteção contra DoS)
        string dosHash = "PBKDF2-SHA256$10000000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        await Assert.That(hasher.Verify("Senha@123", dosHash)).IsFalse();
        await Assert.That(hasher.NeedsRehash(dosHash)).IsTrue();
    }

    // Issue #4: DateTimeOffset sem offset lido com UTC
    [Test]
    public async Task Csv_DateTimeOffsetWithoutOffset_AssumesUniversal()
    {
        var csv = "Data\r\n2026-06-15 12:00:00\r\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var reader = new CsvReader(new CsvOptions { HasHeader = true });

        var items = await reader.ReadAsync<RecordWithOffset>(stream).ToListAsync();
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].Data.Offset).IsEqualTo(TimeSpan.Zero);
    }

    private class RecordWithOffset
    {
        public DateTimeOffset Data { get; set; }
    }

    // Issue #5: Dias úteis com DateTimeKind.Local e DateTimeKind.Utc consistentes
    [Test]
    public async Task BusinessDay_LocalAndUtcInstants_AreConsistent()
    {
        var holidays = new BrazilianNationalHolidays();
        var calc = new BusinessDayCalculator(holidays);

        // Sexta-feira 23:30 UTC = Sexta-feira 20:30 em Brasília
        var utcInstant = new DateTime(2026, 6, 12, 23, 30, 0, DateTimeKind.Utc);
        var localInstant = utcInstant.ToLocalTime();

        var nextBusinessUtc = calc.NextOrSameBusinessDay(utcInstant);
        var nextBusinessLocal = calc.NextOrSameBusinessDay(localInstant);

        await Assert.That(DateOnly.FromDateTime(nextBusinessUtc)).IsEqualTo(DateOnly.FromDateTime(nextBusinessLocal.ToUniversalTime()));
    }

    // Issue #6: Holiday.ToString e erro FindBusinessDay usam cultura invariante com '/'
    [Test]
    public async Task Holiday_ToString_UsesInvariantDateSeparator()
    {
        var prevCulture = CultureInfo.CurrentCulture;
        try
        {
            var customInvariantCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            customInvariantCulture.DateTimeFormat.DateSeparator = ".";
            CultureInfo.CurrentCulture = customInvariantCulture;
            var holiday = new Holiday(new DateOnly(2026, 1, 25), "Aniversário de São Paulo");
            string text = holiday.ToString();

            await Assert.That(text).StartsWith("25/01/2026");
        }
        finally
        {
            CultureInfo.CurrentCulture = prevCulture;
        }
    }

    // Issue #7: DomainEntity.GetHashCode não muda quando Id é atribuído pelo ORM
    [Test]
    public async Task DomainEntity_GetHashCode_DoesNotChangeAfterIdAssignment()
    {
        var entity = new TestEntity();
        var set = new HashSet<TestEntity> { entity };

        int initialHash = entity.GetHashCode();
        await Assert.That(set.Contains(entity)).IsTrue();

        // Simula ORM preenchendo o Id no SaveChanges
        var id = Guid.NewGuid();
        entity.AssignId(id);

        int afterHash = entity.GetHashCode();
        await Assert.That(afterHash).IsEqualTo(initialHash);
        await Assert.That(set.Contains(entity)).IsTrue();

        var equivalent = new TestEntity();
        equivalent.AssignId(id);
        await Assert.That(entity.Equals(equivalent)).IsTrue();
        await Assert.That(entity.GetHashCode()).IsEqualTo(equivalent.GetHashCode());
        await Assert.That(set.Contains(equivalent)).IsTrue();
        await Assert.That(set.Remove(entity)).IsTrue();
    }

    [Test]
    public async Task DomainEntity_ProxySubclass_IsEqualToBaseEntity()
    {
        var id = Guid.NewGuid();
        var entity = new TestEntity();
        entity.AssignId(id);

        var proxy = new TestEntityProxy();
        proxy.AssignId(id);

        await Assert.That(entity.Equals(proxy)).IsTrue();
        await Assert.That(proxy.Equals(entity)).IsTrue();
    }

    private class TestEntity : DomainEntity<Guid>
    {
        public void AssignId(Guid id) => Id = id;
    }

    private class TestEntityProxy : TestEntity
    {
        protected override Type GetUnproxiedType() => typeof(TestEntity);
    }
}
