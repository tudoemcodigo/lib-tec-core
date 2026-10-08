using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;

namespace TEC.Core.Tests.Dates;

public class HolidayCalendarTests
{
    private const int SaoPaulo = 3550308;
    private const int Campinas = 3509502;
    private const int RioDeJaneiro = 3304557;

    private static readonly HolidayLocation SaoPauloCapital = new("SP", SaoPaulo);

    private static Func<Stream> Utf8(string content) => () => new MemoryStream(Encoding.UTF8.GetBytes(content));

    // ---------- Feriados nacionais calculados ----------

    [Test]
    [Arguments(2024, 3, 31)]
    [Arguments(2025, 4, 20)]
    [Arguments(2026, 4, 5)]
    [Arguments(2027, 3, 28)]
    [Arguments(2038, 4, 25)]
    [Arguments(2285, 3, 22)]
    public async Task GetEaster_KnownDates(int year, int month, int day)
    {
        await Assert.That(BrazilianNationalHolidays.GetEaster(year)).IsEqualTo(new DateOnly(year, month, day));
    }

    [Test]
    public async Task BrazilianNational_2026()
    {
        var national = new BrazilianNationalHolidays();

        var dates = national.GetHolidays(2026).Select(h => h.Date.ToString("dd/MM", CultureInfo.InvariantCulture)).ToArray();

        await Assert.That(dates).IsEquivalentTo(new[]
        {
            "01/01", "16/02", "17/02", "03/04", "21/04", "01/05", "04/06", "07/09", "12/10", "02/11", "15/11", "20/11", "25/12",
        });
        await Assert.That(national.GetHoliday(new DateOnly(2026, 6, 4))?.Description).IsEqualTo("Corpus Christi");
        await Assert.That(national.IsHoliday(new DateOnly(2026, 2, 18))).IsFalse(); // Quarta-feira de Cinzas
    }

    [Test]
    public async Task BrazilianNational_RespectsValidityAndOptions()
    {
        var national = new BrazilianNationalHolidays();
        await Assert.That(national.IsHoliday(new DateOnly(2023, 11, 20))).IsFalse(); // Consciência Negra só desde 2024
        await Assert.That(national.IsHoliday(new DateOnly(2024, 11, 20))).IsTrue();
        await Assert.That(national.IsHoliday(new DateOnly(1979, 10, 12))).IsFalse(); // N. Sra. Aparecida desde 1980
        await Assert.That(national.IsHoliday(new DateOnly(1980, 10, 12))).IsTrue();

        var withoutOptional = new BrazilianNationalHolidays(includeCarnival: false, includeCorpusChristi: false);
        await Assert.That(withoutOptional.IsHoliday(new DateOnly(2026, 2, 16))).IsFalse();
        await Assert.That(withoutOptional.IsHoliday(new DateOnly(2026, 6, 4))).IsFalse();
        await Assert.That(withoutOptional.IsHoliday(new DateOnly(2026, 4, 3))).IsTrue(); // Paixão de Cristo continua
    }

    [Test]
    public async Task BrazilianNational_GoodFridayOnTiradentes_NoDuplicateAndExtremeYears()
    {
        var national = new BrazilianNationalHolidays();

        // 2000: Páscoa em 23/04, Paixão de Cristo em 21/04
        var april21 = national.GetHolidays(2000).Where(h => h.Date == new DateOnly(2000, 4, 21)).ToList();
        await Assert.That(april21.Count).IsEqualTo(1);
        await Assert.That(april21[0].Description).IsEqualTo("Tiradentes");

        await Assert.That(national.GetHolidays(1).Count).IsGreaterThan(0);
        await Assert.That(national.GetHolidays(9999).Count).IsGreaterThan(0);

        // A lista devolvida é somente leitura: não altera o cache do provedor
        var list = (ICollection<Holiday>)national.GetHolidays(2026);
        await Assert.That(list.IsReadOnly).IsTrue();
        await Assert.That(national.GetHolidays(2026).Count).IsEqualTo(13);
    }

