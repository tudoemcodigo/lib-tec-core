using System.Collections;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Cryptography.Hybrid;
using TEC.Core.Cryptography.Symmetric;
using TEC.Core.Csv;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;
using TEC.Core.Exceptions;
using TEC.Core.Responses;
using TEC.Core.Text.Generation;
using TEC.Core.Text.Masking;

namespace TEC.Core.Tests.Security.Adversarial;

/// <summary>
/// Vazamento de dados: um marcador secreto é injetado nas entradas e o teste varre mensagens de exceção (cadeia inteira,
/// <c>ToString()</c>, <c>Data</c>), nomes de fonte, respostas de API e JSON, garantindo que ele nunca aparece.
/// </summary>
public class LeakageTests
{
    private static readonly string Secret = "SEGREDO" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    // ---------- CSV ----------

    public sealed class Row
    {
        public int Number { get; set; }
        public DateOnly Date { get; set; }
        public DayOfWeek Day { get; set; }
        public Guid Id { get; set; }
    }

    [Test]
    [Arguments("Number;Date;Day;Id\n{0};01/01/2026;Monday;" + "00000000-0000-0000-0000-000000000000")]
    [Arguments("Number;Date;Day;Id\n1;{0};Monday;00000000-0000-0000-0000-000000000000")]
    [Arguments("Number;Date;Day;Id\n1;01/01/2026;{0};00000000-0000-0000-0000-000000000000")]
    [Arguments("Number;Date;Day;Id\n1;01/01/2026;Monday;{0}")]
    [Arguments("Number;Date;Day;Id\n1;\"{0}\"x;Monday;00000000-0000-0000-0000-000000000000")]
    public async Task Csv_InvalidValue_ErrorNeverContainsTheValue(string template)
    {
        var csv = string.Format(System.Globalization.CultureInfo.InvariantCulture, template, Secret);

        var thrown = await Assert.That(async () => { await ReadAsync(csv, new CsvOptions()); }).ThrowsExactly<CsvException>();
        AssertNoSecret(thrown!);
        await Assert.That(thrown!.RawValue).IsNull();

        // Linha descartada (SkipInvalidRows) ou erro de estrutura, que interrompe a leitura mesmo assim
        var reported = new List<CsvException>();
        try
        {
            await ReadAsync(csv, new CsvOptions { SkipInvalidRows = true, OnInvalidRow = reported.Add });
        }
        catch (CsvException structural)
        {
            reported.Add(structural);
        }

        await Assert.That(reported).HasSingleItem();
        foreach (var error in reported)
            AssertNoSecret(error);
    }

    [Test]
    public async Task HolidaySources_InvalidContent_ErrorChainNeverContainsTheValue()
    {
        string[] csvs =
        [
            $"Data;Descricao;Uf;CodigoIbge\n01/01/2026;Feriado;{Secret};",
            $"Data;Descricao;Uf;CodigoIbge\n{Secret};Feriado;;",
            $"Data;Descricao;Uf;CodigoIbge\n01/01/2026;Feriado;;{Secret}",
        ];
        string[] jsons =
        [
            $$"""[{"data":"2026-01-01","descricao":"x","uf":"{{Secret}}"}]""",
            $$"""[{"data":"{{Secret}}","descricao":"x"}]""",
            $$"""[{"data":"2026-01-01","descricao":"x","codigoIbge":"{{Secret}}"}]""",
            $$"""[{"data":"2026-01-01","descricao":"x","{{Secret}}":1}, "{{Secret}}"]""",
        ];

        foreach (var (content, isCsv) in csvs.Select(c => (c, true)).Concat(jsons.Select(j => (j, false))))
        {
            Func<Stream> open = () => new MemoryStream(Encoding.UTF8.GetBytes(content));
            IHolidaySource source = isCsv ? new CsvHolidaySource(open, "fonte") : new JsonHolidaySource(open, "fonte");
            var thrown = await Assert.That(async () => { await HolidayCalendar.CreateBuilder().AddSource(source).BuildAsync(); })
                .ThrowsExactly<HolidaySourceException>();
            AssertNoSecret(thrown!);
        }
    }

