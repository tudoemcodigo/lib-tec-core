using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using TEC.Core.Common.Results;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Csv;
using TEC.Core.Exceptions;
using TEC.Core.Responses;
using TEC.Core.Text.Codecs;

namespace TEC.Core.Tests.Compatibility;

/// <summary>
/// Confere as codificações (hexadecimal, Base64Url) contra implementações de referência e os caminhos compatíveis
/// com AOT (JSON por contexto gerado, CSV sem <c>Activator</c>).
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
    public async Task DependencyInjectionAbstractions_MatchesTheRuntimeMajorVersion()
    {
        // R11: a Abstractions referenciada acompanha a versão major do runtime do alvo (framework compartilhado)
        var reference = typeof(TEC.Core.DependencyInjection.TecCoreOptions).Assembly.GetReferencedAssemblies()
            .Single(a => a.Name == "Microsoft.Extensions.DependencyInjection.Abstractions");

        await Assert.That(reference.Version!.Major).IsEqualTo(Environment.Version.Major);
    }

    [Test]
    public async Task ComputeHash_HexIsLowercaseReference()
    {
        foreach (var bytes in Samples)
        {
            using var stream = new MemoryStream(bytes);
            var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await Assert.That(await HashHelper.ComputeHashAsync(stream)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task FixedTimeEqualsHex_AcceptsAnyCase()
    {
        foreach (var bytes in Samples)
        {
            var lower = Convert.ToHexString(bytes).ToLowerInvariant();
            await Assert.That(HashHelper.FixedTimeEqualsHex(lower, lower.ToUpperInvariant())).IsTrue();
        }

        await Assert.That(HashHelper.FixedTimeEqualsHex("aBcD", "ABcd")).IsTrue();
        await Assert.That(HashHelper.FixedTimeEqualsHex("abcd", "abce")).IsFalse();
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
    public async Task FixedTimeEqualsHex_InvalidInput_ReturnsFalse(string hex)
    {
        await Assert.That(HashHelper.FixedTimeEqualsHex(hex, hex)).IsFalse();
        // Mesmo resultado da API do .NET (que lança em vez de retornar false)
        await Assert.That(() => Convert.FromHexString(hex)).Throws<FormatException>();
    }

    [Test]
    public async Task FixedTimeEqualsHex_Empty_IsEqual()
    {
        await Assert.That(HashHelper.FixedTimeEqualsHex("", "")).IsTrue();
        await Assert.That(HashHelper.FixedTimeEqualsHex("", "00")).IsFalse();
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
        }
    }

    [Test]
    public async Task OverloadResolutionPriority_ResolvesNullToDefaultMessage()
    {
        // Sem [OverloadResolutionPriority] a chamada abaixo seria ambígua
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
