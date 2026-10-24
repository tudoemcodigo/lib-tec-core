[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0009

# ADR-0009: Coalescência de Requisições Concorrentes Anti-Cache Stampede com SingleFlight

- **Status:** Aceito
- **Data:** 2026-10-08 (atualizado em 2026-10-19)
- **Área:** `TEC.Core.Threading`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

Quando dados armazenados em cache (Redis, memória, etc.) expiram sob alta carga de requisições concorrentes, centenas ou milhares de threads simultâneas constatam a ausência da chave e disparam a mesma query cara no banco de dados ou a mesma chamada a uma API externa de terceiros. Esse fenômeno — conhecido como **Cache Stampede** ou **Thundering Herd** — costuma provocar sobrecarga imediata, picos brutais de CPU, saturação de conexões e indisponibilidade do serviço (*cascading failure*).

## 💡 Decisão

Implementar a classe [`SingleFlight<TKey, TValue>`](../../TEC.Core/Threading/SingleFlight.cs) no `TEC.Core.Threading`:

### Diretrizes de Projeto:
- **Uma Única Execução por Chave:** Para qualquer conjunto de chamadas simultâneas com a mesma chave, apenas a primeira chamada dispara a operação assíncrona. As demais recebem a mesma `Task` e aguardam em repouso o resultado.
- **Não Retenção Pós-Conclusão:** Assim que a operação é concluída (com sucesso ou falha), o registro é removido do dicionário interno. `SingleFlight` não é um cache em si, mas a barreira de sincronização que protege a carga do cache.
- **Propagação Fiel de Exceções:** Se a operação original falhar com uma exceção, todos os chamadores que aguardavam a mesma chave recebem a mesma exceção, e a chave fica liberada para uma nova tentativa imediata.
- **Cancelamento Cooperativo Multichamador:**
  - Cada chamador possui seu próprio `CancellationToken`. Desistir da espera não cancela a operação compartilhada enquanto outros chamadores continuarem aguardando.
  - A operação em andamento só recebe sinal de cancelamento se **todos** os chamadores que a aguardavam desistirem.
- **Isolamento de Travas e Execução Segura:**
  - O cancelamento do token da operação é disparado **fora da trava** (`Lock`). Callbacks de cancelamento síncronos (como encerramento de sockets ou cancelamento de comandos ADO.NET) nunca bloqueiam as demais chaves do dicionário.
  - Exceções disparadas dentro de callbacks de cancelamento não substituem a `OperationCanceledException` do chamador.

## ⚖️ Consequências

### Positivas
- **Blindagem do Banco de Dados:** Elimina picos de queries redundantes em momentos de expiração de cache.
- **Economia Operacional:** Reduz custos de tráfego de rede e consumo de cotas de APIs externas pagas.
- **Thread-Safety sem Deadlocks:** Arquitetura com seções críticas curtas usando a nova primitiva de trava nativa do .NET (`System.Threading.Lock`).

### Negativas / Trade-offs
- A operação executada não deve ter efeitos colaterais mutáveis exclusivos por usuário (deve ser idempotente ou de leitura), já que o mesmo retorno será compartilhado entre todos os chamadores em voo.

---

⬅️ [Anterior: ADR-0008](0008-validacao-e-formatacao-de-documentos-e-cnpj-alfanumerico.md) · [🏛️ Índice dos ADRs](README.md) · [Próximo: ADR-0010](0010-mascaramento-de-dados-pessoais-lgpd-e-describe-untrusted.md) ➡️
