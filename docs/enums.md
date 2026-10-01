# 🏷️ Enums

[⬅ Índice](README.md) · [README](../README.md)

Descrições amigáveis, listagem para combos e conversão flexível de texto para enum. Os metadados são lidos por reflexão **uma única vez** e mantidos em cache (thread-safe).

- [EnumHelper](#enumhelper)
- [EnumExtensions](#enumextensions)
- [EnumItem&lt;TEnum&gt;](#enumitemtenum)
- [Enums com [Flags]](#enums-com-flags)

Enum usado nos exemplos:

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

public enum StatusPedido
{
    [Description("Aguardando pagamento")] AguardandoPagamento = 1,
    [Display(Name = "Pedido enviado")]    Enviado = 2,
    Cancelado = 3
}
```

---

## EnumHelper

`TEC.Core.Enums` · `static class`

| Método | Descrição |
|---|---|
| `string GetDescription(Enum value)` | Ordem de prioridade: `[Description]` → `[Display(Description)]` → `[Display(Name)]` → nome do membro. |
| `string GetDisplayName(Enum value)` | Ordem de prioridade: `[Display(Name)]` → `[Description]` → nome do membro. |
| `long GetCode(Enum value)` | Valor numérico, sem estouro em enums `ulong`. |
| `IReadOnlyList<EnumItem<TEnum>> GetItems<TEnum>()` | Todos os membros, com código, nome e descrição. |
| `bool TryParse<TEnum>(string? value, out TEnum result)` | Converte texto aceitando nome, valor numérico, descrição ou nome de exibição, sem diferenciar maiúsculas. |
| `TEnum Parse<TEnum>(string value)` | Igual a `TryParse`, mas lança `ArgumentException` se o valor não for reconhecido. |
| `bool TryParse(Type enumType, string? value, out object? result)` | Versão não genérica, para quando o tipo só é conhecido em tempo de execução. |

| Chamada | Resultado |
|---|---|
| `GetDescription(StatusPedido.AguardandoPagamento)` | `"Aguardando pagamento"` |
| `GetDescription(StatusPedido.Enviado)` | `"Pedido enviado"` |
| `GetDescription(StatusPedido.Cancelado)` | `"Cancelado"` |
| `GetCode(StatusPedido.Enviado)` | `2` |
| `Parse<StatusPedido>("aguardando pagamento")` | `AguardandoPagamento` |
| `Parse<StatusPedido>("2")` | `Enviado` |
| `Parse<StatusPedido>("pedido enviado")` | `Enviado` |
| `Parse<StatusPedido>("99")` | `ArgumentException` (valor não definido) |

🔒 Valores numéricos só são aceitos se corresponderem a um membro **definido**. O `Enum.Parse` nativo aceitaria `"99"` e criaria um valor inválido.

✅ **Trimming/Native AOT:** compatível sem avisos. Os parâmetros `TEnum` (e o `Type enumType` da versão não genérica) são anotados com `[DynamicallyAccessedMembers(PublicFields)]`; os atributos `[Description]`/`[Display]` são lidos dos campos do enum, que o trimmer sempre preserva. Em métodos genéricos seus que repassem `TEnum` para `EnumHelper`/`ToEnum<TEnum>`, propague a anotação.

---

## EnumExtensions

`TEC.Core.Enums.Extensions` · métodos de extensão

| Método | Descrição |
|---|---|
| `string GetDescription(this Enum value)` | Atalho para `EnumHelper.GetDescription`. |
| `string GetDisplayName(this Enum value)` | Atalho para `EnumHelper.GetDisplayName`. |
| `long GetCode(this Enum value)` | Atalho para `EnumHelper.GetCode`. Enums `ulong` acima de `long.MaxValue` retornam o mesmo padrão de bits (valor negativo). |
| `TEnum ToEnum<TEnum>(this string value)` | Converte texto ou lança `ArgumentException`. |
| `TEnum ToEnumOrDefault<TEnum>(this string? value, TEnum defaultValue = default)` | Converte texto ou retorna o valor padrão. |

```csharp
StatusPedido.AguardandoPagamento.GetDescription();          // "Aguardando pagamento"
"pedido enviado".ToEnum<StatusPedido>();                    // Enviado
"xyz".ToEnumOrDefault(StatusPedido.Cancelado);              // Cancelado
```

---

## EnumItem&lt;TEnum&gt;

`TEC.Core.Enums.Models` · `sealed record EnumItem<TEnum>(TEnum Value, long Code, string Name, string Description)`

Ideal para popular *combos*/*selects* e expor listas de domínio em APIs.

```csharp
app.MapGet("/dominios/status-pedido", () => EnumHelper.GetItems<StatusPedido>());
```

```json
[
  { "value": "aguardandoPagamento", "code": 1, "name": "AguardandoPagamento", "description": "Aguardando pagamento" },
  { "value": "enviado", "code": 2, "name": "Enviado", "description": "Pedido enviado" },
  { "value": "cancelado", "code": 3, "name": "Cancelado", "description": "Cancelado" }
]
```

> O campo `value` segue o conversor de enum configurado na aplicação. Com `JsonDefaults.Options`/`CreateOptions()`, é serializado como string em camelCase. Em trimming/Native AOT (`CreateOptions(IJsonTypeInfoResolver)`), registre o conversor do enum com `JsonDefaults.CreateEnumConverter<StatusPedido>()`.

---

## Enums com [Flags]

```csharp
[Flags]
public enum Permissao { Nenhuma = 0, Leitura = 1, Escrita = 2, Exclusao = 4 }

EnumHelper.TryParse<Permissao>("Leitura, Escrita", out var p);   // true → Leitura | Escrita
EnumHelper.TryParse<Permissao>("8", out _);                      // false (bit não definido)
```

Combinações são aceitas somente com bits definidos no enum.
