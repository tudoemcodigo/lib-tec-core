# ADR-0012: Serialização JSON Estrita, Imutável e Compatível com Native AOT

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Common.Serialization`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

O serializador padrão `System.Text.Json` no .NET, em sua configuração out-of-the-box:
1. **Modifica Estados Globais Acidentalmente:** Instâncias de `JsonSerializerOptions` compartilhadas podem ser mutadas por qualquer trecho de código em tempo de execução, alterando o comportamento de serialização de outros módulos sem aviso.
2. **Serialização Numérica de Enums Insegura:** Por padrão, enums são serializados como inteiros opacos (`Status = 2`). Além disso, o conversor de string padrão do .NET aceita números e combinações arbitrárias de strings como válidas na desserialização, abrindo brechas para injeção de estados inválidos na aplicação.
3. **Escaping Agressivo de Acentos vs Vulnerabilidade a XSS:** O encoder padrão do .NET transforma acentos e caracteres em português em sequências unicode (`\u00E1`, `\u00E7`), tornando logs e JSONs ilegíveis. Por outro lado, desligar o encoder (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`) deixa a aplicação suscetível a Cross-Site Scripting (XSS) caso o JSON seja injetado em templates HTML.
4. **Incompatibilidade com Trimming e Native AOT:** O uso de reflexão dinâmica tradicional quebra builds compilados para binários nativos AOT.

## 💡 Decisão

Padronizar a serialização via [`JsonDefaults`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Common/Serialization/JsonDefaults.cs) e [`JsonExtensions`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Common/Serialization/JsonExtensions.cs):

### Diretrizes de Projeto:
- **Imutabilidade Absoluta:**
  - As instâncias estáticas `JsonDefaults.Options` e `JsonDefaults.IndentedOptions` são congeladas em tempo de inicialização via `.MakeReadOnly()`. Qualquer tentativa de modificação em tempo de execução lança `InvalidOperationException`.
- **Nomenclatura e Valores Nulos:**
  - Formato camelCase (`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`).
  - Omissão de valores nulos na escrita (`DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`), economizando largura de banda em payloads de rede.
- **Encoder Seguro para Língua Portuguesa:**
  - Uso de um `JavaScriptEncoder` customizado que mantém caracteres latinos acentuados perfeitamente legíveis (`UnicodeRanges.BasicLatin`, `Latin1Supplement`, `LatinExtendedA`), mas **continua escapando rigorosamente** caracteres perigosos para HTML (`<`, `>`, `&`, `'`, `+`), bloqueando ataques de XSS.
- **Conversão Estrita de Enums ([`StrictEnumConverter<T>`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Common/Serialization/StrictEnumConverter.cs)):**
  - Enums são serializados como texto camelCase.
  - Na leitura, apenas nomes explicitamente definidos no enum são aceitos; números puros, inteiros entre aspas ou combinações arbitrárias por vírgula são recusados com erro estrito de validação.
- **Suporte Nativo a AOT:**
  - Métodos sobrecarregados em `JsonDefaults` e `JsonExtensions` que aceitam resolvedores compilados (`IJsonTypeInfoResolver`, `JsonTypeInfo<T>`, `JsonSerializerContext`), garantindo compilação limpa sem avisos de IL2026/IL3050 em Native AOT.

## ⚖️ Consequências

### Positivas
- **JSONs Legíveis e Seguros:** Acentos brasileiros são preservados em logs e contratos sem introduzir vulnerabilidades XSS.
- **Integridade de Domínio:** Bloqueio imediato de inteiros fora do enum em contratos de entrada de APIs.
- **Zero Surpresas:** Configurações globais imutáveis eliminam efeitos colaterais entre diferentes bibliotecas do processo.

### Negativas / Trade-offs
- Para ambientes compilados com Native AOT é mandatório declarar um `JsonSerializerContext` com source generators em vez de utilizar as sobrecargas baseadas em reflexão.