    [Test]
    public async Task BusinessDayCalculator_WithBrazilianNational_Standalone()
    {
        var calc = new BusinessDayCalculator(new BrazilianNationalHolidays());

        await Assert.That(calc.NextOrSameBusinessDay(new DateOnly(2026, 2, 14))).IsEqualTo(new DateOnly(2026, 2, 18)); // sábado → Cinzas
        await Assert.That(calc.CountBusinessDaysInMonth(2026, 11)).IsEqualTo(19);
    }

    // ---------- Localidade ----------

    [Test]
    public async Task HolidayLocation_ValidatesAndNormalizes()
    {
        var location = new HolidayLocation(" sp ", SaoPaulo);
        await Assert.That(location.Uf).IsEqualTo("SP");
        await Assert.That(location).IsEqualTo(SaoPauloCapital);
        await Assert.That(HolidayLocation.FromIbgeCode(RioDeJaneiro).Uf).IsEqualTo("RJ");
        await Assert.That(location.ToString()).IsEqualTo("SP/3550308");

        await Assert.That(() => new HolidayLocation("XX")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new HolidayLocation("")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new HolidayLocation("RJ", SaoPaulo)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => HolidayLocation.FromIbgeCode(123)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => HolidayLocation.FromIbgeCode(9_900_000)).ThrowsExactly<ArgumentException>(); // UF 99 inexistente
    }

