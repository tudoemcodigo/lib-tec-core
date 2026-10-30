[🏠 TEC.Core](../README.md) › [📚 Documentação](README.md) › Domínio

# 🏛️ Domínio

> Primitivas mínimas de DDD, sem dependência de persistência: entidade com identidade, raiz de agregado e eventos de domínio.

## 📑 Sumário

- [🎯 Visão geral](#-visão-geral)
- [🚀 Uso](#-uso)
  - [DomainEntity&lt;TId&gt;](#domainentitytid)
  - [AggregateRoot&lt;TId&gt;](#aggregateroottid)
  - [IDomainEvent e IHasDomainEvents](#idomainevent-e-ihasdomainevents)
  - [Entidades persistidas sem depender do ORM](#entidades-persistidas-sem-depender-do-orm)
- [⚙️ Opções](#️-opções)
- [❌ Erros](#-erros)
- [🛡️ Segurança](#️-segurança)
- [❓ Perguntas frequentes](#-perguntas-frequentes)

---

## 🎯 Visão geral

| Namespace | Tipos públicos | Para que serve |
|---|---|---|
| `TEC.Core.Domain` | `DomainEntity<TId>`, `AggregateRoot<TId>`, `IDomainEvent`, `IHasDomainEvents` | Modelar agregados que registram fatos de negócio, coletados pela persistência no commit |
| `TEC.Core.Domain` | `IEntity<TKey>`, `ISoftDelete`, `IAuditable`, `AuditableDomainEntity<TId>`, `AuditableAggregateRoot<TId>` | Entidades com exclusão lógica e autoria preenchidas pelo TEC.ORM, sem o projeto de domínio referenciar o ORM |

```mermaid
flowchart LR
    H(["Handler do command"]) -->|"pedido.Aprovar(agora)"| A["🏛️ AggregateRoot<br/>RaiseDomainEvent(...)"]
    A -->|"DomainEvents"| P["💾 Persistência<br/>(SaveChanges)"]
    P -->|"mesma transação"| O[("📮 Outbox<br/>TEC.Messaging")]
    P -->|"ClearDomainEvents()"| A
```

O TEC.Core só define os contratos. Quem coleta os eventos é a persistência: o interceptor do `TEC.Messaging.SqlServer` (ou do `TEC.Messaging.PostgreSql`) grava cada evento no Outbox, na mesma transação do agregado.

---

## 🚀 Uso

### DomainEntity&lt;TId&gt;

> `TEC.Core.Domain` · `abstract class`

| Membro | Descrição |
|---|---|
| `Id` | Identificador (`protected set`) |
| `Equals` / `==` / `!=` / `GetHashCode` | Iguais quando têm o **mesmo tipo concreto** e o mesmo `Id`. Entidade sem `Id` (valor padrão, "transiente") só é igual a si mesma. O hash depende do tipo real da entidade e permanece estável quando o ORM atribui o `Id` |

> [!WARNING]
> **Coleções com hash ficam O(N²).** `GetHashCode` devolve o mesmo valor para todas as entidades de um tipo (o `Id`
> não entra no cálculo). É de propósito: se o hash usasse o `Id`, uma entidade transiente guardada num `HashSet` ou
> como chave de `Dictionary` mudaria de hash quando o ORM atribuísse o `Id` e ficaria inalcançável na coleção. O custo
> é que todas as entidades do mesmo tipo caem no mesmo balde: cada inserção ou busca compara com as anteriores, e
> montar um `HashSet<Pedido>` com N pedidos custa O(N²) comparações. Até algumas centenas de itens isso passa
> despercebido; com milhares, indexe pelo `Id` (`Dictionary<Guid, Pedido>`, `ToDictionary(p => p.Id)`) ou use um
> `IEqualityComparer` próprio que considere o `Id` quando todas as entidades já estiverem persistidas.

> [!NOTE]
> O nome não é `Entity` para não colidir com `TEC.ORM.Entities.Entity<TKey>`, que traz soft delete e auditoria. Use o do ORM quando quiser esses recursos; use o daqui quando o domínio não deve conhecer a persistência.

### AggregateRoot&lt;TId&gt;

> `TEC.Core.Domain` · `abstract class : DomainEntity<TId>, IHasDomainEvents`

| Membro | Descrição |
|---|---|
| `RaiseDomainEvent(IDomainEvent)` | (`protected`) Registra um evento para o próximo commit |
| `DomainEvents` | Eventos pendentes, na ordem em que ocorreram |
| `ClearDomainEvents()` | Descarta os pendentes (chamado pela persistência) |
| `MaxPendingEvents` | Limite de pendentes por agregado (1000) |

```csharp
using TEC.Core.Common.Results;
using TEC.Core.Domain;

public sealed record PedidoAprovado(Guid PedidoId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed class Pedido : AggregateRoot<Guid>
{
    public StatusPedido Status { get; private set; }

    public Result Aprovar(DateTimeOffset agora)
    {
        if (Status != StatusPedido.Rascunho)
            return Error.Conflict("TRANSICAO_INVALIDA", "Só pedidos em rascunho podem ser aprovados.");

        Status = StatusPedido.Aprovado;
        RaiseDomainEvent(new PedidoAprovado(Id, agora));
        return Result.Success();
    }
}
```

### IDomainEvent e IHasDomainEvents

> `TEC.Core.Domain` · `interface`

| Contrato | Membros | Quem implementa |
|---|---|---|
| `IDomainEvent` | `OccurredAt` | Os eventos (de preferência `sealed record` imutáveis, com ids e metadados, sem dados pessoais) |
| `IHasDomainEvents` | `DomainEvents`, `ClearDomainEvents()` | `AggregateRoot<TId>` ou uma raiz própria |

Use `OccurredAt` vindo do `TimeProvider` do caso de uso (nunca `DateTime.Now`), para os testes serem determinísticos.

### Entidades persistidas sem depender do ORM

> `TEC.Core.Domain` · `IEntity<TKey>`, `ISoftDelete`, `IAuditable`, `AuditableDomainEntity<TId>`, `AuditableAggregateRoot<TId>`

O `IOrmRepository<TEntity, TKey>` do TEC.ORM aceita qualquer entidade que implemente `IEntity<TKey>` e `ISoftDelete`, e grava a
autoria de quem implementa `IAuditable`. Como os três contratos ficam aqui, o projeto de domínio referencia só o TEC.Core
(Clean Architecture: o domínio não conhece a persistência). Auditoria histórica, dado sensível e ordenação ficam no mapeamento
do EF Core, na camada de infraestrutura (ver a Fluent API do TEC.ORM).

| Base | Herda de | Use para |
|---|---|---|
| `AuditableAggregateRoot<TId>` | `AggregateRoot<TId>` | Raiz de agregado persistida, com eventos de domínio |
| `AuditableDomainEntity<TId>` | `DomainEntity<TId>` | Entidade filha persistida (ex.: item do pedido) |

```csharp
public sealed class Pedido : AuditableAggregateRoot<Guid>
{
    private Pedido() { } // EF Core

    public static Result<Pedido> Criar(Guid clienteId, DateTimeOffset agora) { /* invariantes */ }
}
```

Os campos de exclusão lógica e autoria têm setter privado: só a persistência os grava. O `Id` tem setter protegido; o EF Core o
preenche pelos metadados.

---

## ⚙️ Opções

Não há opções nem registro em DI: são tipos base.

## ❌ Erros

| Exceção | Quando ocorre | O que fazer |
|---|---|---|
| `ArgumentNullException` | `RaiseDomainEvent(null)` | Passe o evento |
| `InvalidOperationException` | Mais de `MaxPendingEvents` eventos sem coleta | Laço registrando eventos sem fim, ou lote grande demais num único agregado: salve antes de continuar |

## 🛡️ Segurança

> [!WARNING]
> Eventos de domínio costumam virar mensagens e linhas de histórico. Coloque neles **identificadores e metadados**, nunca senhas, tokens ou textos livres com dados pessoais.

> [!WARNING]
> Não publique o evento de domínio diretamente como contrato entre serviços: renomear uma propriedade quebraria os consumidores. Mapeie para um **evento de integração** versionado (ver TEC.Messaging).

## ❓ Perguntas frequentes

<details>
<summary><b>Os eventos são publicados automaticamente?</b></summary>

Não pelo TEC.Core. Ele só guarda os eventos. A persistência (por exemplo, o interceptor do `TEC.Messaging.SqlServer` ou do `TEC.Messaging.PostgreSql`) os coleta no `SaveChanges` e chama `ClearDomainEvents()`.
</details>

<details>
<summary><b>É thread-safe?</b></summary>

Não. Um agregado pertence a um único caso de uso (um escopo de DI) por vez, como o `DbContext`.
</details>

---

⬅️ [Anterior: Common](common.md) · [📚 Índice](README.md) · [Próximo: Respostas de API](respostas-api.md) ➡️
