using System.Diagnostics.CodeAnalysis;

namespace TEC.Core.Enums.Extensions;

/// <summary>
/// Extensões para valores de enum.
/// </summary>
public static class EnumExtensions
{
    /// <summary>Retorna a descrição (<c>[Description]</c> ou <c>[Display]</c>) ou o nome do membro.</summary>
    public static string GetDescription(this Enum value) => EnumHelper.GetDescription(value);

    /// <summary>Retorna o nome de exibição (<c>[Display(Name)]</c>) ou a descrição/nome do membro.</summary>
    public static string GetDisplayName(this Enum value) => EnumHelper.GetDisplayName(value);

    /// <summary>Retorna o valor numérico do enum.</summary>
    /// <remarks>Enums <c>ulong</c> acima de <see cref="long.MaxValue"/> retornam o mesmo padrão de bits (valor negativo).</remarks>
    public static long GetCode(this Enum value) => EnumHelper.GetCode(value);

    /// <summary>Converte um texto (nome, valor numérico ou descrição) para o enum.</summary>
    public static TEnum ToEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(this string value) where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(value);
        return EnumHelper.Parse<TEnum>(value);
    }

    /// <summary>Converte um texto para o enum, retornando o valor padrão se não for reconhecido.</summary>
    public static TEnum ToEnumOrDefault<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(this string? value, TEnum defaultValue = default) where TEnum : struct, Enum =>
        EnumHelper.TryParse<TEnum>(value, out var result) ? result : defaultValue;
}
