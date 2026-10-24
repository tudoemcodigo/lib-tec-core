[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0008

# ADR-0008: Validação e Formatação de Documentos com Suporte ao CNPJ Alfanumérico (IN RFB 2.229/2024)

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Text.Validation` / `Formatting`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

1. **Esgotamento do CNPJ Numérico:** A Receita Federal do Brasil (RFB) estabeleceu, por meio da **Instrução Normativa RFB nº 2.229/2024** (e anteriores), que a partir de 2026 novos cadastros de pessoas jurídicas adotarão o formato **CNPJ Alfanumérico**. Sistemas legados que assumem apenas dígitos (`char.IsDigit`) rejeitarão cadastros legítimos de empresas recém-criadas.
2. **Ataques de Expressões Regulares (ReDoS):** Validadores ingênuos de e-mail e documentos baseados em regexes retroativas sofrem travamentos de CPU (catastrophic backtracking) ao receber entradas maliciosas com repetições gigantes.
3. **Inconsistência entre Validação e Formatação:** Divergências frequentes onde um validador aceita um texto com caracteres espúrios, mas o formatador falha ao tentar aplicar a máscara.

## 💡 Decisão

Implementar validadores e formatadores de alta performance no namespace `TEC.Core.Text`:

### 1. Suporte Nativo ao CNPJ Alfanumérico
- [`DocumentValidator.IsValidCnpj`](../../TEC.Core/Text/Validation/DocumentValidator.cs) e [`CheckDigitCalculator.CalculateCnpj`](../../TEC.Core/Text/Internal/CheckDigitCalculator.cs):
  - Os primeiros 12 caracteres podem conter dígitos de `0` a `9` e letras maiúsculas de `A` a `Z`.
  - Os 2 dígitos verificadores finais permanecem estritamente numéricos.
  - **Fórmula de Módulo 11:** O valor decimal de cada caractere para cálculo dos pesos é definido pelo seu código ASCII subtraído de 48 (`caractere - '0'`), em conformidade com o manual técnico oficial da Receita Federal.
  - Formatação com máscara (`XX.XXX.XXX/XXXX-XX`) e desmascaramento suportam plenamente letras maiúsculas.

### 2. Validações Abrangentes
- **CPF:** Dígitos verificadores (módulo 11) e rejeição imediata de sequências repetidas (ex.: `111.111.111-11`).
- **PIS/PASEP/NIT:** Cálculo oficial de dígito verificador com pesos `[3, 2, 9, 8, 7, 6, 5, 4, 3, 2]`.
- **CEP:** 8 dígitos com rejeição de sequências repetidas e caracteres inválidos antes de qualquer alocação.
- **Telefone Brasileiro:** DDDs válidos baseados na tabela oficial da Anatel (67 DDDs cadastrados em hash set), distinção entre fixo (10 dígitos) e celular (11 dígitos com 9 na 3ª posição) e suporte ao prefixo de país `+55`.
- **E-mail (RFC 5321):** Validação com Regex compilada via Source Generator do C# (`[GeneratedRegex]`) utilizando a flag [`RegexOptions.NonBacktracking`](https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-options#nonbacktracking-mode) e limite de 254 caracteres, garantindo tempo de execução linear $O(n)$ imune a ReDoS.

### 3. Otimizações de Alocação (Zero/Low Allocation)
- Cálculos de módulo 11 utilizam `stackalloc char` e `ReadOnlySpan<char>`, evitando alocações temporárias no Garbage Collector durante validações em lote.

## ⚖️ Consequências

### Positivas
- **Prontidão Fiscal e Mercadológica:** Aplicações construídas com `TEC.Core` já nascem 100% preparadas para emitir, receber e processar notas fiscais e cadastros com o novo CNPJ alfanumérico.
- **Resiliência a Negação de Serviço:** Imunidade comprovada contra ataques de ReDoS e exaustão de heap via `NonBacktracking`.
- **Confiabilidade:** Geradores randômicos de documentos válidos ([`DocumentGenerator`](../../TEC.Core/Text/Generation/DocumentGenerator.cs)) para ambientes de testes e seed de dados.

### Negativas / Trade-offs
- Sistemas legados integrados ainda em formato puramente numérico precisarão de adequação nas suas colunas de banco (de `numeric(14)` para `varchar(14)`).

---

⬅️ [Anterior: ADR-0007](0007-calculo-de-dias-uteis-feriados-e-fuso-horario-de-brasilia.md) · [🏛️ Índice dos ADRs](README.md) · [Próximo: ADR-0009](0009-coalescencia-de-requisicoes-concorrentes-com-singleflight.md) ➡️
