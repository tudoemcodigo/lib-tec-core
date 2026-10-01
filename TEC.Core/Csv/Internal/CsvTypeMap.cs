using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TEC.Core.Csv.Attributes;
using TEC.Core.Text.Extensions;

namespace TEC.Core.Csv.Internal;

/// <summary>
/// Mapeamento (em cache) entre as propriedades de um tipo e as colunas do CSV.
/// </summary>
/// <remarks>
/// O mapeamento é validado na primeira utilização: tipos sem colunas, nomes de coluna duplicados
/// e propriedades de tipos não suportados geram erro imediato, em vez de produzir um CSV incorreto.
/// Trimming/AOT: o tipo mapeado é anotado com <see cref="DynamicallyAccessedMemberTypes.PublicProperties"/>, então as
/// propriedades lidas aqui são preservadas a partir de <c>CsvReader.ReadAsync&lt;T&gt;</c>/<c>CsvWriter.WriteAsync&lt;T&gt;</c>.
/// </remarks>
internal sealed class CsvTypeMap
{
    private static readonly ConcurrentDictionary<Type, CsvTypeMap> Cache = new();

    private static readonly HashSet<Type> SupportedTypes =
    [
        typeof(string), typeof(char), typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
        typeof(DateTime), typeof(DateOnly), typeof(TimeOnly), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid)
    ];

    internal const DynamicallyAccessedMemberTypes MappedMembers = DynamicallyAccessedMemberTypes.PublicProperties;

    private CsvTypeMap([DynamicallyAccessedMembers(MappedMembers)] Type type)
    {
        var nullability = new NullabilityInfoContext();

        // Attribute.IsDefined/GetCustomAttribute percorrem a cadeia de herança de propriedades sobrescritas (override);
        // PropertyInfo.IsDefined ignora o parâmetro "inherit" e perderia o [CsvIgnore] declarado na classe base
        Columns = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !Attribute.IsDefined(p, typeof(CsvIgnoreAttribute), inherit: true))
            .Select(p => (Property: p, Attribute: (CsvColumnAttribute?)Attribute.GetCustomAttribute(p, typeof(CsvColumnAttribute), inherit: true)))
            .OrderBy(x => x.Attribute?.Order ?? int.MaxValue)
            .ThenBy(x => DeclarationOrder(x.Property))
            .Select(x => new CsvColumn(x.Property, x.Attribute?.Name ?? x.Property.Name, x.Attribute?.Format,
                IsNullableString(x.Property, nullability)))
            .ToList();

        Validate(type, Columns);
    }

    /// <summary>Colunas na ordem de escrita.</summary>
    public IReadOnlyList<CsvColumn> Columns { get; }

    public static CsvTypeMap For([DynamicallyAccessedMembers(MappedMembers)] Type type) =>
        Cache.TryGetValue(type, out var map) ? map : Cache.GetOrAdd(type, new CsvTypeMap(type));

    /// <summary>Normaliza o nome da coluna para comparação (sem acentos, maiúsculas e espaços nas pontas).</summary>
    public static string NormalizeName(string name) => name.Trim().RemoveAccents().ToUpperInvariant();

    /// <summary>
    /// Ordem de declaração: propriedades da classe base primeiro e, em cada classe, na ordem em que foram escritas (uma
    /// propriedade sobrescrita fica na posição da declaração original).
    /// </summary>
    /// <remarks>
    /// <c>PropertyInfo.MetadataToken</c> daria a mesma ordem, mas lança <see cref="InvalidOperationException"/> em Native AOT.
    /// </remarks>
    private static (int Depth, int Index) DeclarationOrder(PropertyInfo property)
    {
        var declaringType = property.GetMethod!.GetBaseDefinition().DeclaringType!;

        int depth = 0;
        for (var current = declaringType.BaseType; current is not null; current = current.BaseType)
            depth++;

        return (depth, Array.FindIndex(DeclaredProperties(declaringType), p => p.Name == property.Name));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Classe base do tipo mapeado: PublicProperties preserva também as propriedades públicas herdadas.")]
    private static PropertyInfo[] DeclaredProperties(Type declaringType) =>
        declaringType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static void Validate(Type type, IReadOnlyList<CsvColumn> columns)
    {
        if (columns.Count == 0)
            throw new InvalidOperationException($"O tipo {type.Name} não possui propriedades públicas mapeáveis para CSV.");

        var unsupported = columns.FirstOrDefault(c => !IsSupported(c.PropertyType));
        if (unsupported is not null)
            throw new NotSupportedException(
                $"A propriedade {type.Name}.{unsupported.Property.Name} ({unsupported.PropertyType.Name}) não é suportada em CSV. " +
                "Use [CsvIgnore] ou converta para um tipo simples.");

        var duplicate = columns.GroupBy(c => NormalizeName(c.Name)).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException(
                $"O tipo {type.Name} possui mais de uma propriedade mapeada para a coluna '{duplicate.First().Name}'.");
    }

    private static bool IsSupported(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsEnum || SupportedTypes.Contains(underlying))
            return true;

        // Tipos com TypeConverter próprio que converte de/para texto (ex.: Uri, Version)
        var converter = CsvValueConverter.GetTypeConverter(underlying);
        return converter.GetType() != typeof(TypeConverter)
            && converter.CanConvertFrom(typeof(string))
            && converter.CanConvertTo(typeof(string));
    }

    // "string?" (anulável): valor vazio no CSV é lido como null; "string": lido como string vazia
    private static bool IsNullableString(PropertyInfo property, NullabilityInfoContext context) =>
        property.PropertyType == typeof(string) && context.Create(property).WriteState == NullabilityState.Nullable;
}

/// <summary>
/// Coluna mapeada para uma propriedade.
/// </summary>
internal sealed record CsvColumn(PropertyInfo Property, string Name, string? Format, bool IsNullableString)
{
    public Type PropertyType => Property.PropertyType;

    public bool CanWrite => Property.SetMethod is { IsPublic: true };
}
