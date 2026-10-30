namespace TEC.Core.Domain;

/// <summary>
/// Entidade de domínio com identidade própria. Duas entidades são iguais quando têm o mesmo tipo concreto e o mesmo
/// <see cref="Id"/>; entidades ainda sem identificador (valor padrão) só são iguais a si mesmas.
/// </summary>
/// <typeparam name="TId">Tipo do identificador.</typeparam>
/// <remarks>
/// Sem dependência de persistência. Para exclusão lógica e autoria preenchidas pelo TEC.ORM sem referenciá-lo no domínio, use
/// <see cref="AuditableDomainEntity{TId}"/> ou <see cref="AuditableAggregateRoot{TId}"/>.
/// </remarks>
public abstract class DomainEntity<TId> : IEquatable<DomainEntity<TId>>, IEntity<TId>
    where TId : notnull
{
    /// <summary>Identificador da entidade.</summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>Retorna o tipo real da entidade, desconsiderando proxies dinâmicos de ORM.</summary>
    protected virtual Type GetUnproxiedType() => GetType();

    /// <inheritdoc />
    public bool Equals(DomainEntity<TId>? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (other.GetUnproxiedType() != GetUnproxiedType() || IsTransient() || other.IsTransient())
            return false;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DomainEntity<TId> other && Equals(other);

    /// <summary>Retorna um hash estável durante o ciclo de vida da entidade, inclusive antes da atribuição de <see cref="Id"/>.</summary>
    public override int GetHashCode() => HashCode.Combine(GetUnproxiedType());

    /// <summary>Compara duas entidades pela identidade.</summary>
    /// <param name="left">Primeira entidade.</param>
    /// <param name="right">Segunda entidade.</param>
    /// <returns><c>true</c> quando representam a mesma entidade.</returns>
    public static bool operator ==(DomainEntity<TId>? left, DomainEntity<TId>? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Compara duas entidades pela identidade.</summary>
    /// <param name="left">Primeira entidade.</param>
    /// <param name="right">Segunda entidade.</param>
    /// <returns><c>true</c> quando representam entidades diferentes.</returns>
    public static bool operator !=(DomainEntity<TId>? left, DomainEntity<TId>? right) => !(left == right);

    private bool IsTransient() => EqualityComparer<TId>.Default.Equals(Id, default!);
}

/// <summary>
/// Raiz de agregado: única porta de entrada para alterar o agregado e fonte dos seus eventos de domínio.
/// </summary>
/// <typeparam name="TId">Tipo do identificador.</typeparam>
/// <example>
/// <code>
/// public sealed class Pedido : AggregateRoot&lt;Guid&gt;
/// {
///     public Result Aprovar(DateTimeOffset agora)
///     {
///         Status = StatusPedido.Aprovado;
///         RaiseDomainEvent(new PedidoAprovado(Id, agora));
///         return Result.Success();
///     }
/// }
/// </code>
/// </example>
public abstract class AggregateRoot<TId> : DomainEntity<TId>, IHasDomainEvents
    where TId : notnull
{
    /// <summary>Limite de eventos pendentes por agregado: protege contra laços que registram eventos sem fim.</summary>
    public const int MaxPendingEvents = 1000;

    private readonly List<IDomainEvent> _domainEvents = [];

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Registra um evento de domínio para ser coletado no próximo commit.</summary>
    /// <param name="domainEvent">Evento ocorrido.</param>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> é nulo.</exception>
    /// <exception cref="InvalidOperationException">O agregado já tem <see cref="MaxPendingEvents"/> eventos pendentes.</exception>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (_domainEvents.Count >= MaxPendingEvents)
            throw new InvalidOperationException($"O agregado já tem {MaxPendingEvents} eventos pendentes; colete-os antes de registrar outros.");
        _domainEvents.Add(domainEvent);
    }
}
