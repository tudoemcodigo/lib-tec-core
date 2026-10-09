# 📝 Changelog

Todas as mudanças relevantes do **TEC.Core** são registradas aqui.
O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Enquanto a versão for `0.x`, mudanças incompatíveis podem ocorrer em versões MINOR.

## [0.1.0] - 2026-10-09

### ✨ Adicionado

#### 🏛️ Domínio

- `TEC.Core.Domain`: `DomainEntity<TId>` (igualdade por tipo concreto e `Id`; entidade transiente só é igual a si mesma), `AggregateRoot<TId>` (`RaiseDomainEvent`, `DomainEvents`, `ClearDomainEvents`, limite de 1000 eventos pendentes), `IDomainEvent` (`OccurredAt`) e `IHasDomainEvents`, para a persistência coletar os eventos no commit (Outbox do TEC.Messaging).

#### 🧱 Common

- Composição de `Result`: `Bind` e `BindAsync` (em `Result` e `Result<T>`), `Ensure` e `ResultTaskExtensions` (`BindAsync`/`MapAsync` sobre `Task<Result<T>>`). Param na primeira falha e preservam todos os erros.

## [0.0.1] - 2026-10-08

Primeira versão do pacote **TEC.Core**, a base dos componentes TEC.

### ✨ Adicionado

#### 🧱 Common

- `Guard` para validação de argumentos (`NotNull`, `NotNullOrWhiteSpace`, `NotEmpty`, `InRange`, `Positive` com o nome do parâmetro preenchido pelo compilador; `Against` com nome explícito via `nameof`). `InRange` não inclui o valor recebido na mensagem.
- `Result`, `Result<T>`, `Error` e `ErrorType` (com `ErrorTypeExtensions`) para fluxos sem exceções, com status HTTP e regra de exposição ao cliente por categoria.
- `JsonDefaults` e `JsonExtensions` (`ToJson`/`FromJson`/`TryFromJson`): camelCase, nulos omitidos, enums só por nome de membro definido, opções globais somente leitura e sobrecargas compatíveis com Native AOT (`JsonTypeInfo<T>`/`JsonSerializerContext`).
- `BrazilianCulture`, com fallback de cultura pt-BR quando não há ICU.

#### 🌐 Respostas e 🚨 exceções

- `ApiResponse`, `ApiResponse<T>`, `PagedResponse<T>` e `ApiError`: envelope único, com `FromResult`/`FromException` e erros 500/502 que nunca expõem detalhes.
- `PagedResult<T>`, `PaginationInfo` e `PaginationExtensions` (inclusive `ToPagedResult` sobre `IQueryable<T>`), com tamanho máximo de página.
- `AppException` e 10 exceções especializadas com código padrão e status HTTP (`RequestValidationException`, `UnauthenticatedException`, `ForbiddenException`, `NotFoundException`, `ConflictException`, `ConcurrencyException`, `BusinessException`, `RateLimitExceededException`, `IntegrationException`, `InvalidConfigurationException`).

#### 🔤 Texto e 🔢 números

- `DocumentValidator`, `DocumentFormatter` e `DocumentGenerator` para CPF, CNPJ (inclusive alfanumérico), PIS, CEP, telefone e e-mail.
- `MaskFormatter` e `SensitiveDataMasker` (CPF, CNPJ, e-mail, telefone, cartão), sem cortar pares surrogate.
- `StringExtensions` (acentos, slug, título, comparações sem acento), independentes de ICU e com `Regex` gerados.
- `Base64UrlEncoder` (`TEC.Core.Text.Codecs`): Base64Url sem preenchimento, com decodificação estrita e canônica e o mesmo resultado em `net8.0` e `net10.0`.
- `BoundedFileReader` (`TEC.Core.IO`): leitura de arquivo de texto sensível com limite de tamanho (inclusive pipe), UTF-8 estrito, sem BOM e buffers zerados.
- `SensitiveDataMasker.DescribeUntrusted`: descreve texto não confiável para log (tamanho + HMAC com chave do processo), sem reproduzi-lo.
- `SingleFlight<TKey, TValue>` (`TEC.Core.Threading`): uma única execução por chave de uma operação assíncrona (sem *cache stampede*); cancelamento só quando todos os que aguardam desistem.
- `NumericExtensions` (R$, percentual, arredondamentos explícitos, `TryParseBrazilianDecimal` estrito) e `NumberToWordsConverter` (número e valor por extenso).