    [Test]
    public async Task Holiday_ScopeAndValidation()
    {
        var national = new Holiday(new DateOnly(2026, 1, 1), "Ano Novo");
        var state = new Holiday(new DateOnly(2026, 7, 9), "Revolução Constitucionalista") { Uf = "sp" };
        var municipal = new Holiday(new DateOnly(2026, 1, 25), "Aniversário de SP") { IbgeCode = SaoPaulo };

        await Assert.That(national.Scope).IsEqualTo(HolidayScope.National);
        await Assert.That(state.Scope).IsEqualTo(HolidayScope.State);
        await Assert.That(state.Uf).IsEqualTo("SP");
        await Assert.That(municipal.Scope).IsEqualTo(HolidayScope.Municipal);
        await Assert.That(municipal.Uf).IsEqualTo("SP"); // deduzida do código IBGE

        await Assert.That(state.AppliesTo(SaoPauloCapital)).IsTrue();
        await Assert.That(state.AppliesTo(new HolidayLocation("RJ"))).IsFalse();
        await Assert.That(municipal.AppliesTo(new HolidayLocation("SP", Campinas))).IsFalse();
        await Assert.That(municipal.AppliesTo(new HolidayLocation("SP"))).IsFalse();
        await Assert.That(national.AppliesTo(HolidayLocation.National)).IsTrue();

        await Assert.That(() => new Holiday { Uf = "RJ", IbgeCode = SaoPaulo }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new Holiday { IbgeCode = SaoPaulo, Uf = "RJ" }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new Holiday { Uf = "São Paulo" }).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Calendar_FiltersByLocation()
    {
        var calendar = await HolidayCalendar.CreateBuilder()
            .AddBrazilianNational()
            .AddHolidays(
            [
                new Holiday(new DateOnly(2026, 7, 9), "Revolução Constitucionalista") { Uf = "SP" },
                new Holiday(new DateOnly(2026, 1, 20), "São Sebastião") { IbgeCode = RioDeJaneiro },
                new Holiday(new DateOnly(2026, 12, 8), "Imaculada Conceição") { IbgeCode = Campinas },
            ])
            .BuildAsync();

        var sp = calendar.GetProvider(SaoPauloCapital);
        var rio = calendar.GetProvider(HolidayLocation.FromIbgeCode(RioDeJaneiro));
        var campinas = calendar.GetProvider(HolidayLocation.FromIbgeCode(Campinas));

        await Assert.That(sp.IsHoliday(new DateOnly(2026, 7, 9))).IsTrue();
        await Assert.That(rio.IsHoliday(new DateOnly(2026, 7, 9))).IsFalse();
        await Assert.That(rio.IsHoliday(new DateOnly(2026, 1, 20))).IsTrue();
        await Assert.That(sp.IsHoliday(new DateOnly(2026, 1, 20))).IsFalse();
        await Assert.That(campinas.IsHoliday(new DateOnly(2026, 12, 8))).IsTrue();
        await Assert.That(campinas.IsHoliday(new DateOnly(2026, 7, 9))).IsTrue(); // estadual vale em Campinas
        await Assert.That(sp.IsHoliday(new DateOnly(2026, 12, 8))).IsFalse();
        await Assert.That(calendar.National.IsHoliday(new DateOnly(2026, 7, 9))).IsFalse();

        await Assert.That(sp.GetHolidays(2026).Count).IsEqualTo(14);   // 13 nacionais + 1 estadual
        await Assert.That(campinas.GetHolidays(2026).Count).IsEqualTo(15);
        await Assert.That(calendar.National.GetHolidays(2026).Count).IsEqualTo(13);
        await Assert.That(calendar.Count).IsEqualTo(3);
        await Assert.That(calendar.IncludesBrazilianNational).IsTrue();

        var ordered = campinas.GetHolidays(new DateOnly(2026, 12, 31), new DateOnly(2026, 7, 1)).Select(h => h.Date).ToList();
        await Assert.That(ordered.Count).IsEqualTo(8); // 09/07, 07/09, 12/10, 02/11, 15/11, 20/11, 08/12, 25/12
        await Assert.That(ordered.SequenceEqual(ordered.Order())).IsTrue();
    }

    [Test]
    public async Task Calendar_SameDate_FirstRegisteredSourceWins()
    {
        var date = new DateOnly(2026, 11, 20);

        var nationalFirst = await HolidayCalendar.CreateBuilder()
            .AddBrazilianNational()
            .AddHolidays([new Holiday(date, "Feriado local") { IbgeCode = SaoPaulo }])
            .BuildAsync();
        var localFirst = await HolidayCalendar.CreateBuilder()
            .AddHolidays([new Holiday(date, "Feriado local") { IbgeCode = SaoPaulo }])
            .AddBrazilianNational()
            .BuildAsync();

        await Assert.That(nationalFirst.GetProvider(SaoPauloCapital).GetHoliday(date)?.Description)
            .IsEqualTo("Dia Nacional de Zumbi e da Consciência Negra");
        await Assert.That(localFirst.GetProvider(SaoPauloCapital).GetHoliday(date)?.Description).IsEqualTo("Feriado local");
        await Assert.That(localFirst.GetProvider(SaoPauloCapital).GetHolidays(date, date).Single().Description).IsEqualTo("Feriado local");
        await Assert.That(localFirst.National.GetHoliday(date)?.Description).IsEqualTo("Dia Nacional de Zumbi e da Consciência Negra");
    }

    [Test]
    public async Task Factory_CreatesCalculatorsPerLocation()
    {
        var calendar = await HolidayCalendar.CreateBuilder()
            .AddBrazilianNational()
            .AddHolidays([new Holiday(new DateOnly(2026, 1, 20), "São Sebastião") { IbgeCode = RioDeJaneiro }])
            .BuildAsync();
        var factory = new BusinessDayCalculatorFactory(calendar);

        await Assert.That(factory.For(HolidayLocation.FromIbgeCode(RioDeJaneiro)).NextOrSameBusinessDay(new DateOnly(2026, 1, 20)))
            .IsEqualTo(new DateOnly(2026, 1, 21));
        await Assert.That(factory.For(SaoPauloCapital).NextOrSameBusinessDay(new DateOnly(2026, 1, 20)))
            .IsEqualTo(new DateOnly(2026, 1, 20));
        await Assert.That(() => factory.For(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => new BusinessDayCalculatorFactory(calendar, Enum.GetValues<DayOfWeek>())).ThrowsExactly<ArgumentException>();
    }

    // ---------- Fontes ----------

    [Test]
    public async Task CsvSource_ReadsOptionalLocationColumns()
    {
        const string csv = """
            Data;Descricao;Uf;CodigoIbge
            09/07/2026;Revolução Constitucionalista;SP;
            25/01/2026;Aniversário de São Paulo;;3550308
            01/01/2026;Confraternização Universal;;
            """;

        var calendar = await HolidayCalendar.CreateBuilder().AddSource(new CsvHolidaySource(Utf8(csv), "csv")).BuildAsync();
        var sp = calendar.GetProvider(SaoPauloCapital);

        await Assert.That(sp.GetHolidays(2026).Count).IsEqualTo(3);
        await Assert.That(sp.GetHoliday(new DateOnly(2026, 1, 25))?.Scope).IsEqualTo(HolidayScope.Municipal);
        await Assert.That(calendar.GetProvider(new HolidayLocation("RJ")).GetHolidays(2026).Count).IsEqualTo(1);
    }

    [Test]
    public async Task CsvSource_File()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tec-core-feriados-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "Data;Descricao\n02/11/2026;Finados\n");
        try
        {
            var calendar = await HolidayCalendar.CreateBuilder().AddCsvFile(path).BuildAsync();
            await Assert.That(calendar.National.IsHoliday(new DateOnly(2026, 11, 2))).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task JsonSource_DefaultFormat()
    {
        const string json = """
            [
              { "data": "2026-01-25", "descrição": "Aniversário de São Paulo", "uf": "SP", "codigoIbge": 3550308 },
              { "Data": "09/07/2026", "Descricao": "Revolução Constitucionalista", "UF": "SP", "extra": true },
              { "data": "2026-01-20", "descricao": "São Sebastião", "codigo_ibge": "3304557" },
              { "data": "2026-12-25", "descricao": "Natal", "uf": null }
            ]
            """;

        var calendar = await HolidayCalendar.CreateBuilder().AddSource(new JsonHolidaySource(Utf8(json), "json")).BuildAsync();

        await Assert.That(calendar.Count).IsEqualTo(4);
        await Assert.That(calendar.GetProvider(SaoPauloCapital).GetHolidays(2026).Count).IsEqualTo(3);
        await Assert.That(calendar.GetProvider(HolidayLocation.FromIbgeCode(RioDeJaneiro)).GetHolidays(2026).Count).IsEqualTo(2);
    }

    [Test]
    public async Task JsonSource_BrasilApiFormatAndCustomMapper()
    {
        const string brasilApi = """[{ "date": "2026-04-21", "name": "Tiradentes", "type": "national" }]""";
        var calendar = await HolidayCalendar.CreateBuilder().AddSource(new JsonHolidaySource(Utf8(brasilApi), "api")).BuildAsync();
        await Assert.That(calendar.National.GetHoliday(new DateOnly(2026, 4, 21))?.Description).IsEqualTo("Tiradentes");

        const string custom = """[{ "dia": 3, "mes": 2, "ativo": true }, { "dia": 4, "mes": 2, "ativo": false }]""";
        var mapped = await HolidayCalendar.CreateBuilder()
            .AddSource(new JsonHolidaySource(Utf8(custom), "custom", e => e.GetProperty("ativo").GetBoolean()
                ? new Holiday(new DateOnly(2026, e.GetProperty("mes").GetInt32(), e.GetProperty("dia").GetInt32()), "Custom")
                : null))
            .BuildAsync();
        await Assert.That(mapped.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("""{ "data": "2026-01-01" }""")]
    [Arguments("""[{ "descricao": "Sem data" }]""")]
    [Arguments("""[{ "data": "2026-01-01" }]""")]
    [Arguments("""[{ "data": "31/02/2026", "descricao": "x" }]""")]
    [Arguments("""[{ "data": "2026-01-01", "descricao": "x", "uf": "ZZ" }]""")]
    [Arguments("""[{ "data": "2026-01-01", "descricao": "x", "codigoIbge": "abc" }]""")]
    [Arguments("""[{ "data": "2026-01-01", "descricao": 10 }]""")]
    [Arguments("""[1]""")]
    [Arguments("""[{ "data": 20260101, "descricao": "x" }]""")]
    public async Task JsonSource_InvalidContent_ThrowsHolidaySourceException(string json)
    {
        var builder = HolidayCalendar.CreateBuilder().AddSource(new JsonHolidaySource(Utf8(json), "json"));

        var ex = await Assert.That(async () => { await builder.BuildAsync(); }).ThrowsExactly<HolidaySourceException>();
        await Assert.That(ex!.SourceName).IsEqualTo("json");
    }

    [Test]
    public async Task JsonSource_DoesNotEchoInvalidValues()
    {
        var builder = HolidayCalendar.CreateBuilder()
            .AddSource(new JsonHolidaySource(Utf8("""[{ "data": "<script>", "descricao": "x" }]"""), "json"));

        var ex = await Assert.That(async () => { await builder.BuildAsync(); }).ThrowsExactly<HolidaySourceException>();
        await Assert.That(ex!.Message).DoesNotContain("<script>");
        await Assert.That(ex.Message).Contains("posição 0");
    }

    [Test]
    public async Task DbSource_MapsColumnsByName()
    {
        var table = new DataTable();
        table.Columns.Add("dt_feriado_data", typeof(string));       // não reconhecida
        table.Columns.Add("DATA", typeof(DateTime));
        table.Columns.Add("Descrição", typeof(string));
        table.Columns.Add("uf", typeof(string));
        table.Columns.Add("Codigo_IBGE", typeof(long));
        table.Rows.Add("x", new DateTime(2026, 1, 25), "Aniversário de São Paulo", DBNull.Value, 3550308L);
        table.Rows.Add("x", new DateTime(2026, 7, 9), "Revolução Constitucionalista", "SP", DBNull.Value);
        table.Rows.Add("x", new DateTime(2026, 12, 25), "Natal", DBNull.Value, DBNull.Value);

        FakeDbConnection? connection = null;
        string? executedSql = null;
        var calendar = await HolidayCalendar.CreateBuilder()
            .AddDatabase(() => connection = new FakeDbConnection(table), "SELECT * FROM Feriados WHERE Ativo = @ativo",
                command =>
                {
                    executedSql = command.CommandText;
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@ativo";
                    parameter.Value = true;
                    command.Parameters.Add(parameter);
                })
            .BuildAsync();

        await Assert.That(calendar.Count).IsEqualTo(3);
        await Assert.That(calendar.GetProvider(SaoPauloCapital).GetHolidays(2026).Count).IsEqualTo(3);
        await Assert.That(calendar.GetProvider(new HolidayLocation("SP")).GetHolidays(2026).Count).IsEqualTo(2);
        await Assert.That(executedSql).IsEqualTo("SELECT * FROM Feriados WHERE Ativo = @ativo");
        await Assert.That(connection!.WasOpened).IsTrue();
        await Assert.That(connection.WasDisposed).IsTrue();
    }

    [Test]
    public async Task DbSource_MissingColumnOrInvalidRow_Fails()
    {
        var withoutDescription = new DataTable();
        withoutDescription.Columns.Add("Data", typeof(DateTime));
        withoutDescription.Rows.Add(new DateTime(2026, 1, 1));

        var ex = await Assert.That(async () =>
        {
            await HolidayCalendar.CreateBuilder().AddDatabase(() => new FakeDbConnection(withoutDescription), "SELECT 1").BuildAsync();
        }).ThrowsExactly<HolidaySourceException>();
        await Assert.That(ex!.InnerException).IsTypeOf<InvalidOperationException>();

        var invalidUf = new DataTable();
        invalidUf.Columns.Add("Data", typeof(string));
        invalidUf.Columns.Add("Descricao", typeof(string));
        invalidUf.Columns.Add("Uf", typeof(string));
        invalidUf.Rows.Add("2026-01-01", "Ano Novo", "XX");

        var rowError = await Assert.That(async () =>
        {
            await HolidayCalendar.CreateBuilder().AddDatabase(() => new FakeDbConnection(invalidUf), "SELECT 1").BuildAsync();
        }).ThrowsExactly<HolidaySourceException>();
        await Assert.That(rowError!.Message).Contains("linha 1");
    }

    [Test]
    public async Task DelegateSource_LoadsAndReceivesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken received = default;

        var calendar = await HolidayCalendar.CreateBuilder()
            .AddDelegate(ct =>
            {
                received = ct;
                return Task.FromResult<IEnumerable<Holiday>>([new Holiday(new DateOnly(2026, 3, 19), "São José") { Uf = "CE" }]);
            }, "repositório")
            .BuildAsync(cts.Token);

        await Assert.That(received).IsEqualTo(cts.Token);
        await Assert.That(calendar.GetProvider(new HolidayLocation("CE")).IsHoliday(new DateOnly(2026, 3, 19))).IsTrue();
    }

    [Test]
    public async Task HttpSource_ReadsJsonAndHidesQueryStringInName()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, """[{ "date": "2026-01-01", "name": "Confraternização mundial", "type": "national" }]""");
        using var client = new HttpClient(handler);
        var source = new HttpHolidaySource(client, new Uri("https://api.exemplo.com/feriados?token=segredo"));

        var calendar = await HolidayCalendar.CreateBuilder().AddSource(source).BuildAsync();

        await Assert.That(calendar.National.IsHoliday(new DateOnly(2026, 1, 1))).IsTrue();
        await Assert.That(source.Name).IsEqualTo("https://api.exemplo.com/feriados");
        await Assert.That(handler.LastRequest!.RequestUri!.Query).IsEqualTo("?token=segredo");
    }

