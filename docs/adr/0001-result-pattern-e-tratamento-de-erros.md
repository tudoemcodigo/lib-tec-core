[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0001

# ADR-0001: Adoção do Result Pattern em Substituição a Exceções para Controle de Fluxo

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Common.Results`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

Em arquiteturas orientadas a microsserviços e alta volumetria, o uso de exceções (`throw new Exception()`) para controlar fluxos de negócio esperados (ex.: saldo insuficiente, recurso não encontrado, validação de campos) impõe graves desvantagens:

1. **Penalidade Severa de Desempenho:** A captura do stack trace do runtime (`ExceptionDispatchInfo` / unwinding de pilha) consome ordens de grandeza a mais de CPU e alocações de heap do que a devolução de estruturas na stack.
2. **Perda de Tipagem e Previsibilidade:** Métodos que lançam exceções não declaram em suas assinaturas os motivos de falha, forçando blocos `catch` genéricos e facilitando o mascaramento de bugs reais de infraestrutura.
3. **Vazamento de Detalhes Internos:** O tratamento inconsistente de exceções em controladores web frequentemente expõe stack traces e mensagens técnicas a clientes externos.

## 💡 Decisão

Implementar no `TEC.Core` os tipos [`Result`](../../TEC.Core/Common/Results/Result.cs), [`Result<T>`](../../TEC.Core/Common/Results/Result.cs), [`Error`](../../TEC.Core/Common/Results/Error.cs) e o enum [`ErrorType`](../../TEC.Core/Common/Results/ErrorType.cs) como o padrão canônico para todos os componentes do ecossistema (`TEC.Cqrs`, `TEC.ORM`, `TEC.Vault`, etc.).

### Diretrizes de Projeto:
- **Imutabilidade e Integridade:** `Result` e `Error` são imutáveis. Um resultado de sucesso não pode conter erros, e uma falha exige obrigatoriamente ao menos um `Error`. Subclasses externas são bloqueadas via `private protected`.
- **Composição Funcional:** Disponibilização dos operadores `Bind`, `BindAsync`, `Map`, `MapAsync`, `Ensure` e bifurcações completas via `Match` (com sobrecargas `Func` e `Action` tanto no `Result` não-genérico quanto no `Result<T>`) para encadeamento elegante de operações sem ramificações imperativas.
- **Mapeamento HTTP Semântico:** Cada categoria de erro (`ErrorType`) possui status HTTP mapeado estritamente (`ToHttpStatusCode()`) e uma política declarativa de exposição ao cliente (`IsExposedToClient()`).
- **Conversão Implícita:** Operadores de conversão implícita de `T` e `Error` para `Result<T>` simplificam a escrita limpa de retornos.

## ⚖️ Consequências

### Positivas
- **Desempenho Extremo:** Validações e rejeições de negócio executam sem custo de stack trace.
- **Consistência Ecossistêmica:** Todos os componentes TEC compartilham os mesmos contratos de erro e status.
- **Integração Fluida com APIs:** Conversão direta de `Result` para [`ApiResponse`](../../TEC.Core/Responses/ApiResponse.cs) sem código repetitivo em controladores ou Minimal APIs.

### Negativas / Trade-offs
- Exige disciplina da equipe para não acessar `.Value` em caso de falha (protegido por `InvalidOperationException`).
- Desenvolvedores acostumados ao paradigma tradicional de exceções precisam assimilar a composição funcional (`Bind`/`Match`).

## 🔄 Alternativas Consideradas
1. **Bibliotecas de Terceiros (ex.: FluentResults, OneOf, LanguageExt):** Rejeitadas para evitar dependências externas transitivas no pacote fundacional do ecossistema e manter total compatibilidade com Native AOT.
2. **Exceções Customizadas por Domínio:** Rejeitadas para fluxos esperados de negócio; exceções (`AppException`) foram reservadas apenas para cenários genuinamente excepcionais e falhas irrecuperáveis.

---

⬅️ [🏛️ Índice dos ADRs](README.md) · [Próximo: ADR-0002](0002-envelope-padronizado-api-response-e-mascaramento-de-erros.md) ➡️
