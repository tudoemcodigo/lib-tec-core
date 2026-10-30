namespace TEC.Core.Domain;

/// <summary>
/// Exclusão lógica: excluir marca <see cref="IsDeleted"/> e <see cref="DeletedAt"/> em vez de remover a linha, e as consultas
/// do TEC.ORM ignoram as linhas excluídas automaticamente.
/// </summary>
/// <remarks>
/// Os dois campos são preenchidos pela persistência (no TEC.ORM com EF Core, por um interceptor ao salvar); a aplicação não os
/// altera. Por isso só há <c>get</c> aqui: a implementação grava pelos metadados do ORM (setter privado ou campo).
/// </remarks>
public interface ISoftDelete
{
    /// <summary>Indica se a entidade foi excluída logicamente.</summary>
    bool IsDeleted { get; }

    /// <summary>Quando a entidade foi excluída (UTC); <c>null</c> se não foi.</summary>
    DateTimeOffset? DeletedAt { get; }
}
