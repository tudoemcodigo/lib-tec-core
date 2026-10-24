[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0003

# ADR-0003: Primitivas DDD Puras e Acúmulo Delimitado de Eventos de Domínio

- **Status:** Aceito
- **Data:** 2026-10-09
- **Área:** `TEC.Core.Domain`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

Modelos de domínio frequentemente sofrem acoplamento precoce com tecnologias de persistência (como Entity Framework Core) ou com bibliotecas de mensageria (como MassTransit ou MediatR). Isso compromete a pureza da arquitetura limpa (Clean Architecture / Onion), dificulta testes unitários puros e introduz riscos de memory leak quando agregados emitem grandes volumes de eventos em laços repetitivos.

## 💡 Decisão

Definir primitivas fundamentais de DDD no namespace [`TEC.Core.Domain`](../../TEC.Core/Domain) sem qualquer dependência de persistência ou mensageria:

### 1. [`DomainEntity<TId>`](../../TEC.Core/Domain/AggregateRoot.cs)
- Identidade explícita via `Id` genérico (`TId : notnull`).
- Regra de igualdade estrutural estrita: duas entidades são iguais **apenas** quando possuem o mesmo tipo concreto em tempo de execução e o mesmo `Id`.
- Proteção para entidades transientes (sem `Id` persistido): são tratadas como transientes e comparadas por identidade de referência (`ReferenceEquals`), evitando que duas entidades novas recém-instanciadas colidam em hashsets ou coleções.

### 2. [`AggregateRoot<TId>`](../../TEC.Core/Domain/AggregateRoot.cs) e [`IDomainEvent`](../../TEC.Core/Domain/IDomainEvent.cs)
- A raiz de agregado gerencia o ciclo de vida e a consistência interna de seus objetos filhos.
- Disparo de eventos via método protegido `RaiseDomainEvent(IDomainEvent)`.
- **Limite Defensivo de Segurança:** Definido `MaxPendingEvents = 1000`. Se uma operação tentar registrar mais de 1000 eventos sem commit/coleta, uma `InvalidOperationException` é lançada, protegendo a aplicação contra loops infinitos de negócio e exaustão de heap.
- Interface [`IHasDomainEvents`](../../TEC.Core/Domain/IDomainEvent.cs): O agregado acumula os eventos em memória; quem os coleta e limpa (`ClearDomainEvents()`) é a camada de persistência (ex.: interceptor do `TEC.Messaging.SqlServer` ou do `TEC.Messaging.PostgreSql` gravando no padrão Transactional Outbox).

## ⚖️ Consequências

### Positivas
- **Independência Tecnológica:** O domínio pode ser compilado e testado sem carregar pacotes de banco de dados ou mensageria.
- **Proteção Contra Vazamentos de Memória:** Teto claro e explícito de eventos pendentes por transação de agregado.
- **Isolamento de Contratos:** Eventos de domínio são estritamente internos ao microsserviço. O `TEC.Messaging` é responsável por transformá-los em eventos de integração versionados para barramentos externos.

### Negativas / Trade-offs
- Entidades que precisam de recursos avançados de ORM (como soft delete e auditoria automática) devem herdar das classes especializadas do `TEC.ORM` quando o domínio admitir esse acoplamento.

---

⬅️ [Anterior: ADR-0002](0002-envelope-padronizado-api-response-e-mascaramento-de-erros.md) · [🏛️ Índice dos ADRs](README.md) · [Próximo: ADR-0004](0004-criptografia-simetrica-assimetrica-e-hibrida-versionada.md) ➡️