    [Test]
    public async Task HttpHolidaySource_CredentialsInUrl_NeverReachNameFailuresOrExceptions()
    {
        using var client = new HttpClient(new StatusHandler(HttpStatusCode.ServiceUnavailable));
        var uri = new Uri($"https://usuario:{Secret}@api.exemplo.com/feriados?token={Secret}#{Secret}");
        var failures = new List<HolidaySourceFailure>();

        var calendar = await HolidayCalendar.CreateBuilder().AddBrazilianNational().AddHttp(client, uri, optional: true)
            .OnSourceError(failures.Add).BuildAsync();
        await Assert.That(failures).HasSingleItem();
        await Assert.That(failures[0].ToString()).DoesNotContain(Secret);
        await Assert.That(calendar.Failures[0].SourceName).DoesNotContain(Secret);
        AssertNoSecret(failures[0].Exception);

        var thrown = await Assert.That(async () => { await HolidayCalendar.CreateBuilder().AddHttp(client, uri).BuildAsync(); })
            .ThrowsExactly<HolidaySourceException>();
        AssertNoSecret(thrown!);
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(Secret) });
    }

    // ---------- JSON ----------

    public sealed record Payload(int Number, DateOnly Date, DayOfWeek Day, Guid Id);

    [Test]
    [Arguments("""{"number":"{0}"}""")]
    [Arguments("""{"date":"{0}"}""")]
    [Arguments("""{"day":"{0}"}""")]
    [Arguments("""{"id":"{0}"}""")]
    public async Task Json_InvalidValue_ExceptionNeverContainsTheValue(string template)
    {
        var json = template.Replace("{0}", Secret, StringComparison.Ordinal);
        var thrown = await Assert.That(() => json.FromJson<Payload>()).ThrowsExactly<JsonException>();
        AssertNoSecret(thrown!);
    }

    // ---------- Criptografia ----------

    [Test]
    public async Task Cryptography_Failures_NeverContainKeysOrPlaintext()
    {
        var aes = new AesGcmCryptography();
        var key = aes.GenerateKeyBase64();
        var otherKey = aes.GenerateKeyBase64();
        var cipher = aes.Encrypt(Secret, key);

        // Chave errada, texto cifrado inválido e chave fora do formato
        AssertNoSecretAnd(await Assert.That(() => aes.Decrypt(cipher, otherKey)).Throws<CryptographicException>(), key, otherKey);
        AssertNoSecretAnd(await Assert.That(() => aes.Decrypt(Secret + "==", key)).Throws<CryptographicException>(), key);
        AssertNoSecretAnd(await Assert.That(() => aes.Encrypt("texto", Secret)).Throws<ArgumentException>());

        var rsa = new RsaCryptography();
        var keys = rsa.GenerateKeyPair();
        var other = rsa.GenerateKeyPair();
        var hybrid = new HybridCryptography();
        var hybridCipher = hybrid.Encrypt(Secret, keys.PublicKeyPem);

        AssertNoSecretAnd(await Assert.That(() => hybrid.Decrypt(hybridCipher, other.PrivateKeyPem)).Throws<CryptographicException>(), PemBody(other.PrivateKeyPem));
        AssertNoSecretAnd(await Assert.That(() => rsa.Decrypt(rsa.Encrypt(Secret, keys.PublicKeyPem), other.PrivateKeyPem)).Throws<CryptographicException>(), PemBody(other.PrivateKeyPem));
        AssertNoSecretAnd(await Assert.That(() => rsa.Encrypt("x", $"-----BEGIN PUBLIC KEY-----\n{Secret}\n-----END PUBLIC KEY-----")).Throws<Exception>());
        AssertNoSecretAnd(await Assert.That(() => hybrid.Encrypt("x", Secret)).Throws<Exception>());
    }

    [Test]
    public async Task RsaKeyPair_PrivateKeyNeverAppearsInTextOrJson()
    {
        var keys = new RsaCryptography().GenerateKeyPair();
        var privateBody = PemBody(keys.PrivateKeyPem);
        var wrapper = new { Chaves = keys, Lista = new[] { keys } };

        await Assert.That(keys.ToString()).DoesNotContain(privateBody);
        await Assert.That($"{keys}").DoesNotContain(privateBody);
        await Assert.That(keys.ToJson()).DoesNotContain(privateBody);
        await Assert.That(JsonSerializer.Serialize(wrapper, JsonDefaults.Options)).DoesNotContain(privateBody);
        await Assert.That((keys with { }).ToString()).DoesNotContain(privateBody);
    }

    [Test]
    public async Task PasswordHasher_NeverEchoesThePassword()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var longPassword = Secret + new string('x', Pbkdf2PasswordHasher.MaxPasswordLength);

        AssertNoSecretAnd(await Assert.That(() => hasher.Hash(longPassword)).Throws<ArgumentException>());
        await Assert.That(hasher.Hash(Secret)).DoesNotContain(Secret);
        await Assert.That(hasher.Verify(Secret, $"PBKDF2-SHA256$100000${Secret}${Secret}")).IsFalse();
    }

    // ---------- Respostas de API ----------

    public static IEnumerable<Func<Exception>> ExceptionsCarryingTheSecret()
    {
        var inner = new InvalidOperationException($"Detalhe interno {Secret}");
        yield return () => new InvalidOperationException($"Falha {Secret}", inner);
        yield return () => new IntegrationException($"Servico{Secret}", $"Timeout {Secret}", inner);
        yield return () => new InvalidConfigurationException($"Config:{Secret}", inner);
        yield return () => new BusinessException("REGRA", "Regra violada.", inner);
        yield return () => new ConflictException("CONFLITO", "Conflito.", inner);
        yield return () => new ConcurrencyException(inner);
        yield return () => new NotFoundException("NAO_ENCONTRADO", "Não encontrado.", inner);
        yield return () => new ForbiddenException("NEGADO", "Acesso negado.", inner);
        yield return () => new UnauthenticatedException("NAO_AUTENTICADO", "Não autenticado.", inner);
        yield return () => new AggregateException(inner, new HttpRequestException(Secret));
        yield return () => new CryptographicException(Secret);
    }

    [Test]
    [MethodDataSource(nameof(ExceptionsCarryingTheSecret))]
    public async Task ApiResponse_FromException_NeverExposesInternalDetails(Exception exception)
    {
        var response = ApiResponse.FromException(exception, "trace-1");
        var typed = ApiResponse<int>.FromException(exception);

        await Assert.That(response.ToJson()).DoesNotContain(Secret);
        await Assert.That(typed.ToJson()).DoesNotContain(Secret);
        await Assert.That(response.ToString()).DoesNotContain(Secret);
    }

    // ---------- Mascaramento ----------

    [Test]
    public async Task Masking_NeverRevealsTheHiddenParts()
    {
        for (int i = 0; i < 2_000; i++)
        {
            var cpf = DocumentGenerator.GenerateCpf();
            var masked = SensitiveDataMasker.MaskCpf(cpf);
            await Assert.That(masked).IsEqualTo($"***.{cpf[3..6]}.{cpf[6..9]}-**");
            await Assert.That(masked.Count(char.IsAsciiDigit)).IsEqualTo(6);

            var card = "4111" + DocumentGenerator.GenerateCpf()[..8] + "1234";
            await Assert.That(SensitiveDataMasker.MaskCreditCard(card).Count(char.IsAsciiDigit)).IsLessThanOrEqualTo(4);
        }

        var email = SensitiveDataMasker.MaskEmail($"{Secret.ToLowerInvariant()}@empresa.com.br");
        await Assert.That(email).DoesNotContain(Secret.ToLowerInvariant()[2..]);
        await Assert.That(email).EndsWith("@empresa.com.br");
    }

    // ---------- Apoio ----------

    private static async Task ReadAsync(string csv, CsvOptions options)
    {
        await foreach (var _ in new CsvReader(options).ReadAsync<Row>(new MemoryStream(Encoding.UTF8.GetBytes(csv)))) { }
    }

    private static string PemBody(string pem) =>
        pem.Split('\n').Where(line => !line.StartsWith("-----", StringComparison.Ordinal)).Skip(2).First().Trim();

    private static void AssertNoSecretAnd(Exception? exception, params string[] others)
    {
        AssertNoSecret(exception!);
        foreach (var other in others)
        {
            if (Describe(exception!).Contains(other, StringComparison.Ordinal))
                throw new InvalidOperationException($"Exceção {exception!.GetType().Name} expõe material sensível.");
        }
    }

    // Mensagem, ToString (com a cadeia de InnerException e o stack trace), Data e propriedades específicas
    private static void AssertNoSecret(Exception exception)
    {
        var text = Describe(exception);
        if (text.Contains(Secret, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"O valor secreto vazou em {exception.GetType().Name}:{Environment.NewLine}{text}");
    }

    private static string Describe(Exception exception)
    {
        var text = new StringBuilder(exception.ToString());
        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);
            foreach (DictionaryEntry entry in current.Data)
                text.AppendLine($"{entry.Key}={entry.Value}");
            if (current is CsvException csv)
                text.AppendLine(csv.RawValue).AppendLine(csv.ColumnName);
            if (current is HolidaySourceException source)
                text.AppendLine(source.SourceName);
            if (current is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions)
                    text.AppendLine(Describe(inner));
        }

        return text.ToString();
    }
}