#### 📅 Datas e dias úteis

- `DateFormatter` e `DateExtensions` (formatação brasileira, parse, períodos, idade, tempo relativo).
- `BrazilTimeZone` (horário de Brasília, conversões de/para UTC), com fallback sem `tzdata`.
- Relógio injetável: `BrazilTimeZone.GetNow(TimeProvider)`/`GetToday(TimeProvider)`, `DateOnly.CalculateAge(TimeProvider)` e `DateTime.ToRelativeTime(TimeProvider)`.
- `BusinessDayCalculator`, `IBusinessDayCalculatorFactory` e `BusinessDayCalculatorFactory`, com limites contra laços longos.
- `BrazilianNationalHolidays` (feriados nacionais fixos e móveis de qualquer ano), `InMemoryHolidayProvider`, `HolidayLocation` (UF + IBGE), `HolidayScope`, `HolidayCalendar`/`HolidayCalendarBuilder`.
- Fontes de feriados `CsvHolidaySource`, `JsonHolidaySource`, `DbHolidaySource` (ADO.NET), `HttpHolidaySource` (pronta para a BrasilAPI) e `DelegateHolidaySource`, com fontes opcionais e relatório de falhas.

#### 🔐 Criptografia

- `AesGcmCryptography` (AES-GCM em memória e em stream), `RsaCryptography` (OAEP-SHA256, assinatura PSS ou PKCS#1), `RsaKeyPair`/`RsaKeyPairJsonConverter` e `HybridCryptography` (RSA + AES-GCM), todos com formatos binários versionados.
- `Pbkdf2PasswordHasher` (PBKDF2-SHA256, 600 mil iterações) com verificação fictícia de mesmo custo para usuário inexistente e `NeedsRehash`.
- `HashHelper` (hash, HMAC, `FixedTimeEquals`, `FixedTimeEqualsHex`) e `SecureRandomGenerator`.

#### 📄 CSV e 🏷️ enums

- `CsvReader`/`CsvWriter` em streaming com `[CsvColumn]`/`[CsvIgnore]`, anti-injeção de fórmulas ligada por padrão, modo estrito, limites de tamanho e `CsvException` com linha e coluna.
- `EnumHelper`, `EnumExtensions` e `EnumItem<TEnum>`.

#### 🧩 Injeção de dependência e 🛡️ identidade

- `AddTecCore(Action<TecCoreOptions>?)`, idempotente, com `TecCoreOptions` (`Csv`, `PasswordHashIterations`, `RsaSignatureMode`) validado na subida.
- `AddBusinessDayCalculator` (calendário com várias localidades ou provedor único), com recusa de registro duplicado.
- `ICurrentUser` e `PrincipalKind`: contrato mínimo de quem executa a operação, com `IsAuthenticated` derivado de `Kind`.

#### ⚡ Plataforma e qualidade

- Alvos `net8.0` e `net10.0` com o mesmo comportamento; compatível com Native AOT e trimming.
- 549 testes por TFM (TUnit + FsCheck), inclusive fuzzing, negação de serviço, vazamento de dados e tempo constante; testes de carga (`Carga-CI`, `Carga-Pesada`), testes estatísticos de tempo (`Seguranca-Pesada`) e benchmarks.
- Samples `TEC.Core.SampleApi` e `TEC.Core.LoadGenerator`.
- CI/CD pelos workflows reutilizáveis do `tec-workflows`: CI em paralelo com `ci-ok`, `performance.yml` semanal e publicação só pelo workflow **Publicar versão**.

[0.0.1]: https://github.com/tudoemcodigo/lib-tec-core
