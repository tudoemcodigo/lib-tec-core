namespace TEC.Core.Domain;

/// <summary>
/// Raiz de agregado (<see cref="AggregateRoot{TId}"/>) com exclusão lógica (<see cref="ISoftDelete"/>) e autoria
/// (<see cref="IAuditable"/>) preenchidas pela persistência.
/// </summary>
/// <typeparam name="TId">Tipo do identificador.</typeparam>
/// <remarks>
/// Os campos têm setter privado: só a persistência (ex.: interceptores do TEC.ORM) os grava, pelos metadados do ORM. O projeto de
/// domínio continua sem referência a pacotes de persistência.
/// </remarks>
public abstract class AuditableAggregateRoot<TId> : AggregateRoot<TId>, ISoftDelete, IAuditable
    where TId : notnull
{
    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; private set; }

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? CreatedByTenant { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <inheritdoc />
    public string? UpdatedBy { get; private set; }

    /// <inheritdoc />
    public string? UpdatedByTenant { get; private set; }

    /// <inheritdoc />
    public string? DeletedBy { get; private set; }

    /// <inheritdoc />
    public string? DeletedByTenant { get; private set; }
}