    [Test]
    [Arguments("https://svc:S3cr3t@api.exemplo.com/feriados?key=x#frag", "https://api.exemplo.com/feriados")]
    [Arguments("https://svc@api.exemplo.com:8443/v1/feriados", "https://api.exemplo.com:8443/v1/feriados")]
    [Arguments("feriados/2026?token=segredo", "feriados/2026")]
    [Arguments("//svc:S3cr3t@api.exemplo.com/feriados?key=x", "//api.exemplo.com/feriados")]
    public async Task HttpSource_NameHidesCredentials(string url, string expectedName)
    {
        using var client = new HttpClient(new FakeHttpHandler(HttpStatusCode.OK, "[]"));
        var source = new HttpHolidaySource(client, new Uri(url, UriKind.RelativeOrAbsolute));

        await Assert.That(source.Name).IsEqualTo(expectedName);
        await Assert.That(source.Name).DoesNotContain("S3cr3t");
    }

    [Test]
    public async Task CsvSource_InvalidUf_FailsWithLineNumber()
    {
        const string csv = "Data;Descricao;Uf\n01/01/2026;Ano Novo;SP\n02/01/2026;Inválido;XX\n";

        var ex = await Assert.That(async () =>
        {
            await HolidayCalendar.CreateBuilder().AddSource(new CsvHolidaySource(Utf8(csv), "csv")).BuildAsync();
        }).ThrowsExactly<HolidaySourceException>();

        var csvError = ex!.InnerException as TEC.Core.Csv.CsvException;
        await Assert.That(csvError).IsNotNull();
        await Assert.That(csvError!.LineNumber).IsEqualTo(3);
        await Assert.That(csvError.ColumnName).IsEqualTo("Uf");
    }

