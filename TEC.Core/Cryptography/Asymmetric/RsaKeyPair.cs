using System.Text.Json;
using System.Text.Json.Serialization;

namespace TEC.Core.Cryptography.Asymmetric;

/// <summary>
/// Par de chaves RSA no formato PEM.
/// </summary>
/// <param name="PublicKeyPem">Chave pública (SubjectPublicKeyInfo), pode ser distribuída.</param>
/// <param name="PrivateKeyPem">
/// Chave privada (PKCS#8), deve ser mantida em segredo (cofre de segredos, nunca no código ou em logs).
/// Não é incluída em serializações JSON nem no <see cref="ToString"/> para evitar vazamentos acidentais.
/// </param>
/// <remarks>
/// Como a chave privada nunca é serializada, o par <b>não pode ser desserializado</b> de JSON: a desserialização lança
/// <see cref="JsonException"/> (em vez de criar um par com a chave privada nula). Guarde as duas chaves PEM como texto
/// (ex.: no cofre de segredos) e recrie o par com o construtor.
/// </remarks>
[JsonConverter(typeof(RsaKeyPairJsonConverter))]
public sealed record RsaKeyPair(string PublicKeyPem, string PrivateKeyPem)
{
    // A validação fica no inicializador (construtor) e também no init (expressão "with")

    /// <summary>Chave pública (SubjectPublicKeyInfo).</summary>
    /// <exception cref="ArgumentException">Valor nulo, vazio ou só com espaços.</exception>
    public string PublicKeyPem
    {
        get;
        init => field = RequireKey(value, nameof(PublicKeyPem));
    } = RequireKey(PublicKeyPem, nameof(PublicKeyPem));

    /// <summary>Chave privada (PKCS#8). Não é serializada em JSON.</summary>
    /// <exception cref="ArgumentException">Valor nulo, vazio ou só com espaços.</exception>
    [JsonIgnore]
    public string PrivateKeyPem
    {
        get;
        init => field = RequireKey(value, nameof(PrivateKeyPem));
    } = RequireKey(PrivateKeyPem, nameof(PrivateKeyPem));

    /// <summary>Evita que a chave privada seja exposta acidentalmente em logs.</summary>
    public override string ToString() => $"RsaKeyPair {{ PublicKeyPem = {PublicKeyPem}, PrivateKeyPem = *** }}";

    private static string RequireKey(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }
}

/// <summary>
/// Conversor JSON de <see cref="RsaKeyPair"/>: escreve somente a chave pública e recusa a leitura
/// (a chave privada não é serializada, então o par não pode ser reconstruído a partir do JSON).
/// Compatível com Native AOT e com <see cref="JsonSerializerContext"/> gerado.
/// </summary>
public sealed class RsaKeyPairJsonConverter : JsonConverter<RsaKeyPair>
{
    /// <inheritdoc />
    /// <exception cref="JsonException">Sempre: <see cref="RsaKeyPair"/> não é desserializável.</exception>
    public override RsaKeyPair Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new JsonException(
            "RsaKeyPair não pode ser desserializado de JSON: a chave privada (PrivateKeyPem) não é serializada, por segurança. " +
            "Desserialize só a chave pública como texto ou recrie o par com new RsaKeyPair(publicKeyPem, privateKeyPem) " +
            "a partir do cofre de segredos.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RsaKeyPair value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        const string name = nameof(RsaKeyPair.PublicKeyPem);
        writer.WriteStartObject();
        writer.WriteString(options.PropertyNamingPolicy?.ConvertName(name) ?? name, value.PublicKeyPem);
        writer.WriteEndObject();
    }
}
