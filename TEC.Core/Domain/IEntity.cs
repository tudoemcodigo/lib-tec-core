namespace TEC.Core.Domain;

/// <summary>Entidade com identificador do tipo <typeparamref name="TKey"/> (<c>int</c>, <c>long</c>, <c>Guid</c>, <c>string</c>...).</summary>
/// <typeparam name="TKey">Tipo do identificador (chave primária, quando persistida).</typeparam>
/// <remarks>
/// Contrato sem dependência de persistência: o TEC.ORM aceita qualquer entidade que o implemente junto com
/// <see cref="ISoftDelete"/>, então o projeto de domínio só precisa referenciar o TEC.Core.
/// </remarks>
public interface IEntity<TKey> where TKey : notnull
{
    /// <summary>Identificador.</summary>
    TKey Id { get; }
}