    [Test]
    public async Task DbSource_IbgeCode_AcceptsIntegralNumbersAndRejectsFractions()
    {
        static DataTable Table(Type codeType, object code)
        {
            var table = new DataTable();
            table.Columns.Add("Data", typeof(DateTime));
            table.Columns.Add("Descricao", typeof(string));
            table.Columns.Add("CodigoIbge", codeType);
            table.Rows.Add(new DateTime(2026, 1, 25), "Aniversário de São Paulo", code);
            return table;
        }

        foreach (var (type, code) in new (Type, object)[] { (typeof(decimal), 3550308m), (typeof(double), 3550308d), (typeof(float), 3550308f) })
        {
            var table = Table(type, code);
            var calendar = await HolidayCalendar.CreateBuilder().AddDatabase(() => new FakeDbConnection(table), "SELECT 1").BuildAsync();
            await Assert.That(calendar.GetProvider(SaoPauloCapital).IsHoliday(new DateOnly(2026, 1, 25))).IsTrue();
        }

        foreach (var (type, code) in new (Type, object)[] { (typeof(decimal), 3550308.5m), (typeof(double), 3550308.5d), (typeof(double), double.NaN), (typeof(double), 1e12d) })
        {
            var table = Table(type, code);
            var ex = await Assert.That(async () =>
            {
                await HolidayCalendar.CreateBuilder().AddDatabase(() => new FakeDbConnection(table), "SELECT 1").BuildAsync();
            }).ThrowsExactly<HolidaySourceException>();
            await Assert.That(ex!.Message).Contains("linha 1");
        }
    }

