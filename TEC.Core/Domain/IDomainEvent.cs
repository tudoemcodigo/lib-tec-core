namespace TEC.Core.Domain;

/// <summary>
/// Fato de negócio ocorrido em um agregado (ex.: <c>PedidoAprovado</c>). Registrado pela raiz do agregado e coletado pela
/// persistência no commit (por exemplo, para o Outbox do TEC.Messaging).
/// </summary>
/// <remarks>
/// Eventos de domínio são internos ao serviço. Para comunicar outros serviços, converta-os em eventos de integração
/// com contrato próprio e versionado: assim o modelo de domínio pode mudar sem quebrar os consumidores.
/// </remarks>
public interface IDomainEvent
{
    /// <summary>Instante em que o fato ocorreu (UTC, vindo do <see cref="TimeProvider"/> do chamador).</summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Objeto que acumula eventos de domínio até a persistência coletá-los.</summary>
public interface IHasDomainEvents
{
    /// <summary>Eventos registrados desde a última coleta, na ordem em que ocorreram.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Descarta os eventos já coletados (chamado pela persistência depois de gravá-los).</summary>
    void ClearDomainEvents();
}
