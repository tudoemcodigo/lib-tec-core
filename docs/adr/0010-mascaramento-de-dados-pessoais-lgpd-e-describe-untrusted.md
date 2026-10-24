[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0010

# ADR-0010: Mascaramento de Dados Pessoais (LGPD) e Descrição Segura para Logs

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Text.Masking`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

1. **Conformidade com a LGPD (Lei nº 13.709/2018):** Dados pessoais e sensíveis (CPF, CNPJ de MEI, telefone, e-mail, cartão de crédito) não podem ser expostos na íntegra em interfaces públicas nem registrados em logs de texto claro.
2. **Corrupção de Strings UTF-16:** Funções ingênuas de mascaramento cortam strings por índice numérico puro. Quando o texto contém caracteres fora do Basic Multilingual Plane (emojis ou caracteres compostos representados por **pares substitutos / surrogate pairs** de 2 `char` no C#), cortar no índice intermediário gera uma metade de surrogate órfã, corrompendo a serialização UTF-8 e causando exceções de codificação.
3. **Vazamento Acidental de Segredos em Logs (*Pasting Mistakes*):** Frequentemente, usuários colam senhas, tokens de acesso ou dados confidenciais em campos errados (como "Nome" ou "Documento"). Se a aplicação rejeitar o dado e imprimir o valor recusado no log (`"Valor inválido: {input}"`), segredos são gravados em texto claro em ferramentas de observabilidade (Datadog, Elastic, CloudWatch), violando normas PCI-DSS e SOC2.

## 💡 Decisão

Implementar no [`SensitiveDataMasker`](../../TEC.Core/Text/Masking/SensitiveDataMasker.cs) rotinas determinísticas de anonimização:

### 1. Mascaramento Especializado por Documento
- **CPF:** Padrão do governo federal `***.456.789-**`.
- **CNPJ:** Formato corporativo `**.345.678/****-**` (inclusive com suporte a alfanumérico).
- **E-mail:** Mantém até 2 caracteres locais e o domínio completo (`jo******@empresa.com.br`).
- **Telefone:** Mantém o DDD e os 4 últimos dígitos (`(11) *****-1234` ou com prefixo `+55`).
- **Cartão de Crédito:** Mantém estritamente os 4 últimos dígitos (`**** **** **** 1234`), em conformidade com o PCI-DSS v4.0.

### 2. Preservação de Pares Substitutos (*Surrogate Pair Safety*)
- No método genérico `Mask(value, visibleStart, visibleEnd, maskChar)`, se o ponto de corte coincidir com a fronteira entre um *high surrogate* e um *low surrogate*, a fronteira é recuada automaticamente, garantindo que nenhum caractere UTF-16 seja partido ao meio.

### 3. Registro Seguro de Textos Não Confiáveis ([`DescribeUntrusted`](../../TEC.Core/Text/Masking/SensitiveDataMasker.cs))
- Para descrever entradas inválidas em logs sem nunca reproduzir o conteúdo:
  - Gera uma descrição contendo apenas a **quantidade de caracteres** e um **identificador curto** derivado de **HMAC-SHA256**.
  - Exemplo de log gerado: `<32 caracteres, hmac:3fa0c19b7e21>`.
  - **Chave Efêmera do Processo:** O HMAC utiliza uma chave aleatória de 256 bits gerada na memória estática do processo (`UntrustedTextKey`). A chave **nunca é persistida nem exposta**.
  - **Segurança Criptográfica:** Um hash puro (como SHA-256) permitiria que atacantes testassem dicionários ou listas de senhas vazadas para descobrir o que foi digitado; com o HMAC de chave efêmera, é impossível confirmar palpites fora da memória daquela instância ativa, servindo unicamente para correlacionar o mesmo erro no mesmo ciclo de vida da aplicação.

## ⚖️ Consequências

### Positivas
- **Conformidade Regulatória Imediata:** Facilita a adequação a auditorias LGPD e PCI-DSS em todos os serviços.
- **Logs Limpos e Blindados:** Elimina o risco de vazamento involuntário de senhas e dados confidenciais colados em formulários.
- **Integridade de Codificação:** Nenhuma string é corrompida por corte incorreto de emojis ou símbolos especiais.

### Negativas / Trade-offs
- O HMAC de `DescribeUntrusted` muda a cada reinicialização da aplicação; não é possível correlacionar o hash entre deploys diferentes, o que é proposital por desenho de segurança.

---

⬅️ [Anterior: ADR-0009](0009-coalescencia-de-requisicoes-concorrentes-com-singleflight.md) · [🏛️ Índice dos ADRs](README.md) · [Próximo: ADR-0011](0011-contrato-de-identidade-de-execucao-com-icurrentuser.md) ➡️
