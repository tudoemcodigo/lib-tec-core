<div align="center">
<img src="https://www.tudoemcodigo.com/marca/PNG/tudoemcodigo-icone-512.png" alt="Tudo em Código" width="90" />
</div>

[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › Registros de Decisões de Arquitetura (ADRs)

# 🏛️ Registros de Decisões de Arquitetura (ADRs) — TEC.Core

> Este diretório documenta as decisões arquiteturais fundamentais adotadas **exclusivamente no TEC.Core**, seus contextos de negócio, requisitos técnicos, motivações de segurança, alternativas descartadas e consequências no ecossistema de componentes TEC.

## 📑 Índice de Decisões

| ADR | Título | Status | Área / Módulo |
|:---:|---|:---:|---|
| [ADR-0001](0001-result-pattern-e-tratamento-de-erros.md) | Adoção do Result Pattern em Substituição a Exceções para Controle de Fluxo | `Aceito` | `Common/Results` |
| [ADR-0002](0002-envelope-padronizado-api-response-e-mascaramento-de-erros.md) | Envelope Padronizado de Respostas de API e Mascaramento de Erros Internos | `Aceito` | `Responses` |
| [ADR-0003](0003-primitivas-ddd-puras-e-eventos-de-dominio.md) | Primitivas DDD Puras e Acúmulo Delimitado de Eventos de Domínio | `Aceito` | `Domain` |
| [ADR-0004](0004-criptografia-simetrica-assimetrica-e-hibrida-versionada.md) | Arquitetura Criptográfica Versionada (AES-GCM, RSA e Híbrida) com ZeroMemory | `Aceito` | `Cryptography` |
| [ADR-0005](0005-hashing-e-verificacao-de-senhas-com-pbkdf2.md) | Hashing e Verificação de Senhas com PBKDF2 Nativo, Tempo Constante e Normalização Latin-1 | `Aceito` | `Cryptography/Hashing` |
| [ADR-0006](0006-motor-de-csv-streaming-com-anti-formula-injection.md) | Motor de CSV em Streaming com Proteção Contra Injeção de Fórmulas e Modo Estrito | `Aceito` | `Csv` |
| [ADR-0007](0007-calculo-de-dias-uteis-feriados-e-fuso-horario-de-brasilia.md) | Cálculo de Dias Úteis, Feriados Nacionais Móveis e Tratamento de Fuso Horário de Brasília | `Aceito` | `Dates` |
| [ADR-0008](0008-validacao-e-formatacao-de-documentos-e-cnpj-alfanumerico.md) | Validação e Formatação de Documentos com Suporte ao CNPJ Alfanumérico (IN RFB 2.229/2024) | `Aceito` | `Text/Validation` |
| [ADR-0009](0009-coalescencia-de-requisicoes-concorrentes-com-singleflight.md) | Coalescência de Requisições Concorrentes Anti-Cache Stampede com SingleFlight | `Aceito` | `Threading` |
| [ADR-0010](0010-mascaramento-de-dados-pessoais-lgpd-e-describe-untrusted.md) | Mascaramento de Dados Pessoais (LGPD) e Descrição Segura para Logs | `Aceito` | `Text/Masking` |
| [ADR-0011](0011-contrato-de-identidade-de-execucao-com-icurrentuser.md) | Abstração Pura de Identidade de Execução Desacoplada do ASP.NET Core | `Aceito` | `Security` |
| [ADR-0012](0012-serializacao-json-estrita-imutavel-e-compativel-com-aot.md) | Serialização JSON Estrita, Imutável e Compatível com Native AOT | `Aceito` | `Common/Serialization` |
| [ADR-0013](0013-leitura-delimitada-de-arquivos-sensiveis-com-boundedfilereader.md) | Leitura Delimitada de Arquivos de Segredos e Configuração com Zeragem de Buffer | `Aceito` | `IO` |
| [ADR-0014](0014-alvo-unico-net10.md) | Alvo Único net10.0 | `Aceito` | Build e pacote |

---

## 🧭 Critérios e Filosofia Arquitetural do TEC.Core

Todas as decisões registradas neste repositório seguem três pilares invioláveis:

1. **Fundação Pura e Zero Acoplamento Externo:** O `TEC.Core` é a base da pirâmide de componentes TEC. Não possui dependências de frameworks pesados, ORMs, drivers de banco, nem ASP.NET Core MVC. Depende unicamente de `Microsoft.Extensions.DependencyInjection.Abstractions` e APIs nativas da BCL (.NET 10 LTS).
2. **Segurança por Padrão (*Secure by Default*):** Recursos criptográficos autenticados, resistência ativa a ataques de temporização (*timing attacks*), proteção anti-injeção de fórmulas em CSV, mascaramento LGPD, sanitização de erros 500/502 e zeragem ativa de memória sensível (`ZeroMemory`).
3. **Foco no Mercado e Regulamentação Brasileira:** Suporte nativo a particularidades fiscais e bancárias do Brasil (CNPJ alfanumérico, feriados ANBIMA/Bacen, horário de Brasília, formatação de moeda R$ e documentos).


---

## 📐 Template dos ADRs

Todos os ADRs seguem a mesma estrutura, na ordem abaixo (a seção de alternativas é opcional):

| Seção | Conteúdo |
|---|---|
| Cabeçalho | Breadcrumb, título `ADR-NNNN: ...` e a lista **Status** (`Aceito`), **Data** (`AAAA-MM-DD`, com `atualizado em` quando houver revisão), **Área** (namespace ou módulo) e **Decisores** |
| 🎯 Contexto | O problema, as ameaças ou as restrições que motivaram a decisão |
| 💡 Decisão | O que foi adotado, com links relativos para o código-fonte e as diretrizes de projeto |
| ⚖️ Consequências | **Positivas** e **Negativas / Trade-offs** |
| 🔄 Alternativas Consideradas | (Opcional) O que foi descartado e por quê |
| Rodapé | Links para o ADR anterior, o índice e o próximo |

Um novo ADR ganha o próximo número livre e entra na tabela acima.

---

⬅️ [Anterior: Revisão técnica](../revisao-tecnica.md) · [📚 Índice](../README.md)
