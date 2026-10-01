namespace TEC.Core.Enums.Models;

/// <summary>
/// Representação de um item de enum, útil para popular combos/selects e expor listas em APIs.
/// </summary>
/// <typeparam name="TEnum">Tipo do enum.</typeparam>
/// <param name="Value">Valor do enum.</param>
/// <param name="Code">Valor numérico.</param>
/// <param name="Name">Nome do membro.</param>
/// <param name="Description">Descrição (<c>[Description]</c> / <c>[Display]</c>) ou o próprio nome.</param>
public sealed record EnumItem<TEnum>(TEnum Value, long Code, string Name, string Description)
    where TEnum : struct, Enum;
