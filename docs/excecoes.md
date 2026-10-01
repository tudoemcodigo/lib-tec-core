# 🚨 Exceções

[⬅ Índice](README.md) · [README](../README.md)

Hierarquia de exceções da aplicação. Cada exceção carrega código, categoria (`ErrorType`), status HTTP e lista de erros, e é convertida diretamente em `ApiResponse` ou `Result`.

- [Hierarquia](#hierarquia)
- [Exceção ou Result?](#exceção-ou-result)
- [AppException (base)](#appexception)
- [Referência das exceções](#referência-das-exceções)
- [Boas práticas de segurança](#boas-práticas-de-segurança)

---

## Hierarquia

```mermaid
classDiagram
    Exception <|-- AppException
    AppException <|-- RequestValidationException : 400
    AppException <|-- UnauthenticatedException : 401
    AppException <|-- ForbiddenException : 403
    AppException <|-- NotFoundException : 404
    AppException <|-- ConflictException : 409
    ConflictException <|-- ConcurrencyException : 409
    AppException <|-- BusinessException : 422
    AppException <|-- RateLimitExceededException : 429
    AppException <|-- InvalidConfigurationException : 500
    AppException <|-- IntegrationException : 502

    class AppException {
        <<abstract>>
        +string Code
        +ErrorType ErrorType
        +int StatusCode
        +IReadOnlyList~Error~ Errors
        +ToResult() Result
        +ToResult~T~() Result~T~
    }
```

| Exceção | HTTP | `ErrorType` | Código padrão | Mensagem vai ao cliente? |
|---|:---:|---|---|:---:|
| `RequestValidationException` | 400 | `Validation` | `VALIDACAO` | ✅ |
| `UnauthenticatedException` | 401 | `Unauthorized` | `NAO_AUTENTICADO` | ✅ (genérica) |
| `ForbiddenException` | 403 | `Forbidden` | `ACESSO_NEGADO` | ✅ (genérica) |
| `NotFoundException` | 404 | `NotFound` | `NAO_ENCONTRADO` | ✅ (sem o ID) |
| `ConflictException` | 409 | `Conflict` | `CONFLITO` | ✅ |
| `ConcurrencyException` | 409 | `Conflict` | `CONCORRENCIA` | ✅ |
| `BusinessException` | 422 | `BusinessRule` | `REGRA_DE_NEGOCIO` | ✅ |
| `RateLimitExceededException` | 429 | `TooManyRequests` | `LIMITE_EXCEDIDO` | ✅ |
| `InvalidConfigurationException` | 500 | `Failure` | `CONFIGURACAO_INVALIDA` | ❌ só no log |
| `IntegrationException` | 502 | `ExternalService` | `FALHA_INTEGRACAO` | ❌ só no log |

---

## Exceção ou Result?

| Use `Result` / `Result<T>` quando... | Use exceção quando... |
|---|---|
| O "erro" faz parte do fluxo esperado (validação de formulário, busca sem resultado). | A situação é excepcional e o fluxo não pode continuar. |
| O chamador **deve** tratar o erro. | O erro deve subir até o middleware global. |
| Há muitas chamadas por segundo (exceções custam caro). | Está dentro de código profundo, onde propagar `Result` seria verboso. |

As duas abordagens convergem: `AppException.ToResult()` converte exceção em `Result`, e `ApiResponse.FromResult`/`FromException` geram a mesma resposta.

---

## AppException

`TEC.Core.Exceptions` · `abstract class AppException : Exception`

| Membro | Descrição |
|---|---|
| `protected AppException(string code, string message, ErrorType errorType, Exception? innerException = null)` | Um único erro. |
| `protected AppException(string message, IEnumerable<Error> errors, Exception? innerException = null)` | Vários erros, **todos da mesma categoria**. |
| `string Code` | Código do primeiro erro. |
| `ErrorType ErrorType` | Categoria. |
| `int StatusCode` | Status HTTP correspondente. |
| `IReadOnlyList<Error> Errors` | Lista imutável de erros (≥ 1). |
| `Result ToResult()` / `Result<T> ToResult<T>()` | Converte em `Result` de falha. |

### Criando uma exceção própria

```csharp
public sealed class PedidoJaFaturadoException(int numero)
    : ConflictException("PEDIDO_JA_FATURADO", $"O pedido {numero} já foi faturado e não pode ser alterado.");
```

---

## Referência das exceções

### RequestValidationException (400)

Dados de entrada inválidos, com um ou mais erros por campo.

| Construtor | Descrição |
|---|---|
| `RequestValidationException(string field, string message, string code = "VALIDACAO")` | Um único campo. |
| `RequestValidationException(Error error, params IEnumerable<Error> additionalErrors)` | Um ou mais erros. Todos precisam ser do tipo `Validation`. Mensagem: *Um ou mais erros de validação ocorreram.* O primeiro erro é obrigatório: `new RequestValidationException()` não compila. |
| `RequestValidationException(IEnumerable<Error> errors)` | Lista de erros já montada (não vazia; todos do tipo `Validation`). |

```csharp
throw new RequestValidationException("email", "E-mail inválido.");

throw new RequestValidationException(
    Error.Validation("CPF_INVALIDO", "CPF inválido.", "cpf"),
    Error.Validation("EMAIL_OBRIGATORIO", "E-mail é obrigatório.", "email"));
```

> O nome é diferente de `System.ComponentModel.DataAnnotations.ValidationException` para evitar conflito.

### UnauthenticatedException (401)

| Construtor | Descrição |
|---|---|
| `UnauthenticatedException()` | Mensagem padrão: *Credenciais inválidas ou sessão expirada.* |
| `UnauthenticatedException(string code, string message, Exception? innerException = null)` | Código e mensagem específicos. |

🔒 Use **a mesma mensagem** para usuário inexistente e senha incorreta. Mensagens diferentes permitem descobrir quais usuários existem.

### ForbiddenException (403)

| Construtor | Descrição |
|---|---|
| `ForbiddenException()` | Mensagem padrão: *Você não tem permissão para realizar esta operação.* |
| `ForbiddenException(string code, string message, Exception? innerException = null)` | Código e mensagem específicos. |

🔒 Não informe qual permissão falta, pois isso ajuda a mapear o modelo de autorização. Não confunda com `UnauthorizedAccessException` do .NET, que trata de permissões de arquivo e sistema.

### NotFoundException (404)

| Membro | Descrição |
|---|---|
| `NotFoundException(string message)` | Código padrão `NAO_ENCONTRADO`. |
| `NotFoundException(string code, string message, Exception? innerException = null)` | Código específico. |
| `static NotFoundException For(string resourceName)` | `"Cliente"` → *Cliente não encontrado(a).* |

```csharp
var cliente = await repo.ObterAsync(id) ?? throw NotFoundException.For("Cliente");
```

🔒 A mensagem **não inclui o identificador pesquisado**: ele pode ser um dado pessoal (ex.: CPF) e permitiria enumerar registros.

### ConflictException (409)

| Construtor | Descrição |
|---|---|
| `ConflictException(string message)` | Código padrão `CONFLITO`. |
| `ConflictException(string code, string message, Exception? innerException = null)` | Código específico. |

```csharp
if (await repo.EmailExisteAsync(dto.Email))
    throw new ConflictException("EMAIL_JA_CADASTRADO", "E-mail já cadastrado.");
```

### ConcurrencyException (409)

Herda de `ConflictException`. Indica que o registro foi alterado por outro usuário ou processo desde a leitura (concorrência otimista).

| Construtor | Descrição |
|---|---|
| `ConcurrencyException(Exception? innerException = null)` | Mensagem padrão: *O registro foi alterado por outro usuário. Recarregue os dados e tente novamente.* Tem prioridade na resolução de sobrecarga: `new ConcurrencyException(null)` usa a mensagem padrão (antes era ambíguo e não compilava). |
| `ConcurrencyException(string message, Exception? innerException = null)` | Mensagem específica. |

```csharp
try
{
    await db.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException ex)
{
    throw new ConcurrencyException(ex);
}
```

### BusinessException (422)

Os dados são válidos, mas a operação viola uma regra de negócio.

| Construtor | Descrição |
|---|---|
| `BusinessException(string message)` | Código padrão `REGRA_DE_NEGOCIO`. |
| `BusinessException(string code, string message, Exception? innerException = null)` | Código específico. |

```csharp
if (conta.Saldo < valor)
    throw new BusinessException("SALDO_INSUFICIENTE", "Saldo insuficiente para a transferência.");
```

### RateLimitExceededException (429)

| Membro | Descrição |
|---|---|
| `RateLimitExceededException(TimeSpan? retryAfter = null, string message = ...)` | Mensagem padrão: *Muitas requisições. Aguarde e tente novamente.* `retryAfter` deve estar entre 0 (exclusivo) e 24 horas. |
| `TimeSpan? RetryAfter` | Tempo sugerido de espera, para o cabeçalho HTTP `Retry-After`. |

```csharp
if (tentativas >= 5)
    throw new RateLimitExceededException(TimeSpan.FromMinutes(15));
```

### InvalidConfigurationException (500)

| Membro | Descrição |
|---|---|
| `InvalidConfigurationException(string settingName, Exception? innerException = null)` | Mensagem: *A configuração '{settingName}' está ausente ou é inválida.* |
| `string SettingName` | Nome da configuração. |

```csharp
var chave = config["Crypto:Key"] ?? throw new InvalidConfigurationException("Crypto:Key");
```

🔒 Informe apenas o **nome** da configuração, nunca o valor. A mensagem não vai ao cliente (500 genérico). O nome é diferente de `System.Configuration.ConfigurationException` para evitar conflito.

### IntegrationException (502)

| Membro | Descrição |
|---|---|
| `IntegrationException(string serviceName, string message, Exception? innerException = null)` | `serviceName` e `message` são usados **só em logs**. |
| `string ServiceName` | Nome do serviço externo que falhou. |

```csharp
try
{
    return await _http.GetFromJsonAsync<SituacaoCadastral>($"cnpj/{cnpj}", ct);
}
catch (HttpRequestException ex)
{
    throw new IntegrationException("ReceitaFederal", "Falha ao consultar situação cadastral.", ex);
}
```

O cliente recebe: *Serviço externo indisponível. Tente novamente mais tarde.* (HTTP 502).

---

## Boas práticas de segurança

- 🔒 **Nunca** inclua dados pessoais, senhas, tokens ou detalhes de infraestrutura na mensagem de uma exceção cuja mensagem vai ao cliente.
- 🔒 Não repita o valor recebido na mensagem de validação (*"CPF 123 inválido"*): ele pode conter dados pessoais ou conteúdo malicioso.
- Guarde a exceção original em `innerException` para o log. Ela nunca é serializada na resposta.
- Registre em log toda resposta com `StatusCode >= 500`, com o `TraceId`.
