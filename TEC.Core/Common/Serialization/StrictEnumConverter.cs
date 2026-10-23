using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TEC.Core.Common.Serialization;

/// <summary>
/// Conversor de enum como texto em camelCase que só aceita, na leitura, nomes de membros definidos.
/// </summary>
/// <remarks>
/// O <see cref="JsonStringEnumConverter{TEnum}"/> com <c>allowIntegerValues: false</c> recusa só o número JSON: no .NET 8
/// ele ainda aceita número entre aspas (<c>"999"</c>) e, em qualquer versão, combinações por vírgula
/// (<c>"a, b"</c>) mesmo em enums sem <c>[Flags]</c>, produzindo valores fora do enum ou um membro diferente do enviado.
/// Este conversor delega a leitura e a escrita ao conversor do runtime e recusa esses casos.
/// </remarks>
internal sealed class StrictEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private static readonly bool IsFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);
    private static readonly ulong DefinedBits = Enum.GetValues<TEnum>().Aggregate(0UL, (bits, value) => bits | Bits(value));

    private readonly JsonStringEnumConverter<TEnum> _factory = new(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
    private JsonConverter<TEnum>? _inner;

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        EnsureNames(ref reader);
        return EnsureDefined(Inner(options).Read(ref reader, typeToConvert, options));
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        Inner(options).Write(writer, value, options);

    // O conversor do runtime não expõe a leitura/escrita de enum como chave de dicionário a conversores externos:
    // a chave é convertida como um valor JSON de texto.
    public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        EnsureNames(ref reader);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
            writer.WriteStringValue(reader.GetString());

        var value = new Utf8JsonReader(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        value.Read();
        return EnsureDefined(Inner(options).Read(ref value, typeToConvert, options));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        using var buffer = new MemoryStream();
        using (var nameWriter = new Utf8JsonWriter(buffer))
            Inner(options).Write(nameWriter, value, options);

        var name = new Utf8JsonReader(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        name.Read();
        writer.WritePropertyName(name.GetString()!);
    }

    // Corrida benigna: duas threads podem criar o conversor interno, que não tem estado próprio
    private JsonConverter<TEnum> Inner(JsonSerializerOptions options) =>
        _inner ??= (JsonConverter<TEnum>)_factory.CreateConverter(typeof(TEnum), options);

    /// <summary>O texto deve ser um nome (ou, em <c>[Flags]</c>, nomes separados por vírgula), nunca um número.</summary>
    private static void EnsureNames(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.PropertyName))
            throw Invalid();

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text) || (!IsFlags && text.Contains(',', StringComparison.Ordinal)))
            throw Invalid();

        foreach (var part in text.Split(','))
        {
            var name = part.AsSpan().Trim();
            if (name.IsEmpty
                || long.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                || ulong.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                throw Invalid();
            }
        }
    }

    private static TEnum EnsureDefined(TEnum value)
    {
        bool defined = IsFlags ? (Bits(value) & ~DefinedBits) == 0 : Enum.IsDefined(value);
        return defined ? value : throw Invalid();
    }

    // Bits do valor em qualquer tipo subjacente (inclusive negativos e ulong acima de long.MaxValue)
    private static ulong Bits(TEnum value) => Type.GetTypeCode(typeof(TEnum)) == TypeCode.UInt64
        ? Convert.ToUInt64(value, CultureInfo.InvariantCulture)
        : unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));

    // Sem o valor recebido na mensagem: ele vem de fora e a exceção costuma ir para o log
    private static JsonException Invalid() => new($"Valor inválido para o enum {typeof(TEnum).Name}.");
}

/// <summary>
/// Aplica o <see cref="StrictEnumConverter{TEnum}"/> a qualquer enum (caminho por reflexão; em Native AOT use
/// <see cref="JsonDefaults.CreateEnumConverter{TEnum}"/> para cada enum).
/// </summary>
internal sealed class StrictEnumConverterFactory : JsonConverterFactory
{
    [RequiresDynamicCode(JsonDefaults.EnumConverterMessage)]
    public StrictEnumConverterFactory()
    {
    }

    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(StrictEnumConverter<>))]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "O construtor da fábrica já exige [RequiresDynamicCode].")]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "O tipo é um enum em uso (CanConvert).")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Restrição struct: enums são tipos-valor, sem construtor a preservar.")]
    [UnconditionalSuppressMessage("Trimming", "IL2071", Justification = "Restrição struct: enums são tipos-valor, sem construtor a preservar.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Construtor preservado por [DynamicDependency].")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert))!;
}