    [Test]
    public async Task GetHolidays_Range_UsesOnlyDatesInsideTheInterval()
    {
        var calendar = await HolidayCalendar.CreateBuilder()
            .AddHolidays(
            [
                new Holiday(new DateOnly(2025, 12, 31), "Antes") { IbgeCode = SaoPaulo },
                new Holiday(new DateOnly(2026, 1, 25), "Aniversário de São Paulo") { IbgeCode = SaoPaulo },
                new Holiday(new DateOnly(2026, 3, 1), "Aniversário do Rio") { IbgeCode = RioDeJaneiro },
                new Holiday(new DateOnly(2026, 3, 1), "Fim do intervalo") { Uf = "SP" },
                new Holiday(new DateOnly(2026, 3, 2), "Depois") { IbgeCode = SaoPaulo }
            ])
            .BuildAsync();
        var sp = calendar.GetProvider(SaoPauloCapital);

        var range = sp.GetHolidays(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1));
        await Assert.That(range.Select(h => h.Description)).IsEquivalentTo(new[] { "Aniversário de São Paulo", "Fim do intervalo" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        // Intervalo invertido, vazio e fora dos dados
        await Assert.That(sp.GetHolidays(new DateOnly(2026, 3, 1), new DateOnly(2026, 1, 1)).Count).IsEqualTo(2);
        await Assert.That(sp.GetHolidays(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)).Count).IsEqualTo(0);
        await Assert.That(sp.GetHolidays(2030).Count).IsEqualTo(0);
        await Assert.That(sp.GetHolidays(2025).Count).IsEqualTo(1);
    }

