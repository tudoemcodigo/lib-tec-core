using System.Runtime.CompilerServices;
using TEC.Core.Common.Guards;

namespace TEC.Core.Dates.Holidays.Sources;

/// <summary>
/// Feriados obtidos por uma função do chamador: use para EF Core, Dapper, um repositório ou qualquer outra origem.
/// </summary>
/// <example>
/// <code>
/// var source = new DelegateHolidaySource(async ct =>
///     (await db.Holidays.AsNoTracking().ToListAsync(ct))
///         .Select(h => new Holiday(h.Date, h.Description) { Uf = h.Uf, IbgeCode = h.IbgeCode }),
///     name: "EF Feriados");
/// </code>
/// </example>
public sealed class DelegateHolidaySource : IHolidaySource
{
    private readonly Func<CancellationToken, Task<IEnumerable<Holiday>>> _load;

    /// <summary>Cria a fonte.</summary>
    /// <param name="load">Função que carrega os feriados.</param>
    /// <param name="name">Nome da fonte, usado em mensagens de erro.</param>
    public DelegateHolidaySource(Func<CancellationToken, Task<IEnumerable<Holiday>>> load, string name)
    {
        _load = Guard.NotNull(load);
        Name = Guard.NotNullOrWhiteSpace(name);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public async IAsyncEnumerable<Holiday> LoadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var task = _load(cancellationToken) ?? throw new InvalidOperationException("A função de carga de feriados retornou uma Task nula.");
        var holidays = await task.ConfigureAwait(false)
            ?? throw new InvalidOperationException("A função de carga de feriados retornou null.");

        foreach (var holiday in holidays)
            yield return holiday;
    }
}
