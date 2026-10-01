# 🔢 Números

[⬅ Índice](README.md) · [README](../README.md)

Formatação no padrão brasileiro, arredondamento e valores por extenso.

- [NumericExtensions](#numericextensions)
- [NumberToWordsConverter](#numbertowordsconverter)
- [Qual arredondamento usar?](#qual-arredondamento-usar)

---

## NumericExtensions

`TEC.Core.Numbers.Extensions` · métodos de extensão

### Formatação

| Método | Entrada → Saída |
|---|---|
| `string ToCurrency(this decimal value)` | `1234.5m` → `"R$ 1.234,50"` |
| | `-1234.5m` → `"-R$ 1.234,50"` |
| `string ToCurrency(this double value)` | `99.9` → `"R$ 99,90"` |
| `string ToBrazilianNumber(this decimal value, int decimals = 2)` | `1234567.891m` → `"1.234.567,89"` |
| | `1234567.891m.ToBrazilianNumber(0)` → `"1.234.568"` |
| `string ToPercentage(this decimal value, int decimals = 2)` | `0.1575m` → `"15,75%"` (o valor é uma **fração**) |
| | `0.5m.ToPercentage(1)` → `"50,0%"` |

- `ToCurrency` troca o espaço não separável (U+00A0) gerado pela cultura por um espaço comum, o que evita problemas em comparações, PDFs e planilhas.
- `ToCurrency(double)` lança `ArgumentOutOfRangeException` para `NaN`, infinito ou valores fora do intervalo de `decimal`.
- `decimals` aceita de 0 a 28 (`NumericExtensions.MaxDecimals`).

### Arredondamento e cálculo

| Método | Entrada → Saída |
|---|---|
| `decimal RoundHalfUp(this decimal value, int decimals = 2)` | `2.345m` → `2.35` (comercial) |
| `decimal RoundBankers(this decimal value, int decimals = 2)` | `2.345m` → `2.34`; `2.355m` → `2.36` (bancário) |
| `decimal Truncate(this decimal value, int decimals = 2)` | `2.349m` → `2.34` (sem arredondar) |
| `decimal PercentOf(this decimal value, decimal percent)` | `200m.PercentOf(15)` → `30` |

### Extenso e conversão

| Método | Entrada → Saída |
|---|---|
| `string ToCurrencyWords(this decimal value)` | `1234.56m` → `"mil duzentos e trinta e quatro reais e cinquenta e seis centavos"` |
| `string ToWords(this long value)` / `ToWords(this int value)` | `21` → `"vinte e um"` |
| `bool TryParseBrazilianDecimal(this string? value, out decimal result)` | `"R$ 1.234,56"` → `true`, `1234.56` |
| | `"abc"` → `false` |
| | `"1.5"`, `"1.2.3"`, `"12.34"` → `false` (separador de milhar fora de grupos de 3 dígitos) |

`TryParseBrazilianDecimal` aceita sinal no início (`-`/`+`), o prefixo `R$` (uma vez, depois do sinal) e o espaço não separável, e limita a entrada a 64 caracteres. Símbolo no meio do número, sinal no final e parênteses são recusados.

O ponto (separador de milhar) só é aceito **entre grupos de 3 dígitos da parte inteira**: `1.234,56`, `1.234.567` e `1234,56` são válidos; `1.5`, `1.2.3` e `12.34` são recusados. Antes, esses valores eram aceitos ignorando o ponto (`1.5` virava `15` e `1.2.3` virava `123`), o que corrompia valores digitados no padrão americano. Observação: `1.234` continua sendo mil duzentos e trinta e quatro (padrão brasileiro).

```csharp
// Boleto / recibo
var valor = 1234.56m;
var linha = $"{valor.ToCurrency()} ({valor.ToCurrencyWords()})";
// "R$ 1.234,56 (mil duzentos e trinta e quatro reais e cinquenta e seis centavos)"

// Formulário: ler valor digitado pelo usuário
if (!input.TryParseBrazilianDecimal(out var preco))
    throw new RequestValidationException("preco", "Valor inválido.");
```

---

## NumberToWordsConverter

`TEC.Core.Numbers.Words` · `static class`

Converte números para extenso em português do Brasil. Os métodos de extensão acima delegam para esta classe.

| Membro | Descrição |
|---|---|
| `const long MaxValue = 999_999_999_999_999` | Maior valor suportado (novecentos e noventa e nove trilhões...). |
| `string ToWords(long number)` | Inteiro por extenso, inclusive negativos. Fora de ±`MaxValue` → `ArgumentOutOfRangeException`. |
| `string ToCurrencyWords(decimal value)` | Valor em reais por extenso, arredondado para 2 casas (comercial). Acima de ±`MaxValue` → `ArgumentOutOfRangeException`. |

| Entrada | `ToWords` |
|---|---|
| `100` | `"cem"` |
| `101` | `"cento e um"` |
| `1100` | `"mil e cem"` |
| `2026` | `"dois mil e vinte e seis"` |
| `-5` | `"menos cinco"` |
| `1_000_001` | `"um milhão e um"` |
| `1_100_000` | `"um milhão e cem mil"` |
| `999_999` | `"novecentos e noventa e nove mil novecentos e noventa e nove"` |

| Entrada | `ToCurrencyWords` |
|---|---|
| `0m` | `"zero real"` |
| `0.01m` | `"um centavo"` |
| `1m` | `"um real"` |
| `-10.5m` | `"menos dez reais e cinquenta centavos"` |
| `1_000_000m` | `"um milhão de reais"` |
| `2_500_000m` | `"dois milhões e quinhentos mil reais"` |

Regras de português aplicadas:
- `100` é "cem"; de `101` a `199` usa "cento";
- `1000` é "mil" (não "um mil");
- "e" antes do último grupo quando ele é menor que 100 ou uma centena exata: "mil **e** cem", "mil **e** vinte";
- "**de** reais" quando o valor termina em milhão, bilhão ou trilhão exato: "um milhão **de** reais".

---

## Qual arredondamento usar?

| Situação | Método | Por quê |
|---|---|---|
| Preço, nota fiscal, boleto | `RoundHalfUp` | Regra comercial esperada pelo cliente (0,5 sobe). |
| Somatório de muitos valores (juros, rateio) | `RoundBankers` | Distribui o arredondamento para cima e para baixo, sem viés acumulado. |
| Cálculo que não pode favorecer o cliente | `Truncate` | Descarta as casas excedentes. |

> ⚠️ `Math.Round` do .NET usa arredondamento **bancário** por padrão. `Math.Round(2.345m, 2)` retorna `2.34`, não `2.35`. Deixe a intenção explícita com `RoundHalfUp` ou `RoundBankers`.
