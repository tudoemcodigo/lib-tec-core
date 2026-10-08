using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using TEC.Core.Common.Globalization;
using TEC.Core.Enums;

namespace TEC.Core.Csv.Internal;

/// <summary>
/// Conversão de valores entre texto do CSV e tipos .NET.
/// </summary>
internal static class CsvValueConverter
{
    private static readonly HashSet<string> TrueValues = new(["true", "1", "sim", "s", "yes", "y", "verdadeiro", "v"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FalseValues = new(["false", "0", "não", "nao", "n", "no", "falso", "f"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Converte o texto para o tipo da propriedade.</summary>
    /// <param name="raw">Texto do campo.</param>
    /// <param name="targetType">Tipo da propriedade.</param>
    /// <param name="format">Formato da coluna (<c>[CsvColumn(Format)]</c>).</param>
    /// <param name="options">Opções de leitura.</param>
    /// <param name="emptyStringAsNull">Lê texto vazio como <c>null</c> (propriedade <c>string?</c>).</param>
    /// <param name="quoted">
    /// O campo estava entre aspas. Textos entre aspas nunca são aparados: o <see cref="CsvWriter"/> usa aspas justamente
    /// para preservar espaços nas pontas.
    /// </param>
    public static object? FromString(string raw, Type targetType, string? format, CsvOptions options, bool emptyStringAsNull = false, bool quoted = false)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        var type = underlying ?? targetType;

        if (type == typeof(string))
        {
            var value = options.TrimValues && !quoted ? raw.Trim() : raw;
            if (options.SanitizeFormulas)
                value = CsvFormulaGuard.Unsanitize(value);

            return emptyStringAsNull && value.Length == 0 ? null : value;
        }

        // Texto livre fora de string (char e tipos com TypeConverter) entre aspas também não é aparado: um char ' ' ou '\t'
        // voltaria vazio ou trocado. Números, datas, booleanos, enums e Guid são sempre aparados.
        var text = quoted && IsFreeTextType(type) ? raw : raw.Trim();
        if (text.Length == 0)
            return underlying is not null || !type.IsValueType ? null : DefaultValue(type);

        var culture = options.Culture;

        if (type.IsEnum)
        {
            return EnumHelper.TryParseRuntimeType(type, text, out var enumValue)
                ? enumValue
                : throw new FormatException($"Valor inválido para o enum {type.Name}.");
        }

        if (type == typeof(bool))
            return ParseBoolean(text, format);

        if (type == typeof(DateTime))
            return format is null ? DateTime.Parse(text, culture) : DateTime.ParseExact(text, format, culture);

        if (type == typeof(DateOnly))
            return format is null ? DateOnly.Parse(text, culture) : DateOnly.ParseExact(text, format, culture);

        if (type == typeof(TimeOnly))
            return format is null ? TimeOnly.Parse(text, culture) : TimeOnly.ParseExact(text, format, culture);

        if (type == typeof(DateTimeOffset))
            return format is null ? DateTimeOffset.Parse(text, culture) : DateTimeOffset.ParseExact(text, format, culture);

        if (type == typeof(TimeSpan))
            return format is null ? TimeSpan.Parse(text, culture) : TimeSpan.ParseExact(text, format, culture);

        if (type == typeof(Guid))
            return Guid.Parse(text);

        if (type == typeof(decimal))
            return decimal.Parse(ValidateGrouping(text, options), WithThousands(NumberStyles.Number | NumberStyles.AllowCurrencySymbol, options), culture);

        if (type == typeof(double))
            return Finite(double.Parse(ValidateGrouping(text, options), WithThousands(NumberStyles.Float, options), culture));

        if (type == typeof(float))
            return Finite(float.Parse(ValidateGrouping(text, options), WithThousands(NumberStyles.Float, options), culture));

        // Texto livre fora de string (char e tipos com TypeConverter): desfaz a neutralização de fórmulas da escrita
        if (type == typeof(char))
            return Convert.ChangeType(options.SanitizeFormulas ? CsvFormulaGuard.Unsanitize(text) : text, type, culture);

        if (type.IsPrimitive)
            return Convert.ChangeType(text, type, culture);

        var converter = GetTypeConverter(type);
        if (converter.CanConvertFrom(typeof(string)))
            return converter.ConvertFromString(null, culture, options.SanitizeFormulas ? CsvFormulaGuard.Unsanitize(text) : text);

        throw new NotSupportedException($"O tipo {type.Name} não é suportado na leitura de CSV.");
    }

    /// <summary>
    /// <c>true</c> para valores cujo texto é livre (string, char e tipos convertidos por <c>ToString()</c>/TypeConverter, como
    /// <see cref="Uri"/>) e pode começar com caractere de fórmula. Números, datas, <see cref="Guid"/>, booleanos e enums não entram.
    /// </summary>
    /// <remarks>Lista fechada dos tipos sem texto livre: <see cref="IFormattable"/> não serve de critério (<see cref="Uri"/> e char o implementam).</remarks>
    public static bool IsFreeText(object? value) => value is not (null or bool or Enum
        or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
        or DateTime or DateOnly or TimeOnly or DateTimeOffset or TimeSpan or Guid);

    // Mesmo critério de IsFreeText, aplicado ao tipo da propriedade (já sem Nullable)
    private static bool IsFreeTextType(Type type) =>
        type == typeof(char) || !(type.IsPrimitive || type.IsEnum || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateOnly) || type == typeof(TimeOnly)
            || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid));

    /// <summary>Converte o valor da propriedade para texto.</summary>
    public static string ToString(object? value, string? format, CsvOptions options)
    {
        switch (value)
        {
            case null:
                return string.Empty;

            case string text:
                return text;

            case Enum enumValue:
                return options.EnumFormat switch
                {
                    CsvEnumFormat.Code => Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType()), CultureInfo.InvariantCulture).ToString()!,
                    CsvEnumFormat.Description => EnumHelper.GetDescription(enumValue),
                    _ => enumValue.ToString()
                };

            case bool boolean:
                return FormatBoolean(boolean, format);

            case IFormattable formattable:
                return formattable.ToString(format, options.Culture);

            default:
                return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// <see cref="TypeConverter"/> de um tipo de propriedade fora da lista nativa (ex.: <see cref="Uri"/>, <see cref="Version"/>
    /// ou tipo com <c>[TypeConverter]</c> próprio).
    /// </summary>
    /// <remarks>
    /// Trimming/AOT: o tipo vem de <c>PropertyInfo.PropertyType</c> (não anotável). <c>TypeDescriptor</c> localiza o conversor
    /// pela tabela interna de conversores do runtime ou pelo <c>[TypeConverter(typeof(...))]</c> do tipo, cujo argumento é
    /// anotado para preservação; o aviso do runtime trata de conversores genéricos (<c>NullableConverter</c>), e
    /// <c>Nullable&lt;T&gt;</c> já chega aqui desembrulhado.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Tipo não genérico (Nullable desembrulhado); conversor localizado via tabela interna ou [TypeConverter] anotado.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Tipo não genérico (Nullable desembrulhado); conversor localizado via tabela interna ou [TypeConverter] anotado.")]
    internal static TypeConverter GetTypeConverter(Type type) => TypeDescriptor.GetConverter(type);

    // Valores padrão (em caixa) dos tipos-valor nativos: campo vazio em propriedade não anulável, sem reflexão
    private static readonly Dictionary<Type, object> BuiltInDefaults = new()
    {
        [typeof(bool)] = false, [typeof(char)] = default(char), [typeof(byte)] = default(byte), [typeof(sbyte)] = default(sbyte),
        [typeof(short)] = default(short), [typeof(ushort)] = default(ushort), [typeof(int)] = 0, [typeof(uint)] = 0u,
        [typeof(long)] = 0L, [typeof(ulong)] = 0UL, [typeof(float)] = 0f, [typeof(double)] = 0d, [typeof(decimal)] = 0m,
        [typeof(DateTime)] = default(DateTime), [typeof(DateOnly)] = default(DateOnly), [typeof(TimeOnly)] = default(TimeOnly),
        [typeof(DateTimeOffset)] = default(DateTimeOffset), [typeof(TimeSpan)] = default(TimeSpan), [typeof(Guid)] = Guid.Empty
    };

    // Valor padrão de um tipo-valor (equivalente a default(T)), sem Activator.CreateInstance
    private static object DefaultValue(Type valueType)
    {
        if (BuiltInDefaults.TryGetValue(valueType, out var value))
            return value;

        return valueType.IsEnum ? Enum.ToObject(valueType, 0) : CustomStructDefault(valueType);
    }

    // Struct com TypeConverter próprio (fora da lista nativa): o objeto não inicializado de um struct é exatamente default(T)
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Struct usado como tipo de propriedade do tipo mapeado; GetUninitializedObject não executa construtores.")]
    private static object CustomStructDefault(Type valueType) => RuntimeHelpers.GetUninitializedObject(valueType);

    // NaN e infinito (inclusive por estouro, como "1E999") vindos do arquivo envenenariam somas e médias adiante
    private static double Finite(double value) =>
        double.IsFinite(value) ? value : throw new FormatException("Valor numérico fora do intervalo (NaN ou infinito).");

    private static float Finite(float value) =>
        float.IsFinite(value) ? value : throw new FormatException("Valor numérico fora do intervalo (NaN ou infinito).");

    private static NumberStyles WithThousands(NumberStyles styles, CsvOptions options) =>
        options.AllowThousandsSeparator ? styles | NumberStyles.AllowThousands : styles & ~NumberStyles.AllowThousands;

    // Separador de milhar só entre grupos completos: em pt-BR, "1.5" e "1.2.3" são recusados (e não lidos como 15 e 123)
    private static string ValidateGrouping(string text, CsvOptions options) =>
        !options.AllowThousandsSeparator || NumberGrouping.HasValidGrouping(text, options.Culture.NumberFormat)
            ? text
            : throw new FormatException("Separador de milhar em posição inválida.");

    // Formato de booleano "Sim|Não": primeira parte = verdadeiro, segunda = falso
    private static bool ParseBoolean(string text, string? format)
    {
        if (TryGetBooleanTexts(format, out var trueText, out var falseText))
        {
            if (string.Equals(text, trueText, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(text, falseText, StringComparison.OrdinalIgnoreCase)) return false;
        }

        if (TrueValues.Contains(text)) return true;
        if (FalseValues.Contains(text)) return false;

        throw new FormatException("Valor inválido para booleano.");
    }

    private static string FormatBoolean(bool value, string? format) =>
        TryGetBooleanTexts(format, out var trueText, out var falseText)
            ? value ? trueText : falseText
            : value ? "true" : "false";

    private static bool TryGetBooleanTexts(string? format, out string trueText, out string falseText)
    {
        trueText = falseText = string.Empty;
        var parts = format?.Split('|');
        if (parts is not { Length: 2 })
            return false;

        (trueText, falseText) = (parts[0], parts[1]);
        return true;
    }
}
