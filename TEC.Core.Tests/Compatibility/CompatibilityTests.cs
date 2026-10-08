using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using TEC.Core.Common.Results;
using TEC.Core.Common.Serialization;
using TEC.Core.Csv;
using TEC.Core.Exceptions;
using TEC.Core.Polyfills;
using TEC.Core.Responses;
using TEC.Core.Text.Codecs;

namespace TEC.Core.Tests.Compatibility;

/// <summary>
/// Garante que os trechos com <c>#if NET9_0_OR_GREATER</c>/polyfills e os caminhos compatíveis com AOT
/// se comportam de forma idêntica em <c>net8.0</c> e <c>net10.0</c> (os testes rodam nos dois runtimes).
/// </summary>
public class CompatibilityTests
{
    private static readonly byte[][] Samples =
    [
        [],
        [0x00],
        [0xFB, 0xFF],
        [0xFF, 0xFE, 0xFD],
        [0x14, 0xFB, 0x9C, 0x03, 0xD9, 0x7E],
        [.. Enumerable.Range(0, 256).Select(i => (byte)i)],
        RandomNumberGenerator.GetBytes(257)
    ];

    [Test]
    public async Task ToHexLower_MatchesReference()
    {
        foreach (var bytes in Samples)
            await Assert.That(EncodingCompat.ToHexLower(bytes)).IsEqualTo(Convert.ToHexString(bytes).ToLowerInvariant());
    }

    [Test]
    public async Task TryFromHex_AcceptsAnyCase_AndRoundTrips()
    {
        foreach (var bytes in Samples)
        {
            var lower = EncodingCompat.ToHexLower(bytes);
            await Assert.That(EncodingCompat.TryFromHex(lower, out var fromLower)).IsTrue();
            await Assert.That(EncodingCompat.TryFromHex(lower.ToUpperInvariant(), out var fromUpper)).IsTrue();
            await Assert.That(fromLower.SequenceEqual(bytes)).IsTrue();
            await Assert.That(fromUpper.SequenceEqual(bytes)).IsTrue();
        }

        await Assert.That(EncodingCompat.TryFromHex("aBcD", out var mixed)).IsTrue();
        await Assert.That(mixed.SequenceEqual(new byte[] { 0xAB, 0xCD })).IsTrue();
    }

    [Test]
    [Arguments("abc")]          // tamanho ímpar
    [Arguments("0g")]           // fora de 0-9/a-f
    [Arguments("zz")]
    [Arguments(" 0a")]
    [Arguments("0a ")]
    [Arguments("0x0a")]
    [Arguments("０１")] // dígitos de largura total (não ASCII)
    [Arguments("٠١")]           // dígitos arábicos
    public async Task TryFromHex_InvalidInput_ReturnsFalse(string hex)
    {
        await Assert.That(EncodingCompat.TryFromHex(hex, out _)).IsFalse();
        // Mesmo resultado da API do .NET (que lança em vez de retornar false)
        await Assert.That(() => Convert.FromHexString(hex)).Throws<FormatException>();
    }

