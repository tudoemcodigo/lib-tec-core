[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0002

# ADR-0002: Envelope Padronizado de Respostas de API e Mascaramento de Erros Internos

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Responses`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

A falta de um padrão único de resposta HTTP em serviços corporativos gera dois problemas críticos:
1. **Inconsistência nos Clientes:** Frontends e integrações parceiras precisam tratar formatos divergentes de payload para sucesso, validações e falhas entre diferentes microsserviços.
2. **Vazamento de Dados de Infraestrutura (CWE-209 / OWASP A05):** Quando falhas inesperadas (500 Internal Server Error) ou falhas em dependências externas (502 Bad Gateway) ocorrem, middlewares padrão ou desenvolvedores desatentos frequentemente vazam exceções com nomes de servidores de banco, tabelas, queries SQL ou chaves de conexão.

## 💡 Decisão

Adotar os registros [`ApiResponse`](../../TEC.Core/Responses/ApiResponse.cs), [`ApiResponse<T>`](../../TEC.Core/Responses/ApiResponse.cs), [`PagedResponse<T>`](../../TEC.Core/Responses/PagedResponse.cs) e [`ApiError`](../../TEC.Core/Responses/ApiError.cs) como o envelope universal de resposta REST.

### Diretrizes de Projeto:
- **Estrutura Previsível:** O envelope contém sempre `success`, `statusCode`, `message`, `data` (em `ApiResponse<T>`), `errors` (array de `ApiError`), `timestamp` (UTC) e `traceId` (opcional, para correlação).
- **Regra Rígida de Não Vazamento:** O método `MapErrors` avalia toda a lista de erros: se **qualquer** erro for de categoria não pública (`ErrorType.Failure` ou `ErrorType.ExternalService`), a resposta inteira é sanitizada, a lista de `errors` é limpa e uma mensagem padronizada em pt-BR é devolvida (`ApiResponse.DefaultMessages.InternalError` ou `ExternalService`).
- **Conversão Direta de `Result` e `Exception`:** Métodos de fábrica `FromResult(Result)` e `FromException(Exception)` mapeiam o domínio e exceções (`AppException`) de forma transparente e segura.
- **Ordenação Serializada:** Propriedades possuem anotação `[JsonPropertyOrder]` para garantir que `success`, `statusCode` e `message` apareçam no início do JSON.

## ⚖️ Consequências

### Positivas
- **Segurança Reforçada:** Detalhes de banco de dados, falhas de rede ou stack traces nunca chegam ao consumidor final.
- **Facilidade de Integração:** Clientes HTTP (web, mobile, parceiros) consomem uma única estrutura contratual em todo o ecossistema TEC.
- **Rastreabilidade:** O campo `TraceId` conecta a resposta HTTP aos logs e traces do OpenTelemetry no `TEC.Observability`.

### Negativas / Trade-offs
- Respostas de sucesso com dados pagam o pequeno overhead de um envelope ao invés de retornar o JSON do modelo puro na raiz.

---

⬅️ [Anterior: ADR-0001](0001-result-pattern-e-tratamento-de-erros.md) · [🏛️ Índice dos ADRs](README.md) · [Próximo: ADR-0003](0003-primitivas-ddd-puras-e-eventos-de-dominio.md) ➡️
