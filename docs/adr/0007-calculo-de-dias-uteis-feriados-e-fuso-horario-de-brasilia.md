# ADR-0007: Cálculo de Dias Úteis, Feriados Nacionais Móveis e Tratamento de Fuso Horário de Brasília

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Dates`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

Aplicações corporativas e financeiras no Brasil enfrentam desafios singulares na gestão de datas e prazos de vencimento:
1. **Regras Regulatórias do Mercado Financeiro (ANBIMA / FEBRABAN / Bacen):** Vencimentos e liquidações financeiras não ocorrem em dias não úteis bancários. Prazos com vencimento em sábado, domingo ou feriado prorrogam-se para o primeiro dia útil subsequente (regra D+0).
2. **Feriados Móveis Gregorianos:** Datas como Carnaval, Sexta-feira da Paixão e Corpus Christi dependem do cálculo computacional do Domingo de Páscoa, variando ano a ano.
3. **Novas Legislações Nacionais:** A inclusão do feriado nacional de 20 de Novembro (Dia Nacional de Zumbi e da Consciência Negra, Lei 14.759/2023) e o histórico de 12 de Outubro (Nossa Senhora Aparecida, Lei 6.802/1980) exigem vigência temporal exata.
4. **Fuso Horário em Servidores em Nuvem:** Servidores e containers rodam preferencialmente em UTC. Se a aplicação comparar a data local do servidor com o calendário brasileiro diretamente, boletos e transações noturnas (ex.: 22h em Brasília = 01h UTC do dia seguinte) calculam prazos no dia errado.
5. **Containers Linux sem Base de Fusos:** Imagens Docker Alpine ou distroless frequentemente não possuem `tzdata` instalado, fazendo chamadas a `TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")` falharem.

## 💡 Decisão

Criar um motor completo e desacoplado de cálculo de datas brasileiras:

### 1. Feriados Nacionais Calculados ([`BrazilianNationalHolidays`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Dates/Holidays/BrazilianNationalHolidays.cs))
- **Sem Banco nem Arquivo:** Calcula datas fixas e móveis em memória para qualquer ano gregoriano.
- **Algoritmo de Meeus/Jones/Butcher:** Cálculo exato da Páscoa e dedução automática dos dias móveis (Carnaval: Páscoa -48 e -47; Paixão de Cristo: Páscoa -2; Corpus Christi: Páscoa +60).
- **Vigência Histórica:** Incorporação de 12/10 apenas a partir de 1980 e de 20/11 apenas a partir de 2024.
- **Cache Thread-Safe:** Cache estático (`ConcurrentDictionary`) para anos entre 1900 e 2200 para acesso em $O(1)$.

### 2. Calculadora de Dias Úteis ([`BusinessDayCalculator`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Dates/BusinessDays/BusinessDayCalculator.cs))
- Cálculo bidirecional de dias úteis (`AddBusinessDays`, `NextBusinessDay`, `PreviousBusinessDay`, `NextOrSameBusinessDay`).
- Regra D+0: `AddBusinessDays(data, 0)` retorna o próprio dia se útil ou prorroga para o próximo dia útil.
- **Proteção Contra Loops Infinitos:** Limite estrito `MaxSearchDays = 3660` (~10 anos) e `MaxBusinessDaysToAdd = 26000` (~100 anos), impedindo que configurações inválidas travem a thread em `while(true)`.

### 3. Fuso Horário de Brasília sem `tzdata` ([`BrazilTimeZone`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Dates/TimeZones/BrazilTimeZone.cs))
- Detecção inteligente do fuso `America/Sao_Paulo` (Linux/macOS) ou `E. South America Standard Time` (Windows).
- **Fallback Automático:** Caso o sistema operacional não possua base de fusos (container sem `tzdata`), o componente inicializa automaticamente com `UTC-03:00` fixo (o Brasil não possui horário de verão desde o Decreto nº 9.772/2019).
- **Prevenção de Erro de Conversão Dupla:** `ToBrasiliaTime` recusa valores com `DateTimeKind.Unspecified` sem declaração explícita de interpretação, evitando subtrair 3 horas acidentalmente de valores que já estavam no horário de Brasília.
- Suporte nativo a [`TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider) injetável para testes determinísticos de tempo (`GetNow(TimeProvider)`, `GetToday(TimeProvider)`).

## ⚖️ Consequências

### Positivas
- **Acurácia Fiscal e Financeira:** Prazos, SLA e vencimentos bancários 100% aderentes à regulamentação nacional.
- **Resiliência em Nuvem:** Roda perfeitamente em qualquer container Linux (inclusive Alpine) sem exigir pacotes adicionais no Dockerfile.
- **Testabilidade Total:** Eliminação do acoplamento a `DateTime.Now` mediante o uso de `TimeProvider`.

### Negativas / Trade-offs
- Para calendários municipais e estaduais específicos, é necessário alimentar o [`HolidayCalendar`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Dates/Holidays/HolidayCalendar.cs) via fontes externas (CSV, JSON, Banco ou BrasilAPI).