    [Test]
    public async Task TryFromHex_Empty_ReturnsEmptyArray()
    {
        await Assert.That(EncodingCompat.TryFromHex("", out var bytes)).IsTrue();
        await Assert.That(bytes.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ToBase64Url_KnownVectors()
    {
        await Assert.That(Base64UrlEncoder.Encode([])).IsEqualTo("");
        await Assert.That(Base64UrlEncoder.Encode([0xFB, 0xFF])).IsEqualTo("-_8");
        await Assert.That(Base64UrlEncoder.Encode([0xFF, 0xFE, 0xFD])).IsEqualTo("__79");
        await Assert.That(Base64UrlEncoder.Encode(Encoding.ASCII.GetBytes("f"))).IsEqualTo("Zg");
        await Assert.That(Base64UrlEncoder.Encode(Encoding.ASCII.GetBytes("fo"))).IsEqualTo("Zm8");
        await Assert.That(Base64UrlEncoder.Encode(Encoding.ASCII.GetBytes("foobar"))).IsEqualTo("Zm9vYmFy");
    }

    [Test]
    public async Task ToBase64Url_MatchesReference()
    {
        foreach (var bytes in Samples)
        {
            var expected = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            await Assert.That(Base64UrlEncoder.Encode(bytes)).IsEqualTo(expected);
#if NET9_0_OR_GREATER
            await Assert.That(Base64UrlEncoder.Encode(bytes)).IsEqualTo(System.Buffers.Text.Base64Url.EncodeToString(bytes));
#endif
        }
    }

    [Test]
    public async Task OverloadResolutionPriority_WorksOnEveryTarget()
    {
        // No net8.0 o atributo vem do polyfill interno; a chamada abaixo seria ambígua sem ele
        var exception = new ConcurrencyException(null);
        await Assert.That(exception.Message).IsEqualTo(ConcurrencyException.DefaultMessage);
    }

    [Test]
    public async Task Json_SourceGenerated_MatchesReflectionDefaults()
    {
        var value = new CompatPayload { FullName = "João <script>", Status = CompatStatus.UnderReview, Total = 12.5m };
        var context = CreateContext();

        var reflection = value.ToJson();
        await Assert.That(value.ToJson(context.CompatPayload)).IsEqualTo(reflection);
        await Assert.That(value.ToJson(context)).IsEqualTo(reflection);
        await Assert.That(reflection).IsEqualTo("{\"fullName\":\"João \\u003Cscript\\u003E\",\"status\":\"underReview\",\"total\":12.5}");

        var indented = CreateContext(writeIndented: true);
        await Assert.That(value.ToJson(indented.CompatPayload)).IsEqualTo(value.ToJson(indented: true));
    }

    [Test]
    public async Task Json_SourceGenerated_Deserializes()
    {
        var context = CreateContext();
        const string json = "{\"fullName\":\"Ana\",\"status\":\"underReview\",\"total\":1}";

        var parsed = json.FromJson(context.CompatPayload);
        await Assert.That(parsed!.Status).IsEqualTo(CompatStatus.UnderReview);
        await Assert.That(json.FromJson<CompatPayload>(context)!.FullName).IsEqualTo("Ana");

        await Assert.That(json.TryFromJson(context.CompatPayload, out var ok)).IsTrue();
        await Assert.That(ok!.Total).IsEqualTo(1m);
        await Assert.That("{invalido".TryFromJson(context.CompatPayload, out _)).IsFalse();
        await Assert.That("{\"status\":1}".TryFromJson<CompatPayload>(context, out _)).IsFalse(); // números recusados, como em Options
        await Assert.That(((string?)null).TryFromJson(context.CompatPayload, out _)).IsFalse();
        await Assert.That(() => "".FromJson(context.CompatPayload)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Json_SourceGenerated_LibraryTypes_MatchReflection()
    {
        var context = CreateContext();
        var ok = ApiResponse<CompatPayload>.Ok(new CompatPayload { FullName = "Ana", Status = CompatStatus.New, Total = 3m }, "Pronto");
        var fail = ApiResponse<CompatPayload>.ValidationError(new ApiError("CAMPO", "Inválido", "nome"));
        var error = Error.Validation("X", "Y", "campo");

        await Assert.That(ok.ToJson(context)).IsEqualTo(ok.ToJson());
        await Assert.That(fail.ToJson(context)).IsEqualTo(fail.ToJson());
        await Assert.That(error.ToJson(context)).IsEqualTo(error.ToJson());
        await Assert.That(error.ToJson(context).FromJson<Error>(context)).IsEqualTo(error);
    }

    [Test]
    public async Task Json_ContextWithoutType_Throws()
    {
        var context = CreateContext();
        await Assert.That(() => new Version(1, 0).ToJson(context)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Json_CreateOptionsWithResolver_DoesNotFreeze()
    {
        var options = JsonDefaults.CreateOptions(CompatJsonContext.Default);
        await Assert.That(options.IsReadOnly).IsFalse();
        await Assert.That(options.TypeInfoResolver).IsSameReferenceAs(CompatJsonContext.Default);
        await Assert.That(JsonDefaults.Options).IsSameReferenceAs(JsonDefaults.Options);
        await Assert.That(JsonDefaults.Options.IsReadOnly).IsTrue();
    }

    // Caminho AOT documentado: opções padrão + conversor de enum genérico + contexto gerado
    private static CompatJsonContext CreateContext(bool writeIndented = false)
    {
        var options = JsonDefaults.CreateOptions(null, writeIndented);
        options.Converters.Add(JsonDefaults.CreateEnumConverter<CompatStatus>());
        options.Converters.Add(JsonDefaults.CreateEnumConverter<ErrorType>());
        return new CompatJsonContext(options);
    }

    [Test]
    public async Task Csv_EmptyFieldInNonNullableValueTypes_ReadsDefault()
    {
        const string csv = "Integer;Date;Id;Active;Status;Amount;Time\n;;;;;;\n";
        var reader = new CsvReader();
        var rows = await reader.ReadAsync<CompatCsvRow>(new MemoryStream(Encoding.UTF8.GetBytes(csv))).ToListAsync();

        await Assert.That(rows.Count).IsEqualTo(1);
        var row = rows[0];
        await Assert.That(row.Integer).IsEqualTo(0);
        await Assert.That(row.Date).IsEqualTo(default(DateTime));
        await Assert.That(row.Id).IsEqualTo(Guid.Empty);
        await Assert.That(row.Active).IsFalse();
        await Assert.That(row.Status).IsEqualTo(default(CompatStatus));
        await Assert.That(row.Amount).IsEqualTo(0m);
        await Assert.That(row.Time).IsEqualTo(default(TimeOnly));
    }
}

public enum CompatStatus { New = 1, UnderReview = 2 }

public sealed class CompatPayload
{
    public string? FullName { get; set; }

    public CompatStatus Status { get; set; }

    public decimal Total { get; set; }

    public string? Optional { get; set; }
}

// Valores iniciais diferentes do padrão: campo vazio no CSV deve resultar em default(T), como no Activator.CreateInstance
public sealed class CompatCsvRow
{
    public int Integer { get; set; } = 7;

    public DateTime Date { get; set; } = DateTime.MaxValue;

    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Active { get; set; } = true;

    public CompatStatus Status { get; set; } = CompatStatus.UnderReview;

    public decimal Amount { get; set; } = 3m;

    public TimeOnly Time { get; set; } = TimeOnly.MaxValue;
}

[JsonSerializable(typeof(CompatPayload))]
[JsonSerializable(typeof(ApiResponse<CompatPayload>))]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(TEC.Core.Cryptography.Asymmetric.RsaKeyPair))]
internal sealed partial class CompatJsonContext : JsonSerializerContext;
