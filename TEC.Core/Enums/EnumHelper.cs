using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using TEC.Core.Enums.Models;

namespace TEC.Core.Enums;

/// <summary>
/// Utilitários para enums: descrições, listagem e conversão a partir de nome, valor ou descrição.
/// Os metadados são lidos via reflexão uma única vez e mantidos em cache.
/// </summary>
/// <remarks>
/// Compatível com trimming e Native AOT: a reflexão lê apenas os campos públicos estáticos do enum, que o trimmer
/// sempre preserva (são necessários para <c>Enum.ToString</c>); os parâmetros genéricos são anotados com
/// <see cref="DynamicallyAccessedMembersAttribute"/>.
/// </remarks>
public static class EnumHelper
{
    private const DynamicallyAccessedMemberTypes EnumMembers = DynamicallyAccessedMemberTypes.PublicFields;

    // O trimmer (ILLink e Native AOT) preserva todos os campos de um enum mantido, pois Enum.ToString/GetNames dependem deles
    private const string EnumFieldsPreserved = "Os campos públicos de enums são sempre preservados pelo trimmer (necessários para Enum.ToString).";

    private static readonly ConcurrentDictionary<Type, EnumMetadata> Cache = new();

    /// <summary>
    /// Retorna a descrição do valor: <c>[Description]</c>, depois <c>[Display(Description)]</c>,
    /// depois <c>[Display(Name)]</c> e, por fim, o nome do membro.
    /// </summary>
    public static string GetDescription(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return GetMetadataOf(value).TryGetMember(value, out var member) ? member.Description : value.ToString();
    }

    /// <summary>
    /// Retorna o nome de exibição do valor: <c>[Display(Name)]</c>, depois <c>[Description]</c> e, por fim, o nome do membro.
    /// </summary>
    public static string GetDisplayName(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return GetMetadataOf(value).TryGetMember(value, out var member) ? member.DisplayName : value.ToString();
    }

    /// <summary>Retorna o valor numérico do enum, sem estouro para enums <c>ulong</c>.</summary>
    public static long GetCode(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ToInt64Bits(value);
    }

    // Converte qualquer tipo subjacente (inclusive ulong acima de long.MaxValue) preservando os bits
    private static long ToInt64Bits(object enumValue) =>
        Type.GetTypeCode(Enum.GetUnderlyingType(enumValue.GetType())) == TypeCode.UInt64
            ? unchecked((long)Convert.ToUInt64(enumValue, CultureInfo.InvariantCulture))
            : Convert.ToInt64(enumValue, CultureInfo.InvariantCulture);

    /// <summary>Lista todos os itens do enum com código, nome e descrição.</summary>
    public static IReadOnlyList<EnumItem<TEnum>> GetItems<[DynamicallyAccessedMembers(EnumMembers)] TEnum>() where TEnum : struct, Enum =>
        GetMetadata(typeof(TEnum)).Members
            .Select(m => new EnumItem<TEnum>((TEnum)m.Value, m.Code, m.Name, m.Description))
            .ToList();

