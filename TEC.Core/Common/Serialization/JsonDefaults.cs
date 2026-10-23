using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

namespace TEC.Core.Common.Serialization;

/// <summary>
/// Configurações padrão de serialização JSON (System.Text.Json) utilizadas em todo o componente.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Options"/>, <see cref="IndentedOptions"/> e <see cref="CreateOptions(bool)"/> usam o resolvedor por reflexão
/// e um conversor de enums criado em tempo de execução: não são compatíveis com trimming/Native AOT.
/// </para>
/// <para>
/// Em apps com trimming ou Native AOT, use <see cref="CreateOptions(IJsonTypeInfoResolver?, bool)"/> com um
/// <see cref="JsonSerializerContext"/> gerado (source generator) e registre os enums com
/// <see cref="CreateEnumConverter{TEnum}"/> (ou <c>[JsonConverter(typeof(JsonStringEnumConverter&lt;TEnum&gt;))]</c>).
/// </para>
/// </remarks>
public static class JsonDefaults
{
    internal const string ReflectionMessage =
        "Usa o System.Text.Json baseado em reflexão, incompatível com trimming/Native AOT. " +
        "Use JsonDefaults.CreateOptions(IJsonTypeInfoResolver) com um JsonSerializerContext gerado " +
        "ou as sobrecargas de JsonExtensions que recebem JsonTypeInfo<T>/JsonSerializerContext.";

    internal const string EnumConverterMessage =
        "Usa um conversor de enums não genérico, que gera código em tempo de execução (incompatível com Native AOT). " +
        "Use JsonDefaults.CreateOptions(IJsonTypeInfoResolver) e JsonDefaults.CreateEnumConverter<TEnum>().";

    // Mantém acentos legíveis (Latin-1/Latin Extended-A), mas continua escapando caracteres
    // sensíveis a HTML (< > & ' +), evitando XSS caso o JSON seja embutido em uma página.
    private static readonly JavaScriptEncoder SafeEncoder =
        JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA);

    private static JsonSerializerOptions? _options;
    private static JsonSerializerOptions? _indentedOptions;

    /// <summary>
    /// Opções padrão (somente leitura): camelCase, enums como string, ignora nulos, acentos sem escape.
    /// </summary>
    /// <remarks>Criadas na primeira utilização; a mesma instância é devolvida sempre.</remarks>
    public static JsonSerializerOptions Options
    {
        [RequiresUnreferencedCode(ReflectionMessage)]
        [RequiresDynamicCode(ReflectionMessage)]
        get => _options ?? Publish(ref _options, Freeze(CreateOptions()));
    }

    /// <summary>Opções padrão com indentação (somente leitura; útil para logs e depuração).</summary>
    public static JsonSerializerOptions IndentedOptions
    {
        [RequiresUnreferencedCode(ReflectionMessage)]
        [RequiresDynamicCode(ReflectionMessage)]
        get => _indentedOptions ?? Publish(ref _indentedOptions, Freeze(CreateOptions(writeIndented: true)));
    }

    /// <summary>
    /// Cria uma nova instância (editável) das opções padrão, para customizações.
    /// As instâncias estáticas são imutáveis para que nenhum código altere o comportamento global.
    /// </summary>
    /// <remarks>
    /// Enums são serializados como texto em camelCase. Na leitura só são aceitos nomes de membros definidos: números
    /// (inclusive entre aspas) e, fora de enums <c>[Flags]</c>, combinações por vírgula são recusados.
    /// </remarks>
    [RequiresDynamicCode(EnumConverterMessage)]
    public static JsonSerializerOptions CreateOptions(bool writeIndented = false)
    {
        var options = CreateBaseOptions(writeIndented);
        options.Converters.Add(new StrictEnumConverterFactory());
        return options;
    }

    /// <summary>
    /// Cria uma nova instância (editável) das opções padrão para uso com metadados gerados em tempo de compilação
    /// (compatível com trimming e Native AOT).
    /// </summary>
    /// <param name="typeInfoResolver">
    /// Resolvedor dos metadados, normalmente o <c>Default</c> de um <see cref="JsonSerializerContext"/> gerado
    /// (ex.: <c>AppJsonContext.Default</c>). Pode ser <c>null</c> quando as opções forem passadas ao construtor do
    /// contexto (<c>new AppJsonContext(JsonDefaults.CreateOptions(null))</c>), que se registra como resolvedor.
    /// </param>
    /// <param name="writeIndented">Indenta o JSON.</param>
    /// <remarks>
    /// Mesmas convenções de <see cref="CreateOptions(bool)"/> (camelCase, ignora nulos, acentos sem escape, profundidade 64),
    /// exceto o conversor de enums: registre cada enum com <see cref="CreateEnumConverter{TEnum}"/> para obter texto em camelCase.
    /// </remarks>
    public static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver? typeInfoResolver, bool writeIndented = false)
    {
        var options = CreateBaseOptions(writeIndented);
        if (typeInfoResolver is not null)
            options.TypeInfoResolver = typeInfoResolver;
        return options;
    }

    /// <summary>
    /// Conversor de enum como texto em camelCase, que só aceita nomes de membros definidos (mesma regra de <see cref="Options"/>),
    /// compatível com Native AOT: <c>options.Converters.Add(JsonDefaults.CreateEnumConverter&lt;MeuEnum&gt;())</c>.
    /// </summary>
    public static JsonConverter<TEnum> CreateEnumConverter<TEnum>() where TEnum : struct, Enum =>
        new StrictEnumConverter<TEnum>();

    private static JsonSerializerOptions CreateBaseOptions(bool writeIndented) =>
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = SafeEncoder,
            WriteIndented = writeIndented,
            MaxDepth = 64
        };

    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    private static JsonSerializerOptions Freeze(JsonSerializerOptions options)
    {
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    // Publica a instância uma única vez (threads concorrentes recebem a mesma)
    private static JsonSerializerOptions Publish(ref JsonSerializerOptions? target, JsonSerializerOptions created) =>
        Interlocked.CompareExchange(ref target, created, null) ?? created;
}
