# ADR-0011: Abstração Pura de Identidade de Execução Desacoplada do ASP.NET Core

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Security`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

Em arquiteturas empresariais modernas, operações de negócio podem ser disparadas a partir de múltiplos contextos:
- Uma requisição HTTP web com token JWT (ASP.NET Core);
- Um worker ou consumidor de filas de mensageria (RabbitMQ / Kafka) rodando em background;
- Uma rotina agendada (Cron / Hangfire);
- Um script de linha de comando ou migração de banco.

Componentes de persistência (como `TEC.ORM`) precisam preencher automaticamente colunas de auditoria (`CreatedBy`, `UpdatedBy`, `CreatedAt`) e aplicar filtros de isolamento multitenant (`TenantId`). Se essas camadas dependerem de `IHttpContextAccessor` ou de contratos do ASP.NET Core:
1. Os modelos de domínio e ORM tornam-se incapazes de rodar fora do pipeline web do ASP.NET.
2. Identificadores instáveis e legíveis (como e-mail ou username) são gravados em logs de auditoria, gerando passivo de LGPD e falhas quando o colaborador altera seu endereço de e-mail.

## 💡 Decisão

Definir no `TEC.Core` os contratos puros [`ICurrentUser`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Security/ICurrentUser.cs) e [`PrincipalKind`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Security/ICurrentUser.cs#L4):

### Diretrizes de Projeto:
- **Enum `PrincipalKind` Explícito:**
  - `Anonymous` (0): Sem identidade autenticada;
  - `User` (1): Usuário humano interativo ou delegado;
  - `Application` (2): Serviço externo autenticado por credenciais de aplicação (Client Credentials / API Key);
  - `System` (3): Processo interno da própria infraestrutura (workers, jobs, triggers).
- **Consistência Obrigatória de Estado:**
  - `IsAuthenticated` é implementado como membro padrão selado da interface (`sealed bool IsAuthenticated => Kind != PrincipalKind.Anonymous;`), garantindo que nenhuma implementação possa divergir o estado booleano do enum real.
  - Se `Kind == Anonymous`, obrigatoriamente `Id` e `TenantId` são `null`.
- **Identificador Estável e Não Pessoal:**
  - O campo `Id` deve ser um identificador imutável do provedor de identidade (ex.: Subject ID / Object ID GUID do Entra ID/Keycloak), **nunca** e-mail, CPF ou login legível.
- **Isolamento Multitenant:**
  - Propriedade `TenantId` para segregação segura em sistemas SaaS compartilhados.

## ⚖️ Consequências

### Positivas
- **Desacoplamento Total:** O `TEC.ORM` e os agregados de domínio auditam quem executou a ação sem saber se a execução veio de uma API REST, de uma fila de mensageria ou de um console job.
- **Conformidade com LGPD:** Tabelas de banco gravam GUIDs opacos de auditoria em vez de dados pessoais nominativos.
- **Implementação Especializada no `TEC.Security`:** O pacote `TEC.Security` é quem provê a implementação concreta de `ICurrentUser` a partir do `ClaimsPrincipal` do token JWT, mantendo o `TEC.Core` livre de dependências de ASP.NET Core.

### Negativas / Trade-offs
- Em testes unitários que exercitam interceptores de auditoria, é necessário mockar ou instanciar um fake leve de `ICurrentUser`.
