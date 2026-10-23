using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TEC.Core.Common.Serialization;

/// <summary>
/// Extensões para serialização e desserialização JSON usando <see cref="JsonDefaults"/>.
/// </summary>
/// <remarks>
/// As sobrecargas sem metadados usam reflexão (incompatíveis com trimming/Native AOT). Em apps AOT, use as sobrecargas
/// que recebem <see cref="JsonTypeInfo{T}"/> ou <see cref="JsonSerializerContext"/> gerados pelo source generator,
/// de preferência com um contexto criado sobre <see cref="JsonDefaults.CreateOptions(IJsonTypeInfoResolver?, bool)"/>.
/// </remarks>
public static class JsonExtensions
{
    /// <summary>Serializa o objeto para JSON.</summary>
    [RequiresUnreferencedCode(JsonDefaults.ReflectionMessage)]
    [RequiresDynamicCode(JsonDefaults.ReflectionMessage)]
    public static string ToJson<T>(this T value, bool indented = false) =>
        JsonSerializer.Serialize(value, indented ? JsonDefaults.IndentedOptions : JsonDefaults.Options);

    /// <summary>Serializa o objeto para JSON com metadados gerados em tempo de compilação (compatível com Native AOT).</summary>
    /// <param name="value">Valor a serializar.</param>
    /// <param name="typeInfo">Metadados do tipo (ex.: <c>AppJsonContext.Default.Pedido</c>); as opções são as do contexto.</param>
    public static string ToJson<T>(this T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return JsonSerializer.Serialize(value, typeInfo);
    }

    /// <summary>Serializa o objeto para JSON usando os metadados do contexto gerado (compatível com Native AOT).</summary>
    /// <exception cref="InvalidOperationException">O contexto não possui metadados para <typeparamref name="T"/>.</exception>
    public static string ToJson<T>(this T value, JsonSerializerContext context) =>
        JsonSerializer.Serialize(value, GetTypeInfo<T>(context));

    /// <summary>Desserializa o JSON para o tipo informado.</summary>
    /// <exception cref="ArgumentException">JSON nulo, vazio ou com texto UTF-16 inválido (surrogate isolado).</exception>
    /// <exception cref="JsonException">JSON inválido ou incompatível com o tipo.</exception>
    [RequiresUnreferencedCode(JsonDefaults.ReflectionMessage)]
    [RequiresDynamicCode(JsonDefaults.ReflectionMessage)]
    public static T? FromJson<T>(this string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
    }

    /// <summary>Desserializa o JSON com metadados gerados em tempo de compilação (compatível com Native AOT).</summary>
    /// <exception cref="ArgumentException">JSON nulo, vazio ou com texto UTF-16 inválido (surrogate isolado).</exception>
    /// <exception cref="JsonException">JSON inválido ou incompatível com o tipo.</exception>
    public static T? FromJson<T>(this string json, JsonTypeInfo<T> typeInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return JsonSerializer.Deserialize(json, typeInfo);
    }

    /// <summary>Desserializa o JSON usando os metadados do contexto gerado (compatível com Native AOT).</summary>
    /// <exception cref="ArgumentException">JSON nulo, vazio ou com texto UTF-16 inválido (surrogate isolado).</exception>
    /// <exception cref="JsonException">JSON inválido ou incompatível com o tipo.</exception>
    /// <exception cref="InvalidOperationException">O contexto não possui metadados para <typeparamref name="T"/>.</exception>
    public static T? FromJson<T>(this string json, JsonSerializerContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize(json, GetTypeInfo<T>(context));
    }

    /// <summary>
    /// Tenta desserializar o JSON. Retorna <c>false</c> se o conteúdo for inválido ou se o tipo não for suportado
    /// pelo serializador (ex.: tipo sem construtor utilizável ou propriedade de tipo não suportado).
    /// </summary>
    [RequiresUnreferencedCode(JsonDefaults.ReflectionMessage)]
    [RequiresDynamicCode(JsonDefaults.ReflectionMessage)]
    public static bool TryFromJson<T>(this string? json, out T? value) =>
        TryDeserialize(json, static text => JsonSerializer.Deserialize<T>(text, JsonDefaults.Options), out value);

    /// <summary>
    /// Tenta desserializar o JSON com metadados gerados em tempo de compilação (compatível com Native AOT).
    /// Retorna <c>false</c> se o conteúdo for inválido ou incompatível com o tipo.
    /// </summary>
    public static bool TryFromJson<T>(this string? json, JsonTypeInfo<T> typeInfo, out T? value)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return TryDeserialize(json, text => JsonSerializer.Deserialize(text, typeInfo), out value);
    }

    /// <summary>
    /// Tenta desserializar o JSON usando os metadados do contexto gerado (compatível com Native AOT).
    /// Retorna <c>false</c> se o conteúdo for inválido ou incompatível com o tipo.
    /// </summary>
    /// <exception cref="InvalidOperationException">O contexto não possui metadados para <typeparamref name="T"/>.</exception>
    public static bool TryFromJson<T>(this string? json, JsonSerializerContext context, out T? value) =>
        TryFromJson(json, GetTypeInfo<T>(context), out value);

    private static bool TryDeserialize<T>(string? json, Func<string, T?> deserialize, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            value = deserialize(json);
            return true;
        }
        // ArgumentException: texto UTF-16 inválido (surrogate isolado), que o serializador não consegue transcodificar
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            value = default;
            return false;
        }
    }

    private static JsonTypeInfo<T> GetTypeInfo<T>(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException(
                $"O contexto {context.GetType().Name} não possui metadados para o tipo {typeof(T).Name}. " +
                $"Adicione [JsonSerializable(typeof({typeof(T).Name}))] ao contexto.");
    }
}
