# 🧩 Injeção de dependência

[⬅ Índice](README.md) · [README](../README.md)

`TEC.Core.DependencyInjection` · `static class ServiceCollectionExtensions`

Registra os serviços do TEC.Core no container do `Microsoft.Extensions.DependencyInjection`. Todos os registros usam `TryAdd`, então **um registro seu feito antes prevalece**.

---

## AddTecCore

```csharp
IServiceCollection AddTecCore(
    this IServiceCollection services,
    CsvOptions? csvOptions = null,
    int passwordHashIterations = Pbkdf2PasswordHasher.DefaultIterations)   // 600.000
```

| Interface | Implementação | Ciclo de vida |
|---|---|---|
| `ISymmetricCryptography` | `AesGcmCryptography` | Singleton |
| `IAsymmetricCryptography` | `RsaCryptography` (assinatura PSS) | Singleton |
| `IHybridCryptography` | `HybridCryptography` (usa as duas acima) | Singleton |
| `IPasswordHasher` | `Pbkdf2PasswordHasher(passwordHashIterations)` | Singleton |
| `ICsvReader` | `CsvReader(csvOptions)` | Singleton |
| `ICsvWriter` | `CsvWriter(csvOptions)` | Singleton |

```csharp
builder.Services.AddTecCore(
    csvOptions: new CsvOptions { Delimiter = ',' },
    passwordHashIterations: 800_000);
```

---

## AddBusinessDayCalculator

### Com provedor de feriados já carregado

```csharp
IServiceCollection AddBusinessDayCalculator(
    this IServiceCollection services,
    IHolidayProvider holidayProvider,
    IEnumerable<DayOfWeek>? nonWorkingDays = null)
```

```csharp
var feriados = await CsvHolidayProvider.FromFileAsync("feriados-2026.csv");
builder.Services.AddBusinessDayCalculator(feriados);
```

### Carregando de arquivos CSV

```csharp
IServiceCollection AddBusinessDayCalculator(
    this IServiceCollection services,
    IEnumerable<string> holidayCsvFiles,
    IEnumerable<DayOfWeek>? nonWorkingDays = null,
    CsvOptions? csvOptions = null)
```

```csharp
builder.Services.AddBusinessDayCalculator(
    ["feriados/nacionais.csv", "feriados/sao-paulo.csv"],
    nonWorkingDays: [DayOfWeek.Sunday]);   // sábado é dia útil
```

| Interface | Implementação | Ciclo de vida |
|---|---|---|
| `IHolidayProvider` | `CsvHolidayProvider` ou o provedor informado | Singleton |
| `IBusinessDayCalculator` | `BusinessDayCalculator` | Singleton |

- A **existência dos arquivos é validada na inicialização**: arquivo inexistente gera `FileNotFoundException` já no `Program.cs`, e não na primeira requisição em produção. Lista vazia ou caminho em branco gera `ArgumentException`.
- O conteúdo é lido **uma única vez**, na primeira resolução do serviço. A leitura bloqueia a thread que resolve o serviço, mas é segura contra deadlock: toda a cadeia de leitura usa `ConfigureAwait(false)`. Para evitar o bloqueio, carregue antes com `await CsvHolidayProvider.FromFilesAsync(...)` e use a sobrecarga com provedor.
- `nonWorkingDays` é **copiado e validado no registro** (nas duas sobrecargas): alterar a coleção depois não afeta a calculadora, e um valor inválido falha já no `Program.cs`.
- ⚠️ Chame `AddBusinessDayCalculator` **uma única vez**: se `IHolidayProvider` ou `IBusinessDayCalculator` já estiverem registrados, é lançada `InvalidOperationException` (antes, o provedor informado era descartado em silêncio). Na sobrecarga com provedor, a calculadora é criada e registrada como instância já no registro.

> 💡 Para copiar os CSVs de feriados para a saída do build:
> ```xml
> <ItemGroup>
>   <None Update="feriados\*.csv" CopyToOutputDirectory="PreserveNewest" />
> </ItemGroup>
> ```

---

## Consumindo os serviços

```csharp
public class CobrancaService(
    IBusinessDayCalculator diasUteis,
    ISymmetricCryptography crypto,
    ICsvWriter csv)
{
    public DateOnly CalcularVencimento(DateOnly emissao) =>
        diasUteis.NextOrSameBusinessDay(emissao.AddDays(30));
}
```

## Substituindo uma implementação

Como o registro usa `TryAdd`, registre a sua implementação **antes** de `AddTecCore()`:

```csharp
builder.Services.AddSingleton<IPasswordHasher, MeuHasherArgon2>();
builder.Services.AddTecCore();   // não sobrescreve o IPasswordHasher acima
```

## Classes estáticas

`DocumentValidator`, `DocumentFormatter`, `SensitiveDataMasker`, `HashHelper`, `SecureRandomGenerator`, `EnumHelper`, `DateFormatter`, `BrazilTimeZone` e as classes de extensão são **estáticas e sem estado**. Use-as diretamente, sem registro no DI.
