[🏠 TEC.Core](../README.md) › [📚 Documentação](README.md) › Compatibilidade

# ⚡ Compatibilidade

> Onde o TEC.Core roda e o que muda entre .NET 8 e .NET 10, com Native AOT, em containers enxutos e sob concorrência.

## 📑 Sumário

- [🎯 Visão geral](#-visão-geral)
- [🚀 Uso](#-uso)
  - [.NET 8 e .NET 10](#net-8-e-net-10)
  - [Native AOT e trimming](#native-aot-e-trimming)
  - [Containers sem ICU ou sem tzdata](#containers-sem-icu-ou-sem-tzdata)
  - [Thread-safety](#thread-safety)
  - [Relógio testável](#relógio-testável)
  - [Observabilidade](#observabilidade)
- [⚙️ Opções](#️-opções)
- [❌ Erros](#-erros)
- [🛡️ Segurança](#️-segurança)
- [❓ Perguntas frequentes](#-perguntas-frequentes)

---

## 🎯 Visão geral

| Item | .NET 8 (LTS) | .NET 10 (LTS) | Native AOT | Trimming |
|---|:---:|:---:|:---:|:---:|
| Pacote | ✅ `lib/net8.0` | ✅ `lib/net10.0` | ✅ `IsAotCompatible` | ✅ |
| Criptografia, texto, datas, números, respostas, DI | ✅ | ✅ | ✅ sem reflexão | ✅ |
| CSV e enums | ✅ | ✅ | ✅ tipos anotados com `[DynamicallyAccessedMembers]` | ✅ |
| JSON com `JsonSerializerContext`/`JsonTypeInfo<T>` | ✅ | ✅ | ✅ | ✅ |
| JSON por reflexão (`JsonDefaults.Options`, `ToJson<T>(bool)`, `FromJson<T>()`) | ✅ | ✅ | ⚠️ aviso IL2026/IL3050 | ⚠️ aviso |
| `params IEnumerable<…>` com argumentos avulsos | ⚠️ C# 13+ | ✅ | — | — |

| Item | Suporte |
|---|---|
| Comportamento entre TFMs | Idêntico em `net8.0` e `net10.0`: os testes rodam nos dois runtimes |
| Sistemas | Windows, Linux e macOS (o CI roda em Linux; desenvolvimento em Windows) |
| Nullable reference types | ✅ habilitado; avisos são erros no build do pacote |
| `InvariantGlobalization=true` | ✅ cultura pt-BR própria como fallback |
| Container sem `tzdata` | ✅ UTC−03:00 fixo como fallback |
| Documentação XML | ✅ IntelliSense em português |

---

## 🚀 Uso

### .NET 8 e .NET 10

- APIs do .NET 9+ usadas internamente (`Convert.ToHexStringLower`, `Convert.FromHexString(…, Span<byte>)`, `System.Buffers.Text.Base64Url`, `OverloadResolutionPriorityAttribute`) têm equivalentes internos no alvo `net8.0`, com testes que comparam os dois caminhos (`CompatibilityTests`). O codificador público [`Base64UrlEncoder`](texto.md#base64urlencoder) dá o mesmo resultado nos dois alvos.
- Métodos com `params IEnumerable<…>` (ex.: `ApiResponse.ValidationError(...)`, `ApiResponse.BadRequest(...)`, `RequestValidationException(Error, params IEnumerable<Error>)`) aceitam argumentos avulsos só com **C# 13+**. Projetos `net8.0` usam C# 12 por padrão:

```csharp
using TEC.Core.Responses;

var cpfError = new ApiError("CPF_INVALIDO", "CPF inválido.", "cpf");
var emailError = new ApiError("EMAIL_OBRIGATORIO", "E-mail é obrigatório.", "email");

// net8.0 com C# 12: passe uma coleção
var response = ApiResponse.ValidationError([cpfError, emailError]);

// C# 13+ (net10.0, ou <LangVersion>13</LangVersion> com o SDK 9+): argumentos avulsos
var same = ApiResponse.ValidationError(cpfError, emailError);
```

- `ToListAsync()` sobre `IAsyncEnumerable<T>` (ex.: leitura de CSV) é nativo no .NET 10; no .NET 8, use o pacote `System.Linq.AsyncEnumerable` ou um `await foreach`.

### Native AOT e trimming

| Área | Situação | Alternativa compatível com AOT |
|---|---|---|
| `JsonDefaults.Options`, `JsonDefaults.IndentedOptions` | ⚠️ `[RequiresUnreferencedCode]` + `[RequiresDynamicCode]` (resolvedor por reflexão) | `JsonDefaults.CreateOptions(IJsonTypeInfoResolver?)` + `JsonSerializerContext` gerado |
| `JsonDefaults.CreateOptions(bool)` | ⚠️ `[RequiresDynamicCode]` (conversor de enums criado em tempo de execução) | `CreateOptions(IJsonTypeInfoResolver?)` + `JsonDefaults.CreateEnumConverter<TEnum>()` |
| `ToJson<T>(bool)`, `FromJson<T>()`, `TryFromJson<T>(out)` | ⚠️ `[RequiresUnreferencedCode]` + `[RequiresDynamicCode]` | Sobrecargas com `JsonTypeInfo<T>` ou `JsonSerializerContext` |
| CSV (`CsvReader`, `CsvWriter`, `ICsvReader`, `ICsvWriter`) | ✅ `T` anotado com `[DynamicallyAccessedMembers(PublicProperties)]` | — |
| Enums (`EnumHelper`, `EnumExtensions`) | ✅ `TEnum`/`Type` anotados com `[DynamicallyAccessedMembers(PublicFields)]` | — |
| Demais módulos | ✅ sem reflexão | — |

O caminho AOT completo para JSON (contexto gerado com as mesmas convenções de `JsonDefaults.Options`) está em [common.md](common.md#jsonextensions). O pacote é compilado com `IsAotCompatible=true` e os analisadores de trimming/AOT como erro: um uso novo de reflexão sem anotação quebra o build.

### Containers sem ICU ou sem tzdata

| Situação | O que o TEC.Core faz |
|---|---|
| `InvariantGlobalization=true` (ou `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`) | `BrazilianCulture.Instance` monta uma cultura pt-BR equivalente (vírgula decimal, `R$`, meses e dias em português, `dd/MM/yyyy`); remoção de acentos e comparações sem acento não dependem de ICU |
| Imagem sem `tzdata` | `BrazilTimeZone.Info` cai para um fuso fixo UTC−03:00 (o Brasil não tem horário de verão desde 2019) |

```xml
<!-- Imagem enxuta: o TEC.Core continua formatando em pt-BR -->
<PropertyGroup>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

> [!TIP]
> Use sempre `BrazilianCulture.Instance` em vez de `CultureInfo.GetCultureInfo("pt-BR")`, que lança
> `CultureNotFoundException` sem ICU. Rode os testes também com `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

### Thread-safety

Os serviços registrados por `AddTecCore()` e `AddBusinessDayCalculator()` não guardam estado mutável próprio e são seguros como Singleton; `Holiday`, `Error`, `Result` e as respostas são imutáveis. As classes estáticas não têm estado. O que **você** fornece precisa ser thread-safe:

| Item fornecido pela aplicação | Requisito |
|---|---|
| `IHolidayProvider`/`IHolidaySource` próprios | Leituras concorrentes seguras |
| `CsvOptions.Culture` | `CultureInfo` somente leitura (ex.: `BrazilianCulture.Instance`) |
| `CsvOptions.OnInvalidRow` | Callback chamado na thread da leitura; use `Interlocked`/coleções concorrentes se compartilhar estado |
| `JsonSerializerOptions` customizadas | Não altere depois do primeiro uso (as de `JsonDefaults` já são somente leitura) |

### Relógio testável

As funções que dependem de "agora" têm sobrecarga com `TimeProvider` (padrão: `TimeProvider.System`):

| Sem relógio injetado | Com relógio injetado |
|---|---|
| `BrazilTimeZone.Now` / `BrazilTimeZone.Today` | `BrazilTimeZone.GetNow(TimeProvider)` / `BrazilTimeZone.GetToday(TimeProvider)` |
| `DateOnly.CalculateAge()` | `DateOnly.CalculateAge(TimeProvider)` |
| `DateTime.ToRelativeTime()` | `DateTime.ToRelativeTime(TimeProvider)` |

Detalhes e exemplos com `FakeTimeProvider` em [datas.md](datas.md#braziltimezone).

### Observabilidade

O TEC.Core **não emite traces, métricas nem logs** (sem `ActivitySource`/`Meter` próprios): é uma biblioteca sem estado e sem I/O próprio. Ele oferece pontos de apoio para a aplicação observar o que acontece:

| Recurso | Tipo | Uso |
|---|---|---|
| `ApiResponse.TraceId` | Propriedade (JSON `traceId`) | Correlaciona a resposta HTTP com o trace/log (`FromException(ex, traceId)` ou `with { TraceId = ... }`) |
| `AppException.Code` / `ErrorType` / `StatusCode` | Propriedades | Dimensões estáveis para logs e métricas da aplicação |
| `IntegrationException.ServiceName` / `InvalidConfigurationException.SettingName` | Propriedades | Identificam a dependência ou a configuração que falhou, só no log |
| `HolidayCalendarBuilder.OnSourceError` / `HolidaySourceFailure` | Callback / registro | Registra fontes de feriados opcionais que falharam |
| `SensitiveDataMasker` | Utilitário | Mascara dados pessoais antes de registrar em log |
| `CsvException.LineNumber` / `ColumnName` | Propriedades | Localizam a linha com erro sem expor o conteúdo do arquivo |

> [!TIP]
> Para traces, métricas, logs estruturados e health checks, use o **TEC.Observability**.

---

## ⚙️ Opções

| Opção (no projeto da aplicação) | Padrão | Efeito |
|---|---|---|
| `<LangVersion>` | C# 12 em `net8.0`, C# 14 em `net10.0` | C# 13+ habilita argumentos avulsos em `params IEnumerable<…>` |
| `<InvariantGlobalization>` | `false` | `true` usa o fallback pt-BR do TEC.Core |
| `<PublishAot>` / `<PublishTrimmed>` | `false` | Use as sobrecargas de JSON com contexto gerado para não ter avisos |

---

## ❌ Erros

| Erro | Quando ocorre | O que fazer |
|---|---|---|
| `CS1503`/`CS1501` em `ValidationError(a, b)` | Projeto `net8.0` com C# 12 | Passe uma coleção (`[a, b]`) ou use `<LangVersion>13</LangVersion>`/`latest` com SDK 9+ |
| `IL2026`/`IL3050` ao publicar com AOT | Uso de `JsonDefaults.Options` ou de `ToJson<T>(bool)`/`FromJson<T>()` | Gere um `JsonSerializerContext` e use as sobrecargas com contexto |
| `CultureNotFoundException` | Código da aplicação chamando `CultureInfo.GetCultureInfo("pt-BR")` sem ICU | Use `BrazilianCulture.Instance` |
| `ToListAsync` não encontrado | Projeto `net8.0` lendo CSV | Pacote `System.Linq.AsyncEnumerable` ou `await foreach` |

---

## 🛡️ Segurança

> [!WARNING]
> Evite `DateTime.Now`/`DateTime.Today` em servidores na nuvem: entre 21h e 0h de Brasília a data do servidor (UTC)
> já é a do dia seguinte. Use `BrazilTimeZone.GetToday(timeProvider)`.

> [!NOTE]
> O fallback de fuso UTC−03:00 é correto enquanto o Brasil não voltar a ter horário de verão. Se isso mudar, instale
> `tzdata` na imagem para usar as regras oficiais.

---

## ❓ Perguntas frequentes

<details>
<summary>O comportamento é o mesmo em .NET 8 e .NET 10?</summary>

Sim. Onde o .NET 10 tem uma API nova, o alvo `net8.0` usa um equivalente interno, e os testes comparam os dois caminhos.

</details>

<details>
<summary>Preciso fazer algo para usar Native AOT?</summary>

Só no JSON: use `JsonSerializerContext` com `JsonDefaults.CreateOptions(IJsonTypeInfoResolver?)` e registre os enums com `JsonDefaults.CreateEnumConverter<TEnum>()`. O resto já é compatível.

</details>

<details>
<summary>O vencimento sai um dia adiantado à noite</summary>

**Causa:** uso de `DateTime.Today`/`DateTime.Now` em servidor UTC.
**Solução:** use `BrazilTimeZone.Today` (ou `GetToday(timeProvider)`).

</details>

---

⬅️ [Anterior: Injeção de dependência](injecao-dependencia.md) · [📚 Índice](README.md) · [Próximo: Concorrência](concorrencia.md) ➡️