    /// <summary>
    /// Converte um texto para o enum, aceitando (sem diferenciar maiúsculas/minúsculas):
    /// nome do membro, valor numérico, descrição ou nome de exibição.
    /// </summary>
    public static bool TryParse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        if (TryParse(typeof(TEnum), value, out var parsed))
        {
            result = (TEnum)parsed!;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Converte um texto para o enum. Lança <see cref="ArgumentException"/> se o valor não for reconhecido.</summary>
    public static TEnum Parse<[DynamicallyAccessedMembers(EnumMembers)] TEnum>(string value) where TEnum : struct, Enum =>
        TryParse<TEnum>(value, out var result)
            ? result
            : throw new ArgumentException($"O valor informado não corresponde a nenhum membro de {typeof(TEnum).Name}.", nameof(value));

    /// <summary>Versão não genérica de <see cref="TryParse{TEnum}(string?, out TEnum)"/> (usada quando o tipo só é conhecido em tempo de execução).</summary>
    public static bool TryParse([DynamicallyAccessedMembers(EnumMembers)] Type enumType, string? value, out object? result)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        result = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        var metadata = GetMetadata(enumType);

        if (metadata.ByText.TryGetValue(text, out var member))
        {
            result = member.Value;
            return true;
        }

        // Valor numérico: aceito apenas se corresponder a um membro definido
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long code))
        {
            member = metadata.Members.FirstOrDefault(m => m.Code == code);
            if (member is not null)
            {
                result = member.Value;
                return true;
            }
        }

        // Combinação de flags ("Leitura, Escrita"): aceita apenas bits definidos no enum
        if (metadata.IsFlags && Enum.TryParse(enumType, text, ignoreCase: true, out var flags)
            && (ToInt64Bits(flags!) & ~metadata.AllFlagsMask) == 0)
        {
            result = flags;
            return true;
        }

        return false;
    }

    // Tipo obtido em tempo de execução (GetType()/PropertyType): o analisador não enxerga a anotação, mas os campos
    // de enums são sempre preservados (ver EnumFieldsPreserved)
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = EnumFieldsPreserved)]
    private static EnumMetadata GetMetadataOf(Enum value) => GetMetadata(value.GetType());

    /// <summary>Versão de <see cref="TryParse(Type, string?, out object?)"/> para um tipo de enum obtido em tempo de execução (ex.: tipo de propriedade).</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = EnumFieldsPreserved)]
    internal static bool TryParseRuntimeType(Type enumType, string? value, out object? result) => TryParse(enumType, value, out result);

    private static EnumMetadata GetMetadata([DynamicallyAccessedMembers(EnumMembers)] Type enumType)
    {
        if (!enumType.IsEnum)
            throw new ArgumentException($"O tipo {enumType.Name} não é um enum.", nameof(enumType));

        return Cache.TryGetValue(enumType, out var metadata) ? metadata : Cache.GetOrAdd(enumType, new EnumMetadata(enumType));
    }

    private sealed record EnumMember(object Value, long Code, string Name, string Description, string DisplayName);

    private sealed class EnumMetadata
    {
        public EnumMetadata([DynamicallyAccessedMembers(EnumMembers)] Type type)
        {
            IsFlags = type.IsDefined(typeof(FlagsAttribute), inherit: false);
            Members = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field =>
                {
                    var value = field.GetValue(null)!;
                    var description = field.GetCustomAttribute<DescriptionAttribute>()?.Description;
                    var display = field.GetCustomAttribute<DisplayAttribute>();
                    return new EnumMember(
                        value,
                        ToInt64Bits(value),
                        field.Name,
                        description ?? display?.GetDescription() ?? display?.GetName() ?? field.Name,
                        display?.GetName() ?? description ?? field.Name);
                })
                .ToList();

            ByValue = Members.GroupBy(m => m.Value).ToDictionary(g => g.Key, g => g.First());
            AllFlagsMask = Members.Aggregate(0L, (mask, m) => mask | m.Code);

            // Índice por nome, descrição e nome de exibição (o nome do membro tem prioridade em caso de conflito)
            ByText = new Dictionary<string, EnumMember>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in Members)
                ByText.TryAdd(member.Name, member);
            foreach (var member in Members)
            {
                ByText.TryAdd(member.Description, member);
                ByText.TryAdd(member.DisplayName, member);
            }
        }

        public bool IsFlags { get; }

        public long AllFlagsMask { get; }

        public IReadOnlyList<EnumMember> Members { get; }

        public Dictionary<object, EnumMember> ByValue { get; }

        public Dictionary<string, EnumMember> ByText { get; }

        public bool TryGetMember(object value, out EnumMember member) => ByValue.TryGetValue(value, out member!);
    }
}
