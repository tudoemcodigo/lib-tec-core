namespace TEC.Core.Security;

/// <summary>Tipo de identidade que executa a operação.</summary>
public enum PrincipalKind
{
    /// <summary>Sem identidade autenticada.</summary>
    Anonymous = 0,

    /// <summary>Pessoa autenticada (login interativo ou token delegado de usuário).</summary>
    User = 1,

    /// <summary>Aplicação autenticada em nome próprio (client credentials, API key), sem usuário.</summary>
    Application = 2,

    /// <summary>Processo interno da própria aplicação (job, worker, mensageria), sem token externo.</summary>
    System = 3
}

/// <summary>
/// Quem executa a operação atual: contrato mínimo para auditoria e isolamento por tenant, sem dependência de ASP.NET Core
/// nem de provedor de identidade.
/// </summary>
/// <remarks>
/// <para>Implementado pelo <c>TEC.Security</c> (a partir do token validado, de uma API key ou da identidade de sistema de um
/// worker) e consumido por componentes como o <c>TEC.ORM</c> (campos de auditoria e filtro por tenant), que assim não
/// dependem do componente de segurança.</para>
/// <para><see cref="Id"/> é um identificador <b>estável e não reutilizável</b> (ex.: object id do Entra ID), nunca e-mail ou
/// login, que podem mudar ou ser reaproveitados por outra pessoa. Não é dado pessoal legível: use-o em colunas de auditoria
/// no lugar do nome.</para>
/// <para>Implementações devem manter o estado coerente: <see cref="PrincipalKind.Anonymous"/> ⇔ <see cref="Id"/> nulo, e
/// anônimo nunca tem <see cref="TenantId"/>. <see cref="IsAuthenticated"/> é calculado a partir de <see cref="Kind"/>
/// e não pode ser implementado de outra forma.</para>
/// </remarks>
/// <example>
/// <code>
/// internal sealed class AuditInterceptor(ICurrentUser currentUser)
/// {
///     public void StampCreation(IAuditable entity, DateTimeOffset now)
///     {
///         entity.CreatedBy = currentUser.Id ?? "anonimo";
///         entity.CreatedAt = now;
///     }
/// }
/// </code>
/// </example>
public interface ICurrentUser
{
    /// <summary>
    /// <c>true</c> quando há identidade autenticada (usuário, aplicação ou sistema). Derivado de <see cref="Kind"/> e não
    /// sobrescrevível: os dois nunca divergem.
    /// </summary>
    /// <remarks>Membro padrão da interface: acesse-o por uma referência <see cref="ICurrentUser"/>.</remarks>
    sealed bool IsAuthenticated => Kind != PrincipalKind.Anonymous;

    /// <summary>Tipo da identidade. <see cref="PrincipalKind.Anonymous"/> quando não autenticada.</summary>
    /// <remarks>Fonte única do estado de autenticação; <see cref="IsAuthenticated"/> é derivado dele.</remarks>
    PrincipalKind Kind { get; }

    /// <summary>
    /// Identificador estável da identidade. Obrigatoriamente <c>null</c> quando <see cref="Kind"/> é
    /// <see cref="PrincipalKind.Anonymous"/>, e não nulo nos demais tipos.
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// Tenant da aplicação ao qual a identidade pertence, ou <c>null</c> quando a aplicação não usa tenants.
    /// Sempre <c>null</c> para <see cref="PrincipalKind.Anonymous"/>.
    /// </summary>
    string? TenantId { get; }
}