    [Test]
    public async Task HttpSource_BrasilApi_OneSourcePerYear()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "[]");
        using var client = new HttpClient(handler);

        await HolidayCalendar.CreateBuilder().AddBrasilApi(client, [2026, 2026, 2027]).BuildAsync();

        await Assert.That(handler.RequestCount).IsEqualTo(2);
        await Assert.That(HttpHolidaySource.BrasilApi(client, 2026).Name).IsEqualTo("https://brasilapi.com.br/api/feriados/v1/2026");
        await Assert.That(() => HttpHolidaySource.BrasilApi(client, 1899)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    // ---------- Falhas ----------

    [Test]
    public async Task OptionalSourceFailure_IsIgnoredAndReported()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.ServiceUnavailable, "");
        using var client = new HttpClient(handler);
        var reported = new List<HolidaySourceFailure>();

        var calendar = await HolidayCalendar.CreateBuilder()
            .AddBrazilianNational()
            .AddHttp(client, new Uri("https://api.exemplo.com/feriados"), optional: true)
            .AddCsvFile(Path.Combine(Path.GetTempPath(), $"nao-existe-{Guid.NewGuid():N}.csv"), optional: true)
            .OnSourceError(reported.Add)
            .BuildAsync();

        await Assert.That(calendar.Failures.Count).IsEqualTo(2);
        await Assert.That(reported.Count).IsEqualTo(2);
        await Assert.That(calendar.Failures[0].Exception).IsTypeOf<HttpRequestException>();
        await Assert.That(calendar.Failures[1].Exception).IsTypeOf<FileNotFoundException>();
        await Assert.That(calendar.National.IsHoliday(new DateOnly(2026, 1, 1))).IsTrue();
    }

    [Test]
    public async Task RequiredSourceFailure_Throws()
    {
        var builder = HolidayCalendar.CreateBuilder()
            .AddBrazilianNational()
            .AddJsonFile(Path.Combine(Path.GetTempPath(), $"nao-existe-{Guid.NewGuid():N}.json"));

        var ex = await Assert.That(async () => { await builder.BuildAsync(); }).ThrowsExactly<HolidaySourceException>();
        await Assert.That(ex!.InnerException).IsTypeOf<FileNotFoundException>();
    }

    [Test]
    public async Task Builder_RejectsInvalidUsage()
    {
        await Assert.That(async () => { await HolidayCalendar.CreateBuilder().BuildAsync(); }).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => HolidayCalendar.CreateBuilder().AddBrazilianNational().AddBrazilianNational())
            .ThrowsExactly<InvalidOperationException>();

        var nullItem = HolidayCalendar.CreateBuilder().AddDelegate(_ => Task.FromResult<IEnumerable<Holiday>>([null!]), "nulos");
        await Assert.That(async () => { await nullItem.BuildAsync(); }).ThrowsExactly<HolidaySourceException>();

        var nullResult = HolidayCalendar.CreateBuilder().AddDelegate(_ => Task.FromResult<IEnumerable<Holiday>>(null!), "nulo");
        await Assert.That(async () => { await nullResult.BuildAsync(); }).ThrowsExactly<HolidaySourceException>();
    }

    [Test]
    public async Task Build_Canceled_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var builder = HolidayCalendar.CreateBuilder().AddDelegate(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        }, "lenta", optional: true);

        await Assert.That(async () => { await builder.BuildAsync(cts.Token); }).Throws<OperationCanceledException>();
    }

    // ---------- Dublês ----------

    private sealed class FakeHttpHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class FakeDbConnection(DataTable table) : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        public bool WasOpened { get; private set; }

        public bool WasDisposed { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = "";

        public override string Database => "fake";

        public override string DataSource => "fake";

        public override string ServerVersion => "1";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open()
        {
            _state = ConnectionState.Open;
            WasOpened = true;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeDbCommand(this, table);

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FakeDbCommand(DbConnection connection, DataTable table) : DbCommand
    {
        private readonly FakeParameters _parameters = new();

        [AllowNull]
        public override string CommandText { get; set; } = "";

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; } = connection;

        protected override DbParameterCollection DbParameterCollection => _parameters;

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object? ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new FakeParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            DbConnection!.State == ConnectionState.Open ? table.CreateDataReader() : throw new InvalidOperationException("Conexão fechada.");
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = "";

        [AllowNull]
        public override string SourceColumn { get; set; } = "";

        public override object? Value { get; set; }

        public override bool SourceColumnNullMapping { get; set; }

        public override int Size { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class FakeParameters : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => _items;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string value) => _items.Any(p => p.ParameterName == value);

        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);

        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }
}
