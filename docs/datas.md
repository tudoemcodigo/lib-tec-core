# 📅 Datas

[⬅ Índice](README.md) · [README](../README.md)

Formatação no padrão brasileiro, extensões de data, cálculo de dias úteis com feriados e fuso horário de Brasília.

- [DateFormatter](#dateformatter)
- [DateExtensions](#dateextensions)
- [Dias úteis: BusinessDayCalculator](#businessdaycalculator)
- [Feriados: Holiday e provedores](#feriados)
- [BrazilTimeZone](#braziltimezone)

> Os exemplos usam `var dt = new DateTime(2026, 9, 30, 14, 35, 12);` e `var d = new DateOnly(2026, 9, 30);` (uma quarta-feira).

---

## DateFormatter

`TEC.Core.Dates.Formatting` · `static class` (métodos de extensão para `DateTime` e `DateOnly`)

### Constantes

| Constante | Valor |
|---|---|
| `BrazilianDateFormat` | `"dd/MM/yyyy"` |
| `BrazilianDateTimeFormat` | `"dd/MM/yyyy HH:mm:ss"` |
| `IsoDateFormat` | `"yyyy-MM-dd"` |

### Formatação

| Método | Tipos | Saída |
|---|---|---|
| `ToBrazilianDate()` | `DateTime`, `DateOnly` | `"30/09/2026"` |
| `ToBrazilianDateTime(bool includeSeconds = false)` | `DateTime` | `"30/09/2026 14:35"` / `"30/09/2026 14:35:12"` |
| `ToIso8601()` | `DateTime` | `"2026-09-30T14:35:12.0000000Z"` (UTC) |
| `ToIso8601()` | `DateOnly` | `"2026-09-30"` |
| `ToLongDate()` | `DateTime`, `DateOnly` | `"30 de setembro de 2026"` |
| `ToFullDate()` | `DateTime`, `DateOnly` | `"quarta-feira, 30 de setembro de 2026"` |
| `ToMonthYear()` | `DateTime`, `DateOnly` | `"setembro/2026"` |
| `GetMonthName(int month, bool capitalize = false)` | — | `9` → `"setembro"` / `"Setembro"` |
| `GetDayOfWeekName(DayOfWeek day, bool capitalize = false)` | — | `Wednesday` → `"quarta-feira"` / `"Quarta-feira"` |

### Tempo relativo

`string ToRelativeTime(this DateTime date, DateTime? reference = null)`: a referência padrão é "agora", no mesmo `DateTimeKind` da data.

| Diferença | Saída |
|---|---|
| menos de 60 s | `"agora"` |
| 5 minutos no passado | `"há 5 minutos"` |
| 2 dias no futuro | `"em 2 dias"` |
| 45 dias no passado | `"há 1 mês"` |
| 400 dias no passado | `"há 1 ano"` |

Faixas: minutos (< 60 min), horas (< 24 h), dias (< 30 d), meses (< 365 d, 30 dias cada, no máximo 11) e anos (365 dias cada).

> ⚠️ Misturar uma data UTC com uma referência local (ou o contrário) lança `ArgumentException`, porque a comparação seria incorreta.

### Interpretação (parse)

| Método | Formatos aceitos |
|---|---|
| `bool TryParseDate(string? value, out DateOnly date)` | `dd/MM/yyyy`, `d/M/yyyy`, `dd-MM-yyyy`, `dd.MM.yyyy`, `ddMMyyyy`, `yyyy-MM-dd`, `yyyyMMdd` |
| `bool TryParseDateTime(string? value, out DateTime dateTime)` | `dd/MM/yyyy HH:mm[:ss]`, `d/M/yyyy H:mm[:ss]`, `yyyy-MM-ddTHH:mm[:ss]`, `yyyy-MM-dd HH:mm:ss`, **ou só a data** (hora 00:00) |

| Chamada | Resultado |
|---|---|
| `TryParseDate("30/09/2026", out var x)` | `true`, `2026-09-30` |
| `TryParseDate("20260930", out var x)` | `true`, `2026-09-30` |
| `TryParseDate("31/02/2026", out var x)` | `false` |
| `TryParseDateTime("30/09/2026 14:35", out var x)` | `true`, `2026-09-30T14:35:00` |

---

## DateExtensions

`TEC.Core.Dates.Extensions` · métodos de extensão

| Método | Tipos | Exemplo → Resultado |
|---|---|---|
| `ToDateOnly()` | `DateTime` | `dt` → `2026-09-30` |
| `ToDateTime(TimeOnly? time = null, DateTimeKind kind = Unspecified)` | `DateOnly` | `d.ToDateTime(new TimeOnly(8, 0))` → `2026-09-30T08:00:00` |
| `IsWeekend()` | `DateTime`, `DateOnly` | `03/10/2026` (sábado) → `true` |
| `IsBetween(start, end)` | `DateTime`, `DateOnly` | inclusive; `start > end` → `ArgumentException` |
| `StartOfMonth()` | `DateTime`, `DateOnly` | → `2026-09-01` (00:00 no `DateTime`) |
| `EndOfMonth()` | `DateOnly` | `10/02/2028` → `2028-02-29` |
| `EndOfMonth()` | `DateTime` | `dt` → `2026-09-30T23:59:59.9999999` |
| `StartOfYear()` / `EndOfYear()` | `DateOnly` | → `2026-01-01` / `2026-12-31` |
| `StartOfDay()` / `EndOfDay()` | `DateTime` | → `00:00:00` / `23:59:59.9999999` |
| `StartOfWeek(DayOfWeek first = Sunday)` | `DateOnly` | → `2026-09-27` (domingo); com `Monday` → `2026-09-28` |
| `EndOfWeek(DayOfWeek first = Sunday)` | `DateOnly` | → `2026-10-03` |
| `DaysInMonth()` | `DateOnly` | fevereiro de 2028 → `29` |
| `Quarter()` | `DateOnly` | → `3` |
| `Semester()` | `DateOnly` | → `2` |
| `CalculateAge(referenceDate = null)` | `DateOnly`, `DateTime` | `15/10/1990` em `30/09/2026` → `35` |

- Em `CalculateAge`, quem nasceu em 29/02 completa anos em 28/02 nos anos não bissextos. Nascimento posterior à referência → `ArgumentException`. Sem `referenceDate`, usa `DateTime.Today` (data **do servidor**); em servidores UTC, informe `BrazilTimeZone.Today`.
- `EndOfMonth` e `EndOfDay` funcionam até em `DateTime.MaxValue` (sem estouro).
- ⚠️ `EndOfMonth()`/`EndOfDay()` em `DateTime` terminam em `23:59:59.9999999` (precisão de 100 ns). Colunas SQL Server `datetime` têm precisão de ~3 ms e **arredondam** esse valor para `00:00:00.000` do **dia seguinte**, incluindo no filtro registros do dia 1º do mês seguinte (`datetime2` e `date` não têm esse problema). Para filtrar períodos, prefira o intervalo semiaberto `>= início` e `< início do próximo mês`.

```csharp
// Filtro de relatório mensal: "hoje" de Brasília (DateTime.Today é a data do servidor, geralmente UTC)
var inicio = BrazilTimeZone.Today.StartOfMonth().ToDateTime(TimeOnly.MinValue);
var proximoMes = inicio.AddMonths(1);
var pedidos = db.Pedidos.Where(p => p.Data >= inicio && p.Data < proximoMes);   // intervalo semiaberto
```

---

## BusinessDayCalculator

`TEC.Core.Dates.BusinessDays` · `sealed class : IBusinessDayCalculator`

Cálculo de dias úteis considerando fins de semana e feriados.

```csharp
public BusinessDayCalculator(IHolidayProvider holidayProvider, IEnumerable<DayOfWeek>? nonWorkingDays = null)
```

- `nonWorkingDays`: padrão **sábado e domingo**. Pelo menos um dia da semana precisa ser útil.
- Os métodos que recebem uma data (exceto `GetBusinessDays`) têm sobrecarga com `DateTime`, que **preserva o horário e o `DateTimeKind`**.
- `DateTime` com `Kind = Utc` representa um instante e é avaliado na **data de Brasília**: sábado 02:00 UTC é sexta-feira 23:00 em Brasília, portanto dia útil. O resultado preserva o horário de Brasília e volta em UTC (ex.: `NextBusinessDay` de segunda 02/11/2026 01:00 UTC — domingo 22:00 em Brasília — retorna 04/11/2026 01:00 UTC, isto é, terça 22:00 em Brasília). `Local` e `Unspecified` usam a data como está.

### Métodos

| Método | Descrição |
|---|---|
| `bool IsBusinessDay(date)` | Indica se é dia útil. |
| `NextBusinessDay(date)` | Próximo dia útil **estritamente posterior**. |
| `PreviousBusinessDay(date)` | Dia útil **estritamente anterior**. |
| `NextOrSameBusinessDay(date)` | A própria data, se for útil; senão, o próximo dia útil. Ideal para **vencimentos**. |
| `PreviousOrSameBusinessDay(date)` | A própria data, se for útil; senão, o dia útil anterior. |
| `AddBusinessDays(date, int businessDays)` | Soma (ou subtrai, se negativo) dias úteis. Com `0`, retorna a própria data. Limite: ±26.000 (`MaxBusinessDaysToAdd`). |
| `int CountBusinessDays(start, end)` | Conta os dias úteis **incluindo as duas datas**. A ordem das datas não importa. Intervalo máximo: 36.600 dias (`MaxRangeDays`). |
| `IReadOnlyList<DateOnly> GetBusinessDays(DateOnly start, DateOnly end)` | Lista os dias úteis do período, em ordem cronológica. Intervalo máximo: 36.600 dias (`MaxRangeDays`). |
| `DateOnly GetNthBusinessDayOfMonth(int year, int month, int n)` | N-ésimo dia útil do mês. `n` maior que o total → `ArgumentOutOfRangeException`. |
| `DateOnly GetFirstBusinessDayOfMonth(int year, int month)` | Primeiro dia útil do mês. |
| `DateOnly GetLastBusinessDayOfMonth(int year, int month)` | Último dia útil do mês. Mês sem dia útil → `InvalidOperationException`. |
| `int CountBusinessDaysInMonth(int year, int month)` | Quantidade de dias úteis do mês. |

### Exemplos

Feriados cadastrados: 12/10 (seg), 02/11 (seg), 15/11 (dom), 20/11 (sex) e 25/12 (sex).

```
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
| `AddBusinessDays(new DateTime(2026,10,9,17,30,0), 1)` | `13/10/2026 17:30` | Mantém o horário |
| `CountBusinessDays(01/10/2026, 31/10/2026)` | `21` | |
| `GetBusinessDays(09/10, 14/10)` | `09/10, 13/10, 14/10` | |
| `GetNthBusinessDayOfMonth(2026, 11, 5)` | `09/11/2026` | 5º dia útil (pagamento de salário) |
| `GetFirstBusinessDayOfMonth(2026, 11)` | `03/11/2026` | 01/11 é domingo; 02/11 é feriado |
| `GetLastBusinessDayOfMonth(2026, 12)` | `31/12/2026` | |
| `CountBusinessDaysInMonth(2026, 11)` | `19` | |

```csharp
// Empresa que trabalha aos sábados
var calc = new BusinessDayCalculator(feriados, nonWorkingDays: [DayOfWeek.Sunday]);
calc.IsBusinessDay(new DateOnly(2026, 10, 3));   // true (sábado)
```

🔒 Limites de segurança: as buscas param após ~10 anos sem encontrar dia útil (`InvalidOperationException` sugerindo revisar o cadastro de feriados), e cálculos que ultrapassariam 01/01/0001 ou 31/12/9999 geram `ArgumentOutOfRangeException` com mensagem clara.

---

## Feriados

`TEC.Core.Dates.Holidays`

### Holiday

```csharp
public sealed class Holiday
{
    public Holiday();
    public Holiday(DateOnly date, string description);

    [CsvColumn("Data", Order = 1)]      public DateOnly Date { get; init; }
    [CsvColumn("Descricao", Order = 2)] public string Description { get; init; }   // null → ArgumentNullException

    public override string ToString();   // "02/11/2026 - Finados"
}
```

`Holiday` é **imutável**: as propriedades só podem ser definidas na criação (construtor ou inicializador `new Holiday { ... }`), então as instâncias devolvidas pelos provedores podem ser compartilhadas sem risco de alteração.

### IHolidayProvider

| Método | Descrição |
|---|---|
| `bool IsHoliday(DateOnly date)` | Indica se a data é feriado. |
| `Holiday? GetHoliday(DateOnly date)` | Feriado da data, ou `null`. |
| `IReadOnlyList<Holiday> GetHolidays(int year)` | Feriados do ano, em ordem cronológica. |
| `IReadOnlyList<Holiday> GetHolidays(DateOnly start, DateOnly end)` | Feriados do período (inclusive), em ordem cronológica. |

### InMemoryHolidayProvider

Provedor em memória, **imutável e thread-safe** (registre como Singleton). Datas duplicadas mantêm a primeira ocorrência; itens nulos geram `ArgumentException`.

| Membro | Descrição |
|---|---|
| `InMemoryHolidayProvider(IEnumerable<Holiday> holidays)` | Cria a partir de uma lista. |
| `int Count` | Quantidade de feriados carregados. |

```csharp
var feriados = new InMemoryHolidayProvider([
    new Holiday(new DateOnly(2026, 10, 12), "Nossa Senhora Aparecida"),
    new Holiday(new DateOnly(2026, 11, 2), "Finados"),
]);
```

### CsvHolidayProvider

Herda de `InMemoryHolidayProvider` e carrega os feriados de CSV.

| Método | Descrição |
|---|---|
| `static Task<CsvHolidayProvider> FromFileAsync(string filePath, CsvOptions? options = null, CancellationToken ct = default)` | Um arquivo. |
| `static Task<CsvHolidayProvider> FromFilesAsync(IEnumerable<string> filePaths, CsvOptions? options = null, CancellationToken ct = default)` | Vários arquivos combinados (ex.: um por ano). |
| `static Task<CsvHolidayProvider> FromStreamAsync(Stream stream, CsvOptions? options = null, CancellationToken ct = default)` | Stream (recurso embutido, download etc.). |

**Layout do arquivo:** separador `;`, cabeçalho obrigatório. O cabeçalho não diferencia maiúsculas nem acentos (`Descricao` ou `Descrição`), e a data aceita `dd/MM/yyyy` ou ISO (`2026-04-21`).

```csv
Data;Descrição
01/01/2026;Confraternização Universal
16/02/2026;Carnaval
17/02/2026;Carnaval
03/04/2026;Sexta-feira Santa
2026-04-21;Tiradentes
```

```csharp
var feriados = await CsvHolidayProvider.FromFilesAsync(["feriados/2026.csv", "feriados/2027.csv"]);
feriados.GetHoliday(new DateOnly(2026, 4, 21));   // 21/04/2026 - Tiradentes

// Recurso embutido no assembly
await using var stream = typeof(Program).Assembly.GetManifestResourceStream("MeuApp.feriados.csv")!;
var embutidos = await CsvHolidayProvider.FromStreamAsync(stream);
```

> 💡 Mantenha arquivos separados para feriados nacionais, estaduais e municipais e combine os que se aplicam a cada unidade com `FromFilesAsync`.

---

## BrazilTimeZone

`TEC.Core.Dates.TimeZones` · `static class`

Útil quando o servidor roda em UTC (containers, nuvem) e as regras de negócio dependem do horário de Brasília.

| Membro | Descrição |
|---|---|
| `const string TimeZoneId = "America/Sao_Paulo"` | Identificador IANA. |
| `TimeZoneInfo Info` | Fuso de Brasília. Tenta o ID IANA, depois o do Windows (`E. South America Standard Time`) e, se o sistema não tiver base de fusos (container sem `tzdata`), usa **UTC−03:00 fixo**, já que o Brasil não adota horário de verão desde 2019. |
| `DateTime Now` | Data e hora atuais em Brasília. |
| `DateOnly Today` | Data atual em Brasília. |
| `DateTime ToBrasiliaTime(this DateTime dateTime)` | Converte UTC, ou o horário local do servidor, para Brasília. `Unspecified` é tratado como UTC. |
| `DateTime FromBrasiliaTimeToUtc(this DateTime brasiliaTime)` | Converte um horário de Brasília para UTC (resultado com `Kind = Utc`). Veja as regras abaixo. |

| Chamada | Resultado |
|---|---|
| `new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc).ToBrasiliaTime()` | `2026-09-30T12:00:00` |
| `new DateTime(2026, 9, 30, 12, 0, 0).FromBrasiliaTimeToUtc()` | `2026-09-30T15:00:00Z` |
| `new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc).FromBrasiliaTimeToUtc()` | `2026-09-30T15:00:00Z` (já é UTC: devolvido sem deslocamento) |
| `new DateTime(2018, 11, 4, 0, 30, 0).FromBrasiliaTimeToUtc()` | `2018-11-04T03:30:00Z` (horário inexistente: −03:00) |
| `new DateTime(2019, 2, 16, 23, 30, 0).FromBrasiliaTimeToUtc()` | `2019-02-17T02:30:00Z` (horário ambíguo: −03:00) |

Regras de `FromBrasiliaTimeToUtc`:

- **`Kind`**: `Unspecified` é o horário de parede de Brasília; `Utc` já está em UTC e é devolvido **sem alteração** (antes era deslocado 3 horas); `Local` é um instante no fuso do servidor e é convertido com `ToUniversalTime()`.
- **Horário inexistente** (lacuna no início do antigo horário de verão, ex.: 04/11/2018 entre 00:00 e 00:59): não lança exceção; é interpretado com o deslocamento padrão −03:00, o que equivale a avançar 1 hora (00:30 → 03:30 UTC = 01:30 do horário de verão).
- **Horário ambíguo** (hora repetida no fim do horário de verão, ex.: 16/02/2019 entre 23:00 e 23:59): interpretado como horário padrão (−03:00), isto é, a **segunda** ocorrência.
- Sem base de fusos no sistema (fallback UTC−03:00 fixo), datas históricas de horário de verão ficam 1 hora diferentes; datas a partir de 2019 não são afetadas.

```csharp
// "Hoje" do ponto de vista do negócio, independente do fuso do servidor
var hoje = BrazilTimeZone.Today;
var vencimento = calc.NextOrSameBusinessDay(hoje.AddDays(30));
```

> ⚠️ Evite `DateTime.Now` e `DateTime.Today` em servidores na nuvem: eles retornam o horário **do servidor** (geralmente UTC), e entre 21h e 0h de Brasília a data já é a do dia seguinte.
