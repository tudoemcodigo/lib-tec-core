[🏠 TEC.Core](../README.md) › [📚 Documentação](README.md) › Datas e dias úteis

# 📅 Datas e dias úteis

> Formata e interpreta datas no padrão brasileiro, calcula vencimentos e prazos em dias úteis com feriados nacionais, estaduais e municipais e informa "agora" e "hoje" no horário de Brasília, mesmo com o servidor em UTC.

## 📑 Sumário

- [🎯 Visão geral](#-visão-geral)
- [🚀 Uso](#-uso)
  - [DateFormatter](#dateformatter) · [DateExtensions](#dateextensions) · [BrazilTimeZone](#braziltimezone)
  - [BusinessDayCalculator](#businessdaycalculator) · [IBusinessDayCalculator](#ibusinessdaycalculator) · [IBusinessDayCalculatorFactory](#ibusinessdaycalculatorfactory) · [BusinessDayCalculatorFactory](#businessdaycalculatorfactory)
  - [Feriados](#feriados): [Holiday](#holiday) · [HolidayScope](#holidayscope) · [HolidayLocation](#holidaylocation) · [IHolidayProvider](#iholidayprovider) · [BrazilianNationalHolidays](#braziliannationalholidays) · [InMemoryHolidayProvider](#inmemoryholidayprovider) · [HolidayCalendar](#holidaycalendar) · [HolidayCalendarBuilder](#holidaycalendarbuilder)
  - [Fontes de feriados](#fontes-de-feriados): [IHolidaySource](#iholidaysource) · [CsvHolidaySource](#csvholidaysource) · [JsonHolidaySource](#jsonholidaysource) · [DbHolidaySource](#dbholidaysource) · [HttpHolidaySource](#httpholidaysource) · [DelegateHolidaySource](#delegateholidaysource) · [HolidaySourceFailure](#holidaysourcefailure) · [HolidaySourceException](#holidaysourceexception)
- [⚙️ Opções](#️-opções)
- [❌ Erros](#-erros)
- [🛡️ Segurança](#️-segurança)
- [❓ Perguntas frequentes](#-perguntas-frequentes)

---

## 🎯 Visão geral

| Preciso de... | Use | Namespace |
|---|---|---|
| Formatar ou interpretar datas em português, tempo relativo | `DateFormatter` | `TEC.Core.Dates.Formatting` |
| Início/fim de mês, semana, trimestre, idade | `DateExtensions` | `TEC.Core.Dates.Extensions` |
| "Agora" e "hoje" em Brasília, conversão UTC ↔ Brasília | `BrazilTimeZone` | `TEC.Core.Dates.TimeZones` |
| Dias úteis com uma única lista de feriados | `BusinessDayCalculator` + `IHolidayProvider` | `TEC.Core.Dates.BusinessDays` / `TEC.Core.Dates.Holidays` |
| Dias úteis por cidade ou UF | `HolidayCalendar` + `IBusinessDayCalculatorFactory` | `TEC.Core.Dates.Holidays` / `TEC.Core.Dates.BusinessDays` |
| Carregar feriados de CSV, JSON, banco ou API | Fontes `*HolidaySource` | `TEC.Core.Dates.Holidays.Sources` |

Os feriados nacionais podem ser **calculados** (sem arquivo); os estaduais e municipais vêm de **fontes**, lidas **uma única vez, na inicialização**, para um calendário em memória que atende **várias localidades**:

```mermaid
flowchart LR
    N["BrazilianNationalHolidays<br/>(calculados)"] --> B
    CSV["CsvHolidaySource"] --> B
    JSON["JsonHolidaySource"] --> B
    DB["DbHolidaySource"] --> B
    HTTP["HttpHolidaySource<br/>(BrasilAPI)"] --> B
    D["DelegateHolidaySource"] --> B
    B["HolidayCalendarBuilder<br/>BuildAsync()"] --> C["HolidayCalendar<br/>(imutável, em memória)"]
    C -->|"GetProvider(local)"| P["IHolidayProvider"]
    C --> F["BusinessDayCalculatorFactory"]
    F -->|"For(local)"| K["IBusinessDayCalculator"]
    P --> K2["BusinessDayCalculator"]
```

> [!NOTE]
> Os exemplos usam `var dt = new DateTime(2026, 9, 30, 14, 35, 12);` e `var d = new DateOnly(2026, 9, 30);` (uma quarta-feira). Nada aqui depende de ICU: a formatação usa `BrazilianCulture.Instance` (veja [Common](common.md#brazilianculture)).

---

## 🚀 Uso

### DateFormatter

> `TEC.Core.Dates.Formatting` · `static class`

Formatação em português, tempo relativo e interpretação de datas digitadas. Métodos de extensão para `DateTime` e `DateOnly`.

| Membro | Retorno | Descrição |
|---|---|---|
| `BrazilianDateFormat` (const) | `string` | `"dd/MM/yyyy"` |
| `BrazilianDateTimeFormat` (const) | `string` | `"dd/MM/yyyy HH:mm:ss"` |
| `IsoDateFormat` (const) | `string` | `"yyyy-MM-dd"` |
| `ToBrazilianDate(this DateTime)` / `ToBrazilianDate(this DateOnly)` | `string` | `"30/09/2026"` |
| `ToBrazilianDateTime(this DateTime date, bool includeSeconds = false)` | `string` | `"30/09/2026 14:35"` / `"30/09/2026 14:35:12"` |
| `ToIso8601(this DateTime)` | `string` | Formato round-trip `"O"`: `"2026-09-30T14:35:12.0000000Z"` em UTC; sem sufixo de fuso quando `Kind = Unspecified`. |
| `ToIso8601(this DateOnly)` | `string` | `"2026-09-30"` |
| `ToLongDate(this DateTime)` / `ToLongDate(this DateOnly)` | `string` | `"30 de setembro de 2026"` |
| `ToFullDate(this DateTime)` / `ToFullDate(this DateOnly)` | `string` | `"quarta-feira, 30 de setembro de 2026"` |
| `ToMonthYear(this DateTime)` / `ToMonthYear(this DateOnly)` | `string` | `"setembro/2026"` |
| `GetMonthName(int month, bool capitalize = false)` | `string` | `9` → `"setembro"` / `"Setembro"` |
| `GetDayOfWeekName(DayOfWeek dayOfWeek, bool capitalize = false)` | `string` | `Wednesday` → `"quarta-feira"` / `"Quarta-feira"` |
| `ToRelativeTime(this DateTime date)` | `string` | Tempo relativo em português, comparado com "agora" do relógio do sistema no mesmo `Kind` da data (`UtcNow` para UTC; horário local do servidor para `Local`/`Unspecified`). |
| `ToRelativeTime(this DateTime date, DateTime reference)` | `string` | Igual, em relação a `reference` (mesmo fuso que `date`: ambas UTC ou ambas locais). |
| `ToRelativeTime(this DateTime date, TimeProvider timeProvider)` | `string` | Igual, com "agora" vindo do relógio **injetado** (testável). |
| `TryParseDate(string? value, out DateOnly date)` | `bool` | Aceita `dd/MM/yyyy`, `d/M/yyyy`, `dd-MM-yyyy`, `dd.MM.yyyy`, `ddMMyyyy`, `yyyy-MM-dd`, `yyyyMMdd`. Espaços nas pontas são ignorados. Nunca lança. |
| `TryParseDateTime(string? value, out DateTime dateTime)` | `bool` | Aceita `dd/MM/yyyy HH:mm[:ss]`, `d/M/yyyy H:mm[:ss]`, `yyyy-MM-ddTHH:mm[:ss]`, `yyyy-MM-dd HH:mm:ss` **ou só a data** (qualquer formato de `TryParseDate`, hora 00:00). Nunca lança. |

```csharp
using TEC.Core.Dates.Extensions;
using TEC.Core.Dates.Formatting;

var reference = new DateOnly(2026, 9, 30);
var start = reference.StartOfMonth();                                    // 01/09/2026
var endExclusive = start.AddMonths(1);                                   // filtro: >= start e < endExclusive

string title = $"Vendas de {reference.ToMonthYear()}";                   // "Vendas de setembro/2026"
string period = $"{start.ToBrazilianDate()} a {reference.EndOfMonth().ToBrazilianDate()}";   // "01/09/2026 a 30/09/2026"
string full = reference.ToFullDate();                                    // "quarta-feira, 30 de setembro de 2026"

var createdAt = new DateTime(2026, 9, 30, 11, 0, 0);
string when = createdAt.ToRelativeTime(new DateTime(2026, 9, 30, 14, 0, 0));   // "há 3 horas"

if (DateFormatter.TryParseDate("30/09/2026", out var date)) { /* 2026-09-30 */ }
```

**Tempo relativo**

| Diferença | Saída |
|---|---|
| menos de 60 s | `"agora"` |
| 5 minutos no passado | `"há 5 minutos"` |
| 2 dias no futuro | `"em 2 dias"` |
| 45 dias no passado | `"há 1 mês"` |
| 400 dias no passado | `"há 1 ano"` |

Faixas: minutos (< 60 min), horas (< 24 h), dias (< 30 d), meses (< 365 d, 30 dias cada, no máximo 11) e anos (365 dias cada).

**Interpretação**

| Chamada | Resultado |
|---|---|
| `TryParseDate("30/09/2026", out var x)` | `true`, `2026-09-30` |
| `TryParseDate("20260930", out var x)` | `true`, `2026-09-30` |
| `TryParseDate("31/02/2026", out var x)` | `false` |
| `TryParseDateTime("30/09/2026 14:35", out var x)` | `true`, `2026-09-30T14:35:00` |

#### Relógio injetável (testes)

As sobrecargas com `TimeProvider` (`ToRelativeTime`, [`CalculateAge`](#dateextensions), [`BrazilTimeZone.GetNow`/`GetToday`](#braziltimezone)) deixam o código testável: em produção injete `TimeProvider.System`; nos testes, um `FakeTimeProvider` (pacote `Microsoft.Extensions.TimeProvider.Testing`).

```csharp
using Microsoft.Extensions.Time.Testing;
using TEC.Core.Dates.Formatting;

// Program.cs
builder.Services.AddSingleton(TimeProvider.System);

// Serviço
public sealed class CommentService(TimeProvider clock)
{
    public string Age(DateTime createdAtUtc) => createdAtUtc.ToRelativeTime(clock);   // "há 5 minutos"
}

// Teste: o relógio é controlado pelo teste
var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 15, 0, 0, TimeSpan.Zero));
var service = new CommentService(clock);
string text = service.Age(new DateTime(2026, 9, 30, 14, 55, 0, DateTimeKind.Utc));   // "há 5 minutos"
clock.Advance(TimeSpan.FromHours(2));
text = service.Age(new DateTime(2026, 9, 30, 14, 55, 0, DateTimeKind.Utc));          // "há 2 horas"
```

> [!NOTE]
> Para datas `Local`/`Unspecified`, "agora" é `timeProvider.GetLocalNow()` (fuso **do relógio/servidor**, não Brasília). Em servidores UTC, compare instantes UTC ou converta antes com [`BrazilTimeZone`](#braziltimezone).

---

### DateExtensions

> `TEC.Core.Dates.Extensions` · `static class`

Atalhos de calendário (métodos de extensão): início e fim de períodos, fim de semana, trimestre, idade.

| Membro | Retorno | Descrição |
|---|---|---|
| `ToDateOnly(this DateTime date)` | `DateOnly` | `dt` → `2026-09-30` |
| `ToDateTime(this DateOnly date, TimeOnly? time = null, DateTimeKind kind = DateTimeKind.Unspecified)` | `DateTime` | `d.ToDateTime(new TimeOnly(8, 0))` → `2026-09-30T08:00:00`. Sem `time`, 00:00. |
| `IsWeekend(this DateOnly)` / `IsWeekend(this DateTime)` | `bool` | `03/10/2026` (sábado) → `true` |
| `IsBetween(this DateOnly date, DateOnly start, DateOnly end)` | `bool` | Inclusivo. |
| `IsBetween(this DateTime date, DateTime start, DateTime end)` | `bool` | Inclusivo. |
| `StartOfMonth(this DateOnly)` | `DateOnly` | → `2026-09-01` |
| `StartOfMonth(this DateTime)` | `DateTime` | → `2026-09-01T00:00:00` (preserva o `Kind`) |
| `EndOfMonth(this DateOnly)` | `DateOnly` | `10/02/2028` → `2028-02-29` |
| `EndOfMonth(this DateTime)` | `DateTime` | `dt` → `2026-09-30T23:59:59.9999999` (preserva o `Kind`) |
| `StartOfYear(this DateOnly)` / `EndOfYear(this DateOnly)` | `DateOnly` | → `2026-01-01` / `2026-12-31` |
| `StartOfDay(this DateTime)` / `EndOfDay(this DateTime)` | `DateTime` | → `00:00:00` / `23:59:59.9999999` |
| `StartOfWeek(this DateOnly date, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)` | `DateOnly` | → `2026-09-27` (domingo); com `Monday` → `2026-09-28` |
| `EndOfWeek(this DateOnly date, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)` | `DateOnly` | → `2026-10-03` |
| `DaysInMonth(this DateOnly)` | `int` | fevereiro de 2028 → `29` |
| `Quarter(this DateOnly)` | `int` | → `3` |
| `Semester(this DateOnly)` | `int` | → `2` |
| `CalculateAge(this DateOnly birthDate, DateOnly? referenceDate = null)` | `int` | `15/10/1990` em `30/09/2026` → `35`. Sem `referenceDate`, usa `BrazilTimeZone.Today`. |
| `CalculateAge(this DateOnly birthDate, TimeProvider timeProvider)` | `int` | Idade na data atual **de Brasília** segundo o relógio injetado (testável). |
| `CalculateAge(this DateTime birthDate, DateTime? referenceDate = null)` | `int` | Mesma regra, usando a parte de data. |

- Em `CalculateAge`, quem nasceu em 29/02 completa anos em 28/02 nos anos não bissextos. Sem referência, a data atual é a **de Brasília**, independente do fuso do servidor.
- `EndOfMonth` e `EndOfDay` funcionam até em `DateTime.MaxValue` (sem estouro).

```csharp
using TEC.Core.Dates.Extensions;
using TEC.Core.Dates.TimeZones;

// Filtro de relatório mensal: "hoje" de Brasília (DateTime.Today é a data do servidor, geralmente UTC)
var start = BrazilTimeZone.Today.StartOfMonth().ToDateTime(TimeOnly.MinValue);
var nextMonth = start.AddMonths(1);
var orders = db.Orders.Where(o => o.Date >= start && o.Date < nextMonth);   // intervalo semiaberto

int quarter = new DateOnly(2026, 9, 30).Quarter();                          // 3
int age = new DateOnly(1990, 10, 15).CalculateAge(new DateOnly(2026, 9, 30));   // 35
int ageNow = new DateOnly(1990, 10, 15).CalculateAge(timeProvider);         // relógio injetado
```

> [!WARNING]
> `EndOfMonth()`/`EndOfDay()` em `DateTime` terminam em `23:59:59.9999999` (precisão de 100 ns). Colunas SQL Server `datetime` têm precisão de ~3 ms e **arredondam** esse valor para `00:00:00.000` do **dia seguinte**, incluindo no filtro registros do dia 1º do mês seguinte (`datetime2` e `date` não têm esse problema). Para filtrar períodos, prefira o intervalo semiaberto `>= início` e `< início do próximo mês`.

---

### BrazilTimeZone

> `TEC.Core.Dates.TimeZones` · `static class`

Útil quando o servidor roda em UTC (containers, nuvem) e as regras de negócio dependem do horário de Brasília.

| Membro | Retorno | Descrição |
|---|---|---|
| `TimeZoneId` (const) | `string` | `"America/Sao_Paulo"` (identificador IANA). |
| `Info` | `TimeZoneInfo` | Fuso de Brasília. Tenta o ID IANA, depois o do Windows (`E. South America Standard Time`) e, se o sistema não tiver base de fusos (container sem `tzdata`), usa **UTC−03:00 fixo**, já que o Brasil não adota horário de verão desde 2019. |
| `Now` | `DateTime` | Data e hora atuais em Brasília (relógio do sistema), com `Kind = Unspecified` (horário de parede). **Já está em Brasília**: não passe por `ToBrasiliaTime` (lança exceção, evitando o desconto duplo de 3 horas); para UTC use `FromBrasiliaTimeToUtc` ou `DateTime.UtcNow`. |
| `Today` | `DateOnly` | Data atual em Brasília (relógio do sistema). |
| `GetNow(TimeProvider timeProvider)` | `DateTime` | Como `Now`, mas segundo o relógio **injetado** (testável, ex.: `FakeTimeProvider`). |
| `GetToday(TimeProvider timeProvider)` | `DateOnly` | Como `Today`, segundo o relógio injetado. |
| `ToBrasiliaTime(this DateTime dateTime)` | `DateTime` | Converte um instante UTC (`Kind = Utc`) ou do fuso do servidor (`Kind = Local`) para Brasília (resultado `Unspecified`). ⚠️ `Kind = Unspecified` lança `ArgumentException`: o valor pode já estar em Brasília (ex.: `Now`, colunas de banco sem fuso) e tratá-lo como UTC descontaria 3 horas. |
| `ToBrasiliaTime(this DateTime dateTime, DateTimeKind unspecifiedKind)` | `DateTime` | Igual, declarando como interpretar `Unspecified`: `DateTimeKind.Utc` (ex.: colunas gravadas em UTC) ou `DateTimeKind.Local`. `Utc`/`Local` no próprio valor prevalecem. |
| `FromBrasiliaTimeToUtc(this DateTime brasiliaTime)` | `DateTime` | Converte um horário de Brasília para UTC (resultado com `Kind = Utc`). Veja as regras abaixo. |

Regras de `FromBrasiliaTimeToUtc`:

- **`Kind`**: `Unspecified` é o horário de parede de Brasília; `Utc` já está em UTC e é devolvido **sem alteração**; `Local` é um instante no fuso do servidor e é convertido com `ToUniversalTime()`.
- **Horário inexistente** (lacuna no início do antigo horário de verão, ex.: 04/11/2018 entre 00:00 e 00:59): não lança exceção; é interpretado com o deslocamento padrão −03:00, o que equivale a avançar 1 hora (00:30 → 03:30 UTC = 01:30 do horário de verão).
- **Horário ambíguo** (hora repetida no fim do horário de verão, ex.: 16/02/2019 entre 23:00 e 23:59): interpretado como horário padrão (−03:00), isto é, a **segunda** ocorrência.
- Sem base de fusos no sistema (fallback UTC−03:00 fixo), datas históricas de horário de verão ficam 1 hora diferentes; datas a partir de 2019 não são afetadas.

```csharp
using TEC.Core.Dates.TimeZones;

// Servidor em UTC: 30/09 02:30 UTC ainda é 29/09 em Brasília
DateTime local = new DateTime(2026, 9, 30, 2, 30, 0, DateTimeKind.Utc).ToBrasiliaTime();   // 29/09/2026 23:30
DateOnly businessDate = DateOnly.FromDateTime(local);                                      // 29/09/2026

// Horário de corte informado em Brasília, gravado em UTC
DateTime cutoffUtc = new DateTime(2026, 9, 30, 18, 0, 0).FromBrasiliaTimeToUtc();          // 30/09/2026 21:00 UTC

DateTime now = BrazilTimeZone.Now;     // Kind = Unspecified, horário de Brasília
DateOnly today = BrazilTimeZone.Today;

// Código testável: relógio injetado
public sealed class CutoffService(TimeProvider clock)
{
    public bool IsAfterCutoff() => BrazilTimeZone.GetNow(clock).Hour >= 18;
    public DateOnly Today() => BrazilTimeZone.GetToday(clock);
}
```

| Chamada | Resultado |
|---|---|
| `new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc).ToBrasiliaTime()` | `2026-09-30T12:00:00` |
| `new DateTime(2026, 9, 30, 15, 0, 0).ToBrasiliaTime()` | `ArgumentException` (`Unspecified` é ambíguo) |
| `new DateTime(2026, 9, 30, 15, 0, 0).ToBrasiliaTime(DateTimeKind.Utc)` | `2026-09-30T12:00:00` (lido do banco em UTC) |
| `BrazilTimeZone.Now.ToBrasiliaTime()` | `ArgumentException` (`Now` já está em Brasília) |
| `new DateTime(2026, 9, 30, 12, 0, 0).FromBrasiliaTimeToUtc()` | `2026-09-30T15:00:00Z` |
| `new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc).FromBrasiliaTimeToUtc()` | `2026-09-30T15:00:00Z` (já é UTC: devolvido sem deslocamento) |
| `new DateTime(2018, 11, 4, 0, 30, 0).FromBrasiliaTimeToUtc()` | `2018-11-04T03:30:00Z` (horário inexistente: −03:00) |
| `new DateTime(2019, 2, 16, 23, 30, 0).FromBrasiliaTimeToUtc()` | `2019-02-17T02:30:00Z` (horário ambíguo: −03:00) |

> [!WARNING]
> Evite `DateTime.Now` e `DateTime.Today` em servidores na nuvem: eles retornam o horário **do servidor** (geralmente UTC), e entre 21h e 0h de Brasília a data já é a do dia seguinte.

---

### BusinessDayCalculator

> `TEC.Core.Dates.BusinessDays` · `sealed class` (implementa `IBusinessDayCalculator`)

Cálculo de dias úteis considerando fins de semana e feriados. Sem estado mutável: seguro como Singleton.

- `nonWorkingDays`: padrão **sábado e domingo**. Pelo menos um dia da semana precisa ser útil. A lista é copiada e validada no construtor.
- Os métodos que recebem uma data (exceto `GetBusinessDays`) têm sobrecarga com `DateTime`, que **preserva o horário e o `DateTimeKind`**.
- `DateTime` com `Kind = Utc` representa um instante e é avaliado na **data de Brasília**: sábado 02:00 UTC é sexta-feira 23:00 em Brasília, portanto dia útil. O resultado preserva o horário de Brasília e volta em UTC (ex.: `NextBusinessDay` de segunda 02/11/2026 01:00 UTC, que é domingo 22:00 em Brasília, retorna 04/11/2026 01:00 UTC, isto é, terça 22:00 em Brasília). `Local` e `Unspecified` usam a data como está.

| Membro | Retorno | Descrição |
|---|---|---|
| `BusinessDayCalculator(IHolidayProvider holidayProvider, IEnumerable<DayOfWeek>? nonWorkingDays = null)` | — | Cria a calculadora. |
| `MaxBusinessDaysToAdd` (const) | `int` | `26_000`: limite de `AddBusinessDays` (±). |
| `MaxRangeDays` (const) | `int` | `36_600`: intervalo máximo, em dias corridos, de `CountBusinessDays`/`GetBusinessDays`. |
| `IsBusinessDay(DateOnly)` / `IsBusinessDay(DateTime)` | `bool` | Indica se é dia útil. |
| `NextBusinessDay(DateOnly)` / `(DateTime)` | `DateOnly` / `DateTime` | Próximo dia útil **estritamente posterior**. |
| `PreviousBusinessDay(DateOnly)` / `(DateTime)` | `DateOnly` / `DateTime` | Dia útil **estritamente anterior**. |
| `NextOrSameBusinessDay(DateOnly)` / `(DateTime)` | `DateOnly` / `DateTime` | A própria data, se for útil; senão, o próximo dia útil. Ideal para **vencimentos**. |
| `PreviousOrSameBusinessDay(DateOnly)` / `(DateTime)` | `DateOnly` / `DateTime` | A própria data, se for útil; senão, o dia útil anterior. |
| `AddBusinessDays(DateOnly date, int businessDays)` / `(DateTime, int)` | `DateOnly` / `DateTime` | Soma (ou subtrai, se negativo) dias úteis a partir da data (que não precisa ser útil). Com `0`, age como `NextOrSameBusinessDay`. Limite: ±`MaxBusinessDaysToAdd`. |
| `CountBusinessDays(DateOnly start, DateOnly end)` / `(DateTime, DateTime)` | `int` | Conta os dias úteis **incluindo as duas datas**. A ordem das datas não importa. Intervalo máximo: `MaxRangeDays`. |
| `GetBusinessDays(DateOnly start, DateOnly end)` | `IReadOnlyList<DateOnly>` | Dias úteis do período (inclusive), em ordem cronológica. A ordem das datas não importa. Intervalo máximo: `MaxRangeDays`. |
| `GetNthBusinessDayOfMonth(int year, int month, int n)` | `DateOnly` | N-ésimo dia útil do mês (ex.: 5º dia útil para salário). |
| `GetFirstBusinessDayOfMonth(int year, int month)` | `DateOnly` | Primeiro dia útil do mês. |
| `GetLastBusinessDayOfMonth(int year, int month)` | `DateOnly` | Último dia útil do mês. |
| `CountBusinessDaysInMonth(int year, int month)` | `int` | Quantidade de dias úteis do mês. |

```csharp
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;

// Feriados nacionais calculados para qualquer ano (fixos + Carnaval, Paixão e Corpus Christi), sem arquivo
var calculator = new BusinessDayCalculator(new BrazilianNationalHolidays());

calculator.NextOrSameBusinessDay(new DateOnly(2026, 11, 15));   // 16/11/2026 (15/11 é domingo e feriado)
calculator.AddBusinessDays(new DateOnly(2026, 10, 9), 5);       // 19/10/2026 (pula o fim de semana e 12/10)
calculator.GetNthBusinessDayOfMonth(2026, 11, 5);               // 09/11/2026: 5º dia útil de novembro

// Empresa que trabalha aos sábados
var saturdays = new BusinessDayCalculator(new BrazilianNationalHolidays(), nonWorkingDays: [DayOfWeek.Sunday]);
saturdays.IsBusinessDay(new DateOnly(2026, 10, 3));             // true (sábado)
```

Referência com os feriados 12/10 (seg), 02/11 (seg), 15/11 (dom), 20/11 (sex) e 25/12 (sex):

```text
       Outubro 2026                  Novembro 2026
 D  S  T  Q  Q  S  S           D  S  T  Q  Q  S  S
             1  2  3           1 [2] 3  4  5  6  7
 4  5  6  7  8  9 10           8  9 10 11 12 13 14
11[12]13 14 15 16 17         [15]16 17 18 19[20]21
18 19 20 21 22 23 24          22 23 24 25 26 27 28
25 26 27 28 29 30 31          29 30
```

| Chamada | Resultado | Explicação |
|---|---|---|
| `IsBusinessDay(12/10/2026)` | `false` | Feriado |
| `NextBusinessDay(09/10/2026)` | `13/10/2026` (ter) | Pula o sábado, o domingo e o feriado de 12/10 |
| `PreviousBusinessDay(13/10/2026)` | `09/10/2026` (sex) | |
| `NextOrSameBusinessDay(15/11/2026)` | `16/11/2026` (seg) | Vencimento no domingo/feriado passa para o próximo dia útil |
| `NextOrSameBusinessDay(30/09/2026)` | `30/09/2026` | Já é dia útil |
| `PreviousOrSameBusinessDay(15/11/2026)` | `13/11/2026` (sex) | |
| `AddBusinessDays(09/10/2026, 5)` | `19/10/2026` (seg) | D+5 úteis |
| `AddBusinessDays(13/10/2026, -1)` | `09/10/2026` (sex) | |
| `AddBusinessDays(10/10/2026, 0)` | `13/10/2026` (ter) | D+0 em sábado: próximo dia útil (12/10 é feriado) |
| `AddBusinessDays(new DateTime(2026,10,9,17,30,0), 1)` | `13/10/2026 17:30` | Mantém o horário |
| `CountBusinessDays(01/10/2026, 31/10/2026)` | `21` | |
| `GetBusinessDays(09/10, 14/10)` | `09/10, 13/10, 14/10` | |
| `GetNthBusinessDayOfMonth(2026, 11, 5)` | `09/11/2026` | 5º dia útil (pagamento de salário) |
| `GetFirstBusinessDayOfMonth(2026, 11)` | `03/11/2026` | 01/11 é domingo; 02/11 é feriado |
| `GetLastBusinessDayOfMonth(2026, 12)` | `31/12/2026` | |
| `CountBusinessDaysInMonth(2026, 11)` | `19` | |

---

### IBusinessDayCalculator

> `TEC.Core.Dates.BusinessDays` · `interface`

Contrato com **todos os métodos de instância** de [`BusinessDayCalculator`](#businessdaycalculator) (inclusive as sobrecargas `DateTime`), exceto o construtor e as constantes. Use a interface para injeção de dependência e testes.

| Membro | Descrição |
|---|---|
| `IsBusinessDay`, `NextBusinessDay`, `PreviousBusinessDay`, `NextOrSameBusinessDay`, `PreviousOrSameBusinessDay`, `AddBusinessDays`, `CountBusinessDays` | Sobrecargas `DateOnly` e `DateTime`. |
| `GetBusinessDays(DateOnly start, DateOnly end)` | Só `DateOnly`. |
| `GetNthBusinessDayOfMonth`, `GetFirstBusinessDayOfMonth`, `GetLastBusinessDayOfMonth`, `CountBusinessDaysInMonth` | Por ano e mês. |

```csharp
public sealed class BillingService(IBusinessDayCalculator businessDays)
{
    // "Hoje" de Brasília, não a data do servidor
    public DateOnly DueDate() => businessDays.NextOrSameBusinessDay(BrazilTimeZone.Today.AddDays(30));
    public DateOnly DueDate(DateOnly issueDate) => businessDays.AddBusinessDays(issueDate, 3);
}
```

---

### IBusinessDayCalculatorFactory

> `TEC.Core.Dates.BusinessDays` · `interface`

Calculadoras de dias úteis por localidade (ex.: filiais em cidades diferentes). Registrada por `AddBusinessDayCalculator(calendar, ...)`.

| Membro | Retorno | Descrição |
|---|---|---|
| `For(HolidayLocation location)` | `IBusinessDayCalculator` | Calculadora com os feriados nacionais, da UF e do município da localidade. |

```csharp
public class InvoiceService(IBusinessDayCalculatorFactory calendars)
{
    public DateOnly DueDate(DateOnly date, int ibgeCode) =>
        calendars.For(HolidayLocation.FromIbgeCode(ibgeCode)).NextOrSameBusinessDay(date);
}
```

---

### BusinessDayCalculatorFactory

> `TEC.Core.Dates.BusinessDays` · `sealed class` (implementa `IBusinessDayCalculatorFactory`)

Calculadoras por localidade a partir de um [`HolidayCalendar`](#holidaycalendar): use quando a mesma aplicação atende cidades diferentes (filiais, clientes, agências). Sem estado mutável: seguro como Singleton.

| Membro | Retorno | Descrição |
|---|---|---|
| `BusinessDayCalculatorFactory(HolidayCalendar calendar, IEnumerable<DayOfWeek>? nonWorkingDays = null)` | — | `nonWorkingDays` (padrão: sábado e domingo) é copiado e validado na criação, não na primeira chamada de `For`. |
| `For(HolidayLocation location)` | `IBusinessDayCalculator` | Calculadora com feriados nacionais + da UF + do município. Leve e sem cache: o número de localidades vindas de requisições não aumenta o consumo de memória. |

```csharp
var factory = new BusinessDayCalculatorFactory(calendar);
var rio = factory.For(HolidayLocation.FromIbgeCode(3304557));
rio.NextOrSameBusinessDay(new DateOnly(2026, 1, 20));   // 21/01/2026 (São Sebastião, feriado municipal)
```

---

### Feriados

Feriados nacionais, estaduais e municipais usados pela calculadora. Os tipos ficam em `TEC.Core.Dates.Holidays` e as fontes em `TEC.Core.Dates.Holidays.Sources`.

| Fonte | Classe | Observação |
|---|---|---|
| Cálculo | `BrazilianNationalHolidays` | Nacionais de qualquer ano; Carnaval e Corpus Christi desligáveis |
| CSV / JSON | `CsvHolidaySource` / `JsonHolidaySource` | Arquivo ou stream; colunas `Uf` e `CodigoIbge` opcionais |
| Banco de dados | `DbHolidaySource` | ADO.NET puro (`DbConnection` + SQL), qualquer provedor |
| API HTTP | `HttpHolidaySource` | Array JSON; pronta para a BrasilAPI |
| Função própria | `DelegateHolidaySource` | EF Core, Dapper, repositório… |
| Lista em memória | `AddHolidays` / `InMemoryHolidayProvider` | Testes e listas fixas |

**Exemplo completo: várias fontes e várias cidades**

```csharp
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.TimeZones;
using TEC.Core.DependencyInjection;

// Program.cs: fontes lidas uma vez, em paralelo, sem bloquear thread
var calendar = await HolidayCalendar.CreateBuilder()
    .AddBrazilianNational()                                          // nacionais calculados, qualquer ano
    .AddJsonFile("holidays/local.json")                              // estaduais e municipais
    .AddDatabase(() => new SqlConnection(connectionString),
        "SELECT Data, Descricao, Uf, CodigoIbge FROM Feriados WHERE Ativo = 1")
    .AddBrasilApi(httpClient, [2026, 2027], optional: true)          // falha → ignorada e registrada
    .OnSourceError(f => logger.LogWarning(f.Exception, "Fonte de feriados ignorada: {Source}", f.SourceName))
    .BuildAsync();                                                   // fonte obrigatória com erro → HolidaySourceException

builder.Services.AddBusinessDayCalculator(calendar,
    defaultLocation: new HolidayLocation("SP", 3550308),             // matriz: São Paulo (código IBGE)
    nonWorkingDays: [DayOfWeek.Sunday]);                             // empresa que trabalha aos sábados

// Serviço
public sealed class ChargeService(IBusinessDayCalculator headquarters, IBusinessDayCalculatorFactory calendars)
{
    public DateOnly HeadquartersDueDate() =>
        headquarters.NextOrSameBusinessDay(BrazilTimeZone.Today.AddDays(30));          // "hoje" de Brasília

    public DateOnly BranchDueDate(int ibgeCode) =>
        calendars.For(HolidayLocation.FromIbgeCode(ibgeCode)).NextOrSameBusinessDay(BrazilTimeZone.Today.AddDays(30));
}
```

```json
[
  { "data": "2026-01-25", "descricao": "Aniversário de São Paulo", "uf": "SP", "codigoIbge": 3550308 },
  { "data": "2026-07-09", "descricao": "Revolução Constitucionalista", "uf": "SP" }
]
```

Sem várias localidades, basta um provedor: `new BusinessDayCalculator(new BrazilianNationalHolidays())`. O registro em DI (`AddBusinessDayCalculator`, sobrecargas e o que é registrado) está em [Injeção de dependência](injecao-dependencia.md#addbusinessdaycalculator).

> [!TIP]
> A localidade é identificada por UF e código IBGE do município (`HolidayLocation`). Quando duas fontes têm feriado na mesma data, vale a registrada **primeiro** no builder.

#### Holiday

> `TEC.Core.Dates.Holidays` · `sealed class` (imutável)

Feriado ou dia não útil, nacional, estadual ou municipal. As propriedades só podem ser definidas na criação (construtor ou inicializador de objeto), então as instâncias devolvidas pelos provedores podem ser compartilhadas sem risco de alteração. As propriedades têm `[CsvColumn]` (`Data`, `Descricao`, `Uf`, `CodigoIbge`) para leitura direta de CSV; `Scope` tem `[CsvIgnore]`.

| Membro | Retorno | Descrição |
|---|---|---|
| `Holiday()` | — | Para inicializador de objeto e leitura de CSV (`Description` começa vazia). |
| `Holiday(DateOnly date, string description)` | — | Feriado nacional. |
| `Holiday(DateOnly date, string description, HolidayLocation location)` | — | Feriado restrito à localidade (nacional com `HolidayLocation.National`). |
| `Date` | `DateOnly` | Data. |
| `Description` | `string` | Descrição (nunca nula). |
| `Uf` | `string?` | Sigla da UF, normalizada para maiúsculas (`null` nos nacionais). Em feriados municipais sem UF, é deduzida do código IBGE. |
| `IbgeCode` | `int?` | Código IBGE do município (7 dígitos, começando pelo código da UF). |
| `Scope` | `HolidayScope` | `Municipal` (com `IbgeCode`), `State` (só `Uf`) ou `National`. |
| `AppliesTo(HolidayLocation location)` | `bool` | Nacional: sempre. Estadual: mesma UF. Municipal: mesmo município. |
| `ToString()` | `string` | `"25/01/2026 - Aniversário de São Paulo (SP/3550308)"`; estadual `"... (SP)"`; nacional sem sufixo. |

```csharp
var national  = new Holiday(new DateOnly(2026, 1, 1), "Confraternização Universal");
var state     = new Holiday(new DateOnly(2026, 7, 9), "Revolução Constitucionalista", new HolidayLocation("SP"));
var municipal = new Holiday(new DateOnly(2026, 1, 20), "São Sebastião") { IbgeCode = 3304557 };   // UF deduzida: RJ

municipal.AppliesTo(new HolidayLocation("RJ", 3304557));   // true
municipal.AppliesTo(new HolidayLocation("RJ"));            // false (só o município)
```

#### HolidayScope

> `TEC.Core.Dates.Holidays` · `enum`

Abrangência de um feriado, deduzida de `Holiday.Uf` e `Holiday.IbgeCode`.

| Membro | Valor | Descrição |
|---|:-:|---|
| `National` | 0 | Vale em todo o país (sem UF e sem código IBGE). |
| `State` | 1 | Vale em uma UF (somente `Uf`). |
| `Municipal` | 2 | Vale em um município (`IbgeCode` informado). |

```csharp
new Holiday(new DateOnly(2026, 7, 9), "Revolução Constitucionalista") { Uf = "SP" }.Scope;   // HolidayScope.State
```

#### HolidayLocation

> `TEC.Core.Dates.Holidays` · `sealed record`

Localidade usada para escolher os feriados aplicáveis: nacionais, da UF e do município (pelo código IBGE).

| Membro | Retorno | Descrição |
|---|---|---|
| `HolidayLocation(string uf)` | — | Localidade estadual: nacionais + da UF. A sigla é normalizada para maiúsculas. |
| `HolidayLocation(string uf, int ibgeCode)` | — | Localidade municipal: nacionais + da UF + do município. Valida que o município é da UF. |
| `FromIbgeCode(int ibgeCode)` (static) | `HolidayLocation` | Localidade municipal com a UF deduzida do código (`3304557` → `RJ`). |
| `National` (static) | `HolidayLocation` | Somente feriados nacionais. |
| `Uf` | `string?` | Sigla da UF (`null` em `National`). |
| `IbgeCode` | `int?` | Código IBGE do município (`null` se nacional ou estadual). |
| `ToString()` | `string` | `"Nacional"`, `"SP"` ou `"SP/3550308"`. |

```csharp
var saoPaulo = new HolidayLocation("SP", 3550308);    // nacionais + estaduais de SP + municipais da capital
var parana   = new HolidayLocation("PR");             // nacionais + estaduais do PR
var rio      = HolidayLocation.FromIbgeCode(3304557); // UF deduzida do código (RJ)
```

#### IHolidayProvider

> `TEC.Core.Dates.Holidays` · `interface`

Fonte de feriados consultada pela calculadora de dias úteis.

| Membro | Retorno | Descrição |
|---|---|---|
| `IsHoliday(DateOnly date)` | `bool` | Indica se a data é feriado. |
| `GetHoliday(DateOnly date)` | `Holiday?` | Feriado da data, ou `null`. |
| `GetHolidays(int year)` | `IReadOnlyList<Holiday>` | Feriados do ano, em ordem cronológica. |
| `GetHolidays(DateOnly start, DateOnly end)` | `IReadOnlyList<Holiday>` | Feriados do período (inclusive), em ordem cronológica. As implementações do TEC.Core aceitam o período em qualquer ordem. |

```csharp
IHolidayProvider holidays = calendar.GetProvider(new HolidayLocation("SP", 3550308));
foreach (var holiday in holidays.GetHolidays(2026))
    Console.WriteLine(holiday);   // 01/01/2026 - Confraternização Universal ...
```

> [!IMPORTANT]
> Implementações próprias são usadas por uma calculadora Singleton: precisam ser **thread-safe**.

#### BrazilianNationalHolidays

> `TEC.Core.Dates.Holidays` · `sealed class` (implementa `IHolidayProvider`)

Feriados nacionais **calculados para qualquer ano**, sem arquivo nem banco, seguindo o calendário de dias não úteis do mercado financeiro (ANBIMA/FEBRABAN). Imutável e thread-safe (anos de 1900 a 2200 ficam em cache).

| Feriado | Data | Observação |
|---|---|---|
| Confraternização Universal | 01/01 | |
| Carnaval | Páscoa − 48 (seg) e − 47 (ter) | Desligável (`includeCarnival: false`) |
| Paixão de Cristo | Páscoa − 2 | |
| Tiradentes | 21/04 | Prevalece se coincidir com a Paixão (ex.: 2000) |
| Dia do Trabalho | 01/05 | |
| Corpus Christi | Páscoa + 60 | Desligável (`includeCorpusChristi: false`) |
| Independência do Brasil | 07/09 | |
| Nossa Senhora Aparecida | 12/10 | Desde 1980 (Lei 6.802/1980) |
| Finados | 02/11 | |
| Proclamação da República | 15/11 | |
| Dia Nacional de Zumbi e da Consciência Negra | 20/11 | Desde 2024 (Lei 14.759/2023) |
| Natal | 25/12 | |

| Membro | Retorno | Descrição |
|---|---|---|
| `BrazilianNationalHolidays(bool includeCarnival = true, bool includeCorpusChristi = true)` | — | Carnaval e Corpus Christi são pontos facultativos federais: desligue-os se a empresa trabalha nesses dias. |
| `IncludeCarnival` | `bool` | Se a segunda e a terça-feira de Carnaval são feriados. |
| `IncludeCorpusChristi` | `bool` | Se Corpus Christi é feriado. |
| `GetEaster(int year)` (static) | `DateOnly` | Domingo de Páscoa (gregoriano, algoritmo de Meeus/Jones/Butcher). `GetEaster(2026)` → `05/04/2026`. |
| `IsHoliday`, `GetHoliday`, `GetHolidays(int)`, `GetHolidays(DateOnly, DateOnly)` | — | Implementação de `IHolidayProvider`. As listas devolvidas são cópias. |

```csharp
// Sem fonte nenhuma: só nacionais
var calculator = new BusinessDayCalculator(new BrazilianNationalHolidays());
calculator.NextOrSameBusinessDay(new DateOnly(2026, 2, 14));   // 18/02/2026 (sábado → pula domingo e Carnaval)
```

> [!NOTE]
> Quarta-feira de Cinzas (meio expediente) não entra. Além das vigências de 12/10 e 20/11, as regras atuais valem para todos os anos: para datas históricas, cadastre os feriados em uma fonte.

#### InMemoryHolidayProvider

> `TEC.Core.Dates.Holidays` · `sealed class` (implementa `IHolidayProvider`)

Provedor em memória simples, **imutável e thread-safe**. **Não filtra por localidade**: todos os feriados informados valem. Datas duplicadas mantêm a primeira ocorrência. Para várias localidades, use [`HolidayCalendar`](#holidaycalendar).

| Membro | Retorno | Descrição |
|---|---|---|
| `InMemoryHolidayProvider(IEnumerable<Holiday> holidays)` | — | Cria a partir de uma lista (copiada). |
| `Count` | `int` | Quantidade de feriados carregados (sem duplicados). |
| `IsHoliday`, `GetHoliday`, `GetHolidays(int)`, `GetHolidays(DateOnly, DateOnly)` | — | Implementação de `IHolidayProvider`. O período pode vir em qualquer ordem. |

```csharp
var holidays = new InMemoryHolidayProvider([
    new Holiday(new DateOnly(2026, 10, 12), "Nossa Senhora Aparecida"),
    new Holiday(new DateOnly(2026, 11, 2), "Finados"),
]);
var calculator = new BusinessDayCalculator(holidays);
```

> [!NOTE]
> A classe é `sealed`: para outra lógica, implemente [`IHolidayProvider`](#iholidayprovider) (ou uma [fonte](#iholidaysource) para o `HolidayCalendar`).

#### HolidayCalendar

> `TEC.Core.Dates.Holidays` · `sealed class` (imutável, thread-safe)

Calendário de feriados de várias localidades, carregado de uma ou mais fontes por [`HolidayCalendarBuilder`](#holidaycalendarbuilder). Todos os feriados ficam em um único índice em memória; `GetProvider` devolve uma visão com os aplicáveis à localidade. Registre como Singleton (`AddBusinessDayCalculator(calendar, ...)`).

| Membro | Retorno | Descrição |
|---|---|---|
| `CreateBuilder()` (static) | `HolidayCalendarBuilder` | Início da configuração (o construtor do builder não é público). |
| `GetProvider(HolidayLocation location)` | `IHolidayProvider` | Feriados aplicáveis à localidade (nacionais + da UF + do município). Leve (não copia dados): pode ser criado por requisição. `IsHoliday`/`GetHoliday` consultam um índice por data; `GetHolidays` faz busca binária no índice ordenado e percorre só o período pedido. |
| `National` | `IHolidayProvider` | Somente nacionais (equivale a `GetProvider(HolidayLocation.National)`). |
| `Count` | `int` | Feriados carregados das fontes (os calculados não entram). |
| `IncludesBrazilianNational` | `bool` | Se os nacionais calculados fazem parte do calendário. |
| `Failures` | `IReadOnlyList<HolidaySourceFailure>` | Fontes opcionais que falharam e foram ignoradas (útil em health checks). |

**Regras da carga:**

- **Obrigatória × opcional:** uma fonte obrigatória (padrão) que falha interrompe a carga com `HolidaySourceException` (a exceção original fica em `InnerException`). Uma fonte com `optional: true` que falha é ignorada: vai para `Failures` e para o callback de `OnSourceError`.
- **Prioridade:** quando há feriados na mesma data para a localidade, vale o da fonte registrada **primeiro** no builder (inclusive `AddBrazilianNational`). Só a descrição muda: a data continua não útil.
- **Cancelamento:** o `CancellationToken` de `BuildAsync` é repassado às fontes, e o cancelamento é propagado mesmo em fontes opcionais.
- **Sem recarga:** os dados são lidos só em `BuildAsync`. Para atualizar, reinicie a aplicação.

```csharp
var calendar = await HolidayCalendar.CreateBuilder()
    .AddBrazilianNational()
    .AddJsonFile("holidays/local.json")
    .BuildAsync();

var saoPauloHolidays = calendar.GetProvider(new HolidayLocation("SP", 3550308));
if (calendar.Failures.Count > 0) { /* health check degradado */ }
```

#### HolidayCalendarBuilder

> `TEC.Core.Dates.Holidays` · `sealed class`

Configura as fontes e carrega o `HolidayCalendar`, uma única vez, na inicialização. Obtido por `HolidayCalendar.CreateBuilder()`. `BuildAsync` lê todas as fontes **em paralelo**; uma fonte que falha não interrompe a leitura das demais.

| Membro | Retorno | Descrição |
|---|---|---|
| `MaxHolidaysPerSource` (const) | `int` | `1_000_000` feriados por fonte. |
| `AddBrazilianNational(bool includeCarnival = true, bool includeCorpusChristi = true)` | `HolidayCalendarBuilder` | Nacionais calculados. Só pode ser chamado uma vez. |
| `AddHolidays(IEnumerable<Holiday> holidays, string name = "Memória")` | `HolidayCalendarBuilder` | Lista em memória (copiada na chamada; sempre obrigatória). |
| `AddCsvFile(string filePath, CsvOptions? options = null, bool optional = false)` | `HolidayCalendarBuilder` | Arquivo CSV ([`CsvHolidaySource`](#csvholidaysource)). |
| `AddJsonFile(string filePath, Func<JsonElement, Holiday?>? mapper = null, bool optional = false)` | `HolidayCalendarBuilder` | Arquivo JSON ([`JsonHolidaySource`](#jsonholidaysource)). |
| `AddDatabase(Func<DbConnection> connectionFactory, string commandText, Action<DbCommand>? configureCommand = null, bool optional = false)` | `HolidayCalendarBuilder` | Consulta ADO.NET ([`DbHolidaySource`](#dbholidaysource), nome padrão). |
| `AddDelegate(Func<CancellationToken, Task<IEnumerable<Holiday>>> load, string name, bool optional = false)` | `HolidayCalendarBuilder` | Função própria ([`DelegateHolidaySource`](#delegateholidaysource)). |
| `AddHttp(HttpClient httpClient, Uri requestUri, Func<JsonElement, Holiday?>? mapper = null, bool optional = false)` | `HolidayCalendarBuilder` | API que devolve um array JSON ([`HttpHolidaySource`](#httpholidaysource)). |
| `AddBrasilApi(HttpClient httpClient, IEnumerable<int> years, bool optional = false)` | `HolidayCalendarBuilder` | BrasilAPI, uma fonte por ano distinto (1900 a 2199). |
| `AddSource(IHolidaySource source, bool optional = false)` | `HolidayCalendarBuilder` | Qualquer fonte, inclusive implementações próprias. |
| `OnSourceError(Action<HolidaySourceFailure> handler)` | `HolidayCalendarBuilder` | Callback para cada fonte **opcional** que falhar (ex.: log). Uma nova chamada substitui o callback anterior. |
| `BuildAsync(CancellationToken cancellationToken = default)` | `Task<HolidayCalendar>` | Carrega as fontes e cria o calendário. |

```csharp
var calendar = await HolidayCalendar.CreateBuilder()
    .AddBrazilianNational(includeCarnival: false)
    .AddCsvFile("holidays/municipal.csv", optional: true)
    .AddSource(new InternalHolidaySource(), optional: true)   // fonte própria (veja IHolidaySource)
    .BuildAsync(cancellationToken);
```

---

### Fontes de feriados

#### IHolidaySource

> `TEC.Core.Dates.Holidays.Sources` · `interface`

Origem dos feriados (arquivo, banco, API…), lida uma única vez por `HolidayCalendarBuilder.BuildAsync`. Implemente para fontes que não estejam prontas no TEC.Core.

| Membro | Retorno | Descrição |
|---|---|---|
| `Name` | `string` | Nome usado em mensagens de erro e em `HolidaySourceFailure`. Não deve conter segredos. |
| `LoadAsync(CancellationToken cancellationToken = default)` | `IAsyncEnumerable<Holiday>` | Lê os feriados da fonte. |

```csharp
using System.Runtime.CompilerServices;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;

public sealed class InternalHolidaySource : IHolidaySource
{
    public string Name => "Feriados internos";

    public async IAsyncEnumerable<Holiday> LoadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new Holiday(new DateOnly(2026, 12, 24), "Véspera de Natal (ponto facultativo interno)");
    }
}
```

Qualquer exceção lançada em `LoadAsync` vira `HolidaySourceException` (fonte obrigatória) ou `HolidaySourceFailure` (fonte opcional). Item nulo é recusado com `InvalidOperationException`.

#### CsvHolidaySource

> `TEC.Core.Dates.Holidays.Sources` · `sealed class` (implementa `IHolidaySource`)

Feriados em CSV (arquivo ou stream), lidos com o [`CsvReader`](csv.md) do TEC.Core.

| Membro | Retorno | Descrição |
|---|---|---|
| `CsvHolidaySource(string filePath, CsvOptions? options = null)` | — | Lê de um arquivo. `Name` = caminho. |
| `CsvHolidaySource(Func<Stream> openStream, string name, CsvOptions? options = null)` | — | Stream aberto a cada carga (ex.: recurso embutido) e descartado ao final. |
| `Name` | `string` | Nome da fonte. |
| `LoadAsync(CancellationToken cancellationToken = default)` | `IAsyncEnumerable<Holiday>` | Lê os feriados. |

**Layout:** padrão de `CsvOptions.Default` (separador `;`, cabeçalho obrigatório, cultura pt-BR). Os nomes de coluna não diferenciam maiúsculas nem acentos (`Descricao` ou `Descrição`). A data aceita `dd/MM/yyyy` ou ISO. `Uf` e `CodigoIbge` são opcionais: sem eles, o feriado é nacional. Com o código IBGE, a UF pode ficar vazia.

```csv
Data;Descrição;Uf;CodigoIbge
09/07/2026;Revolução Constitucionalista;SP;
2026-01-25;Aniversário de São Paulo;SP;3550308
20/01/2026;São Sebastião;;3304557
```

```csharp
var calendar = await HolidayCalendar.CreateBuilder()
    .AddCsvFile("holidays/municipal.csv")
    .BuildAsync();
```

#### JsonHolidaySource

> `TEC.Core.Dates.Holidays.Sources` · `sealed class` (implementa `IHolidaySource`)

Feriados em JSON (arquivo ou stream): um array de objetos. A leitura usa `JsonDocument` e não depende de serialização por reflexão, então é **compatível com Native AOT**.

| Membro | Retorno | Descrição |
|---|---|---|
| `MaxJsonBytes` (const) | `int` | `64 * 1024 * 1024` (64 MB). Profundidade máxima: 16. |
| `JsonHolidaySource(string filePath, Func<JsonElement, Holiday?>? mapper = null)` | — | Lê de um arquivo. `Name` = caminho. |
| `JsonHolidaySource(Func<Stream> openStream, string name, Func<JsonElement, Holiday?>? mapper = null)` | — | Stream aberto a cada carga e descartado ao final. |
| `Name` | `string` | Nome da fonte. |
| `LoadAsync(CancellationToken cancellationToken = default)` | `IAsyncEnumerable<Holiday>` | Lê os feriados. |
| `MapDefault(JsonElement element)` (static) | `Holiday?` | Converte um objeto no formato padrão. Use dentro de um `mapper` próprio. |

**Formato padrão:**

- Nomes de campo sem diferenciar maiúsculas, acentos, `_`, `-` e espaços. Data: `data` ou `date` (texto em `aaaa-MM-dd` ou `dd/MM/aaaa`). Descrição: `descricao`, `description`, `nome` ou `name`. UF: `uf`. Município: `codigoIbge`, `codIbge`, `ibge` ou `ibgeCode` (número ou texto). O formato da BrasilAPI é lido sem mapper.
- `uf` e `codigoIbge` são opcionais (`null` aceito). Campos desconhecidos são ignorados.
- Para outro formato, informe um `mapper`: ele pode chamar `JsonHolidaySource.MapDefault(element)` e devolver `null` para ignorar o item.

```json
[
  { "data": "2026-01-25", "descricao": "Aniversário de São Paulo", "uf": "SP", "codigoIbge": 3550308 },
  { "data": "09/07/2026", "descricao": "Revolução Constitucionalista", "uf": "SP" }
]
```

```csharp
// Ignora pontos facultativos de um JSON de terceiros
var calendar = await HolidayCalendar.CreateBuilder()
    .AddJsonFile("holidays/third-party.json", e =>
        e.TryGetProperty("tipo", out var type) && type.GetString() == "facultativo" ? null : JsonHolidaySource.MapDefault(e))
    .BuildAsync();
```

#### DbHolidaySource

> `TEC.Core.Dates.Holidays.Sources` · `sealed class` (implementa `IHolidaySource`)

Feriados lidos de um banco via ADO.NET puro (`DbConnection`), sem depender de ORM nem de provedor específico (SQL Server, PostgreSQL, Oracle, SQLite…).

| Membro | Retorno | Descrição |
|---|---|---|
| `DbHolidaySource(Func<DbConnection> connectionFactory, string commandText, Action<DbCommand>? configureCommand = null, string? name = null)` | — | `connectionFactory` cria uma conexão nova (aberta ou não), aberta se necessário e **descartada** ao final. `configureCommand` ajusta parâmetros, timeout etc. |
| `Name` | `string` | Padrão `"Banco de dados"`. Nunca inclua a connection string no nome. |
| `LoadAsync(CancellationToken cancellationToken = default)` | `IAsyncEnumerable<Holiday>` | Executa o comando e lê as linhas. |

- **Colunas:** associadas pelo nome, sem diferenciar maiúsculas, acentos, `_`, `-` e espaços (mesmos nomes aceitos de [`JsonHolidaySource`](#jsonholidaysource)). `Data` e `Descricao` são obrigatórias; `Uf` e `CodigoIbge` são opcionais (`NULL` nos nacionais). Se a tabela usa outros nomes, use alias: `SELECT dt_feriado AS Data, ...`.
- **Tipos aceitos:** a data pode ser `date`, `datetime`, `datetimeoffset` (parte de data do valor) ou texto em `aaaa-MM-dd`/`dd/MM/aaaa`. O código IBGE pode ser qualquer tipo inteiro, `decimal`/`NUMBER`, `double`/`float`/`REAL` (SQLite) ou texto. Valores com parte fracionária (`3550308.5`), `NaN` ou fora do intervalo de `int` são **recusados** (não são arredondados).

```csharp
var source = new DbHolidaySource(
    () => new NpgsqlConnection(connectionString),
    "SELECT data, descricao, uf, codigo_ibge FROM feriados WHERE empresa_id = @company",
    command =>
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@company";
        parameter.Value = companyId;
        command.Parameters.Add(parameter);
    });
```

#### HttpHolidaySource

> `TEC.Core.Dates.Holidays.Sources` · `sealed class` (implementa `IHolidaySource`)

Feriados obtidos por HTTP GET de uma API que devolve um array JSON, com o mesmo formato e os mesmos limites de [`JsonHolidaySource`](#jsonholidaysource).

| Membro | Retorno | Descrição |
|---|---|---|
| `BrasilApiBaseUrl` (const) | `string` | `"https://brasilapi.com.br/api/feriados/v1/"` (o ano vai no final). |
| `HttpHolidaySource(HttpClient httpClient, Uri requestUri, Func<JsonElement, Holiday?>? mapper = null)` | — | `requestUri` absoluta ou relativa ao `BaseAddress`. Padrão do `mapper`: `JsonHolidaySource.MapDefault`. |
| `BrasilApi(HttpClient httpClient, int year)` (static) | `HttpHolidaySource` | Feriados nacionais do ano na BrasilAPI (1900 a 2199). |
| `Name` | `string` | URL sem credenciais, query string e fragmento. |
| `LoadAsync(CancellationToken cancellationToken = default)` | `IAsyncEnumerable<Holiday>` | Faz o GET e lê a resposta. |

O `HttpClient` é do chamador e **não** é descartado. Use `IHttpClientFactory` e configure timeout e retry nele.

```csharp
var calendar = await HolidayCalendar.CreateBuilder()
    .AddSource(HttpHolidaySource.BrasilApi(httpClient, 2026), optional: true)
    .AddHttp(httpClient, new Uri("https://<api-de-feriados>/municipais"))
    .BuildAsync();
```

#### DelegateHolidaySource

> `TEC.Core.Dates.Holidays.Sources` · `sealed class` (implementa `IHolidaySource`)

Feriados obtidos por uma função do chamador: EF Core, Dapper, um repositório ou qualquer outra origem.

| Membro | Retorno | Descrição |
|---|---|---|
| `DelegateHolidaySource(Func<CancellationToken, Task<IEnumerable<Holiday>>> load, string name)` | — | `name` aparece nas mensagens de erro. |
| `Name` | `string` | Nome da fonte. |
| `LoadAsync(CancellationToken cancellationToken = default)` | `IAsyncEnumerable<Holiday>` | Chama a função e devolve os itens. |

```csharp
var calendar = await HolidayCalendar.CreateBuilder()
    .AddDelegate(async ct => (await db.Holidays.AsNoTracking().ToListAsync(ct))
        .Select(h => new Holiday(h.Date, h.Description) { Uf = h.Uf, IbgeCode = h.IbgeCode }), "EF Holidays")
    .BuildAsync();
```

#### HolidaySourceFailure

> `TEC.Core.Dates.Holidays.Sources` · `sealed record`

Falha na carga de uma fonte **opcional**, ignorada pelo calendário. Útil para logs e health checks. Não lança exceções.

| Membro | Tipo | Descrição |
|---|---|---|
| `SourceName` | `string` | Nome da fonte (`IHolidaySource.Name`). |
| `Exception` | `Exception` | Erro ocorrido. |

```csharp
builder.OnSourceError(f => logger.LogWarning(f.Exception, "Fonte de feriados ignorada: {Source}", f.SourceName));
```

#### HolidaySourceException

> `TEC.Core.Dates.Holidays.Sources` · `sealed class` (herda `Exception`)

Falha na carga de uma fonte **obrigatória**, lançada por `BuildAsync`. A exceção original fica em `InnerException`.

| Membro | Tipo | Descrição |
|---|---|---|
| `HolidaySourceException(string sourceName, Exception innerException)` | — | Mensagem: *Falha ao carregar os feriados da fonte '&lt;nome&gt;': &lt;mensagem original&gt;* |
| `SourceName` | `string` | Nome da fonte que falhou. |

```csharp
try
{
    calendar = await builder.BuildAsync(cancellationToken);
}
catch (HolidaySourceException ex)
{
    logger.LogCritical(ex.InnerException, "Feriados indisponíveis na fonte {Source}", ex.SourceName);
    throw;
}
```

---

## ⚙️ Opções

| Opção / Parâmetro | Padrão | Descrição |
|---|---|---|
| `nonWorkingDays` (`BusinessDayCalculator`, `BusinessDayCalculatorFactory`, `AddBusinessDayCalculator`) | sábado e domingo | Dias da semana não úteis (ao menos um dia precisa ser útil) |
| `BusinessDayCalculator.MaxBusinessDaysToAdd` | 26.000 | Limite (±) de `AddBusinessDays` |
| `BusinessDayCalculator.MaxRangeDays` | 36.600 | Intervalo máximo, em dias corridos, de `CountBusinessDays`/`GetBusinessDays` |
| Busca de dia útil | 3.660 dias (~10 anos) | Limite interno de iterações de `Next*`/`Previous*` |
| `includeCarnival` / `includeCorpusChristi` (`BrazilianNationalHolidays`, `AddBrazilianNational`) | `true` / `true` | Pontos facultativos federais |
| `optional` (métodos `Add*` do builder) | `false` | Fonte opcional que falha é ignorada (vai para `Failures`) em vez de interromper a carga |
| `HolidayCalendarBuilder.MaxHolidaysPerSource` | 1.000.000 | Feriados por fonte |
| `JsonHolidaySource.MaxJsonBytes` | 64 MB | Tamanho máximo do JSON (arquivo ou resposta HTTP); profundidade máxima 16 |
| `defaultLocation` (`AddBusinessDayCalculator(calendar, …)`) | `HolidayLocation.National` | Localidade do `IHolidayProvider`/`IBusinessDayCalculator` registrados |
| `firstDayOfWeek` (`StartOfWeek`/`EndOfWeek`) | `DayOfWeek.Sunday` | Primeiro dia da semana |
| `timeProvider` (`GetNow`, `GetToday`, `CalculateAge`, `ToRelativeTime`) | `TimeProvider.System` nas sobrecargas sem relógio | Relógio injetável para testes |

---

## ❌ Erros

Mensagens reais do código. 🔒 Nenhuma mensagem repete o valor recebido (que pode vir de arquivo, banco ou API externa).

**Formatação, extensões e fuso**

| Situação | Exceção | Mensagem / o que fazer |
|---|---|---|
| `GetMonthName` com mês fora de 1 a 12 | `ArgumentOutOfRangeException` | *O valor deve estar entre 1 e 12.* |
| `GetDayOfWeekName`, `StartOfWeek`/`EndOfWeek` com `DayOfWeek` inválido | `ArgumentOutOfRangeException` | *Dia da semana inválido.* |
| `ToRelativeTime` com uma data UTC e a outra local | `ArgumentException` | *A data e a referência devem estar no mesmo fuso (ambas UTC ou ambas locais).* |
| `timeProvider` nulo (`ToRelativeTime`, `CalculateAge`, `GetNow`, `GetToday`) | `ArgumentNullException` | Injete `TimeProvider.System` |
| `IsBetween` com `start > end` | `ArgumentException` | *A data inicial não pode ser posterior à data final.* |
| `CalculateAge` com nascimento posterior à referência | `ArgumentException` | *A data de nascimento não pode ser posterior à data de referência.* |
| `ToDateTime` com `DateTimeKind` inválido | `ArgumentOutOfRangeException` | *DateTimeKind inválido.* |
| `ToBrasiliaTime(dateTime)` com `Kind = Unspecified` | `ArgumentException` | Use `ToBrasiliaTime(value, DateTimeKind.Utc)` (ou `Local`) para declarar a origem |
| `ToBrasiliaTime(dateTime, unspecifiedKind)` com `unspecifiedKind` diferente de `Utc`/`Local` | `ArgumentOutOfRangeException` | |
| `TryParseDate` / `TryParseDateTime` / `FromBrasiliaTimeToUtc` | — | Nunca lançam (retornam `false`; horários inexistentes/ambíguos usam −03:00) |

**Dias úteis**

| Situação | Exceção | Mensagem / o que fazer |
|---|---|---|
| `holidayProvider` / `calendar` / `location` nulo | `ArgumentNullException` | |
| `nonWorkingDays` com valor fora do enum | `ArgumentException` | *Dia da semana inválido.* |
| `nonWorkingDays` com os 7 dias | `ArgumentException` | *Ao menos um dia da semana deve ser útil.* |
| `AddBusinessDays` acima de ±26.000 | `ArgumentOutOfRangeException` | *O valor deve estar entre -26000 e 26000.* |
| Intervalo acima de 36.600 dias (`CountBusinessDays`/`GetBusinessDays`) | `ArgumentOutOfRangeException` | *O intervalo não pode passar de 36600 dias.* |
| `GetNthBusinessDayOfMonth` com `n ≤ 0` | `ArgumentOutOfRangeException` | |
| `GetNthBusinessDayOfMonth` com `n` maior que o total do mês | `ArgumentOutOfRangeException` | *O mês MM/AAAA possui apenas N dias úteis.* |
| Ano fora de 1 a 9999 ou mês fora de 1 a 12 | `ArgumentOutOfRangeException` | |
| `GetLastBusinessDayOfMonth` em mês sem dia útil | `InvalidOperationException` | *O mês MM/AAAA não possui dias úteis.* |
| Cálculo que ultrapassaria 01/01/0001 ou 31/12/9999 | `ArgumentOutOfRangeException` | *O cálculo ultrapassa o limite do calendário (01/01/0001 a 31/12/9999).* |
| Busca sem encontrar dia útil em 3.660 dias | `InvalidOperationException` | 🔒 *Nenhum dia útil encontrado em 3660 dias a partir de dd/MM/aaaa. Verifique o cadastro de feriados.* |
| `AddBusinessDayCalculator` chamado duas vezes | `InvalidOperationException` | Veja [Injeção de dependência](injecao-dependencia.md#addbusinessdaycalculator) |

**Feriados e localidades**

| Situação | Exceção | Mensagem / o que fazer |
|---|---|---|
| `description`/`Description` nula; `location` nulo | `ArgumentNullException` | |
| `uf` nula, vazia ou só espaços (`HolidayLocation`) | `ArgumentException` (`ArgumentNullException` se nula) | |
| UF que não é uma das 27 | `ArgumentException` | *UF inválida. Informe a sigla de uma das 27 UFs (ex.: SP).* |
| Código IBGE fora do formato | `ArgumentException` | *Código IBGE do município inválido: são 7 dígitos, começando pelo código da UF (ex.: 3550308).* |
| Município de outra UF | `ArgumentException` | *O código IBGE do município não pertence à UF informada.* |
| `InMemoryHolidayProvider` com item nulo | `ArgumentException` | *A lista de feriados não pode conter itens nulos.* |
| `GetEaster` ou `GetHolidays(int)` com ano fora de 1 a 9999 | `ArgumentOutOfRangeException` | |

**Carga do calendário e fontes**

| Situação | Exceção | Mensagem / o que fazer |
|---|---|---|
| `BuildAsync` sem fontes | `InvalidOperationException` | *Nenhuma fonte de feriados foi configurada.* |
| `AddBrazilianNational` duas vezes | `InvalidOperationException` | *Os feriados nacionais calculados já foram adicionados.* |
| Fonte **obrigatória** falhou | `HolidaySourceException` | *Falha ao carregar os feriados da fonte '&lt;nome&gt;': ...*; a causa está em `InnerException` (exceções abaixo). Fonte opcional → `HolidaySourceFailure` |
| Fonte devolveu item nulo / passou de 1.000.000 feriados | `InvalidOperationException` (em `InnerException`) | 🔒 *A fonte passou do limite de 1000000 feriados.* |
| Argumento nulo; caminho, nome ou SQL em branco; lista com item nulo; `years` vazio | `ArgumentException` / `ArgumentNullException` | Na configuração do builder |
| Ano da BrasilAPI fora de 1900 a 2199 | `ArgumentOutOfRangeException` | |
| Carga cancelada | `OperationCanceledException` | Propagado mesmo em fontes opcionais |
| Arquivo CSV/JSON inexistente | `FileNotFoundException` | *Arquivo de feriados não encontrado.* |
| `openStream` devolveu `null` | `InvalidOperationException` | |
| CSV com conteúdo inválido (inclusive UF/IBGE recusados) | `CsvException` | Com linha e coluna (veja [CSV](csv.md#csvexception)) |
| JSON cuja raiz não é array | `FormatException` | *O JSON de feriados deve ser um array de objetos.* |
| Item JSON inválido (não objeto, sem `data`/`descricao`, tipo errado, UF/IBGE inválidos) | `FormatException` | *Feriado na posição N do JSON: ...* |
| JSON acima de 64 MB / profundidade acima de 16 | `InvalidDataException` / `JsonException` | 🔒 *O JSON de feriados passa do limite de 64 MB.* |
| `MapDefault` chamado diretamente com objeto inválido | `FormatException` (formato) / `ArgumentException` (UF/IBGE) | |
| `connectionFactory` devolveu `null` | `InvalidOperationException` | *A função de conexão retornou null.* |
| Resultado SQL sem `Data` e `Descricao` | `InvalidOperationException` | *O resultado do SQL de feriados precisa das colunas 'Data' e 'Descricao' (use alias se necessário).* |
| Linha SQL inválida (data nula/inválida, descrição nula, IBGE fracionário, UF inválida...) | `FormatException` | *Feriado na linha N do resultado: ...* |
| Erro do provedor ADO.NET | `DbException` do provedor | |
| Status HTTP de erro | `HttpRequestException` | `EnsureSuccessStatusCode` |
| Função de `DelegateHolidaySource` devolveu `Task` nula / resultado `null` | `InvalidOperationException` | *A função de carga de feriados retornou uma Task nula.* / *...retornou null.* |
| `HolidaySourceException` com `innerException` nulo | `ArgumentNullException` | |

---

## 🛡️ Segurança

| Ameaça | Controle |
|---|---|
| Fonte de feriados gigante ou maliciosa (DoS) | Limite de 1.000.000 feriados por fonte, JSON até 64 MB e profundidade 16; busca de dia útil limitada a 3.660 dias; `AddBusinessDays` e intervalos com teto |
| Vazamento de segredo em log | `HttpHolidaySource.Name` omite credenciais da URL (`usuario:senha@`), query string e fragmento: `https://svc:S3cr3t@api.exemplo.com/feriados?key=x` → `https://api.exemplo.com/feriados`. `DbHolidaySource.Name` nunca contém a connection string |
| Dados externos em mensagens | Mensagens de validação de UF/IBGE e de itens inválidos nunca repetem o valor recebido |
| Estado compartilhado | `Holiday`, `HolidayCalendar`, provedores e calculadoras são imutáveis e thread-safe (Singleton) |

> [!WARNING]
> O `commandText` do `DbHolidaySource` deve ser um SQL **fixo, definido pela aplicação**. Nunca concatene valores externos: use parâmetros em `configureCommand`.

> [!WARNING]
> Os caminhos de `CsvHolidaySource`/`JsonHolidaySource` (e de `AddCsvFile`/`AddJsonFile`) não devem vir do usuário final (*path traversal*).

> [!CAUTION]
> Implementações próprias de `IHolidayProvider` são chamadas por calculadoras Singleton em paralelo: precisam ser thread-safe.

---

## ❓ Perguntas frequentes

<details>
<summary>O vencimento sai um dia adiantado à noite</summary>

**Causa:** uso de `DateTime.Today`/`DateTime.Now` em servidor UTC: entre 21h e 0h de Brasília a data do servidor já é a do dia seguinte.
**Solução:** use `BrazilTimeZone.Today`/`BrazilTimeZone.Now` (ou `GetToday(timeProvider)`/`GetNow(timeProvider)` com o relógio injetado).

</details>

<details>
<summary>Como testar código que depende de "hoje"?</summary>

Injete `TimeProvider` (registre `TimeProvider.System` em produção) e use `BrazilTimeZone.GetToday(clock)`, `CalculateAge(clock)` e `ToRelativeTime(clock)`. Nos testes, passe um `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) e controle o tempo com `SetUtcNow`/`Advance`. Veja [Relógio injetável](#relógio-injetável-testes).

</details>

<details>
<summary><code>InvalidOperationException</code> ao chamar <code>AddBusinessDayCalculator</code></summary>

A calculadora já foi registrada. Registre uma única vez e combine as fontes no mesmo `HolidayCalendar.CreateBuilder()`. Detalhes em [Injeção de dependência](injecao-dependencia.md#addbusinessdaycalculator).

</details>

<details>
<summary>Um <code>DateTime</code> UTC de sábado de madrugada foi considerado dia útil</summary>

É o comportamento esperado: `Kind = Utc` é um instante e é avaliado na **data de Brasília** (sábado 02:00 UTC é sexta 23:00 em Brasília). Para avaliar a data "como está", passe `DateOnly` ou `DateTime` com `Kind = Unspecified`.

</details>

<details>
<summary>Feriados novos não aparecem depois de atualizar o banco</summary>

O `HolidayCalendar` lê as fontes só em `BuildAsync`, na inicialização, e não recarrega. Reinicie a aplicação para atualizar.

</details>

<details>
<summary>Uma fonte opcional falhou: como saber?</summary>

Registre um callback com `OnSourceError` (log) e consulte `HolidayCalendar.Failures` (ex.: em um health check degradado).

</details>

---

⬅️ [Anterior](numeros.md) · [📚 Índice](README.md) · [Próximo](criptografia.md) ➡️
