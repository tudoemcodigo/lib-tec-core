# Changelog

Todas as mudanças relevantes do **TEC.Core** são registradas aqui.
O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Enquanto a versão for `0.x`, mudanças incompatíveis podem ocorrer em versões MINOR.

## [Não publicado]

### ⚠️ Mudanças incompatíveis

- **Formato do AES-GCM em memória** (`AesGcmCryptography.Encrypt(byte[]…)` e `Encrypt(string…)`): agora começa com um byte de versão (`[versão 0x01][nonce 12][tag 16][dados]`). Dados cifrados pela 0.0.1 **não** são lidos: decifre-os com a 0.0.1 e recifre com a nova versão. O formato em stream não mudou (um byte de versão desconhecido no stream passou a gerar a mesma mensagem explícita de versão não suportada, em vez de "dados inválidos ou corrompidos").
- **AES-GCM em memória: o byte de versão é autenticado** (entra no dado associado, antes do `associatedData` do chamador). Dados cifrados por builds anteriores desta versão não publicada precisam ser recifrados.
- **Formato da criptografia híbrida** (`HybridCryptography`): agora é `[versão 0x01][tamanho 2][chave cifrada][dados AES-GCM]` e o cabeçalho inteiro é o dado associado do AES-GCM. Dados da 0.0.1 precisam ser recifrados. Uma versão desconhecida gera `CryptographicException` com mensagem explícita.
- `HashHelper.FixedTimeEquals` passou a ser **ordinal** (diferencia maiúsculas de minúsculas). Para hashes hexadecimais em qualquer caixa, use o novo `HashHelper.FixedTimeEqualsHex`.
- `IPasswordHasher.Verify(string password, string? hashedPassword)`: o hash passou a aceitar `null` (usuário inexistente) e o contrato exige uma verificação fictícia de custo equivalente. Implementações próprias da interface devem seguir a nova regra.
- `TryParseBrazilianDecimal` e a leitura de `decimal`/`double`/`float` no CSV recusam separador de milhar fora de grupos completos (`"1.5"`, `"1.2.3"`, `"12.34"`), que antes eram lidos como `15`, `123` e `1234`.
- `TryParseBrazilianDecimal` só aceita sinal no início e o símbolo `R$` uma vez, como prefixo: `"12R$34,00"`, `"R$R$ 1"`, `"1,00-"` e `"(1,00)"` passaram a ser recusados.
- **JSON: enums só por nome de membro definido.** `JsonDefaults.Options`/`CreateOptions` e `CreateEnumConverter<TEnum>()` recusam número entre aspas (`"999"`, aceito pelo conversor do .NET 8), combinações por vírgula em enums sem `[Flags]` e, em `[Flags]`, bits não definidos. `CreateEnumConverter<TEnum>()` passou a devolver `JsonConverter<TEnum>` (antes `JsonStringEnumConverter<TEnum>`).
- CSV: `MaxColumns` passou a contar também o último campo da linha (antes aceitava uma coluna a mais); `double`/`float` recusam `NaN`, infinito e valores que estouram (`1E999`); a neutralização de fórmulas vale para qualquer valor textual (`char`, `Uri` e tipos com `TypeConverter`), não só `string`.
- `BusinessDayCalculator.CountBusinessDays`/`GetBusinessDays` recusam intervalos acima de `MaxRangeDays` (36.600 dias) com `ArgumentOutOfRangeException`.
- `SensitiveDataMasker.MaskEmail` mascara o valor inteiro quando há caractere de controle (antes o trecho após o `@`, com a quebra de linha, saía sem alteração).
- `Holiday` é imutável: `Date` e `Description` passaram de `set` para `init` (`Description` nula lança `ArgumentNullException`).
- `AddBusinessDayCalculator` lança `InvalidOperationException` se `IHolidayProvider` ou `IBusinessDayCalculator` já estiverem registrados (antes o provedor informado era descartado em silêncio).
- `BusinessDayCalculator`: `DateTime` com `Kind = Utc` é avaliado na data de Brasília e o resultado preserva o horário de Brasília (volta em UTC).
- `BrazilTimeZone.FromBrasiliaTimeToUtc`: `Kind = Utc` é devolvido sem alteração (antes era deslocado 3 horas); `Kind = Local` é convertido com `ToUniversalTime()`.
- `RequestValidationException(params IEnumerable<Error>)` foi substituído por `RequestValidationException(Error error, params IEnumerable<Error> additionalErrors)` e `RequestValidationException(IEnumerable<Error> errors)`: `new RequestValidationException()` não compila mais.
- `Error`, `ApiError` e `PagedResult<T>` validam também as cópias com `with` (antes era possível criar instâncias inválidas).
- `RsaCryptography` recusa importar chaves com mais de 16384 bits (`MaximumImportKeySize`) ou PEM acima de 32 KB.
- CSV: campos entre aspas não são mais aparados por `TrimValues`; espaços antes da aspa de abertura e depois da aspa de fechamento são ignorados.
- CSV: novo limite `MaxRecordLength` (padrão 4.194.304 caracteres por registro); arquivos com registros maiores passam a gerar `CsvException`.
- Native AOT/trimming: `JsonDefaults.Options`, `JsonDefaults.IndentedOptions`, `JsonExtensions.ToJson<T>(bool)`, `FromJson<T>()` e `TryFromJson<T>(out)` passaram a ter `[RequiresUnreferencedCode]` e `[RequiresDynamicCode]`, e `JsonDefaults.CreateOptions(bool)` tem `[RequiresDynamicCode]`: apps com trimming/AOT recebem avisos IL2026/IL3050 nessas chamadas (use as novas sobrecargas compatíveis com AOT). O comportamento não mudou; `Options`/`IndentedOptions` agora são criadas na primeira utilização.
- Parâmetros genéricos anotados com `[DynamicallyAccessedMembers]`: `T` de `ICsvReader`/`ICsvWriter`/`CsvReader`/`CsvWriter` (`PublicProperties`), `TEnum` de `EnumHelper.GetItems/TryParse/Parse` e `EnumExtensions.ToEnum/ToEnumOrDefault` e o `Type enumType` de `EnumHelper.TryParse(Type, …)` (`PublicFields`). Implementações próprias de `ICsvReader`/`ICsvWriter` precisam repetir a anotação (aviso IL2095), e métodos genéricos que repassem `T`/`TEnum` precisam propagá-la em apps com trimming.

### Adicionado

- Suporte a **.NET 8** (LTS): o pacote tem `lib/net8.0` e `lib/net10.0`, com o mesmo comportamento (APIs do .NET 9+ substituídas por equivalentes internos no `net8.0`, cobertos por testes que rodam nos dois runtimes).
- Compatibilidade com **Native AOT e trimming** (`IsAotCompatible`; avisos IL2xxx/IL3xxx tratados como erro).
- `JsonDefaults.CreateOptions(IJsonTypeInfoResolver?, bool)` e `JsonDefaults.CreateEnumConverter<TEnum>()` para uso com `JsonSerializerContext` gerado.
- `JsonExtensions.ToJson`/`FromJson`/`TryFromJson` com `JsonTypeInfo<T>` ou `JsonSerializerContext` (compatíveis com AOT).
- `HashHelper.FixedTimeEqualsHex`.
- `Result.ToFailure()` e `Result.ToFailure<TOut>()`: propagam os erros de uma falha para outro tipo de resultado.
- `BusinessDayCalculator.MaxRangeDays`.
- `AesGcmCryptography.FormatVersion` e `HybridCryptography.FormatVersion`.
- `RsaCryptography.MaximumImportKeySize`.
- `CsvOptions.StrictColumnCount`, `CsvOptions.RequireAllColumns`, `CsvOptions.MaxRecordLength` e `CsvOptions.AllowThousandsSeparator`.
- `PaginationExtensions.ToPagedResult(this IQueryable<T>…)`: executa só `COUNT` + `Skip`/`Take`, sem materializar a consulta.
- `.editorconfig`, `.gitattributes`, `Directory.Build.props`, `nuget.config` (package source mapping) e workflow CodeQL.

### Corrigido

- **Native AOT:** a leitura e a escrita de CSV lançavam `InvalidOperationException` (o desempate da ordem das colunas usava `PropertyInfo.MetadataToken`, indisponível em Native AOT). A ordem agora vem da declaração: classe base primeiro.
- `RemoveAccents`, `ToSlug`, comparações sem acento e a leitura de cabeçalhos de CSV lançavam `ArgumentException` com texto UTF-16 inválido (surrogate isolado) quando o ICU está disponível.
- `ToCurrency(double)` lançava `OverflowException` (em vez de `ArgumentOutOfRangeException`) exatamente no limite de `decimal`.
- `RsaCryptography.Encrypt(string…)`, `HybridCryptography.Encrypt(string…)` e `HashHelper.ComputeHmac(string, string…)` zeram os bytes intermediários do texto claro e da chave.
- `CsvReader` com tipo de destino `struct` devolvia sempre valores padrão.
- `[CsvIgnore]`/`[CsvColumn]` da classe base eram ignorados em propriedades sobrescritas (`override`).
- `FromBrasiliaTimeToUtc` lançava exceção para horários inexistentes do antigo horário de verão (ex.: 04/11/2018 00:30); agora usa o deslocamento padrão (−03:00) e trata horários ambíguos como horário padrão.
- `PaginationInfo.Create` estourava com `totalItems` próximo de `long.MaxValue`.
- `ToRelativeTime` exibia "há 12 meses" para diferenças de 360 a 364 dias.
- `SensitiveDataMasker.Mask` podia cortar um par surrogate (emoji), gerando texto inválido.
- `JsonExtensions.TryFromJson` deixava escapar `NotSupportedException`.
- `new ConcurrencyException(null)` era ambíguo e não compilava.
- `ParamName` incorreto (`"x?.ToList()"`) em `CsvHolidayProvider.FromFilesAsync` e `AddBusinessDayCalculator`.
- `AddBusinessDayCalculator` avaliava `nonWorkingDays` só na primeira resolução; agora a coleção é copiada e validada no registro.
- Todos os `await` da biblioteca usam `ConfigureAwait(false)` (regra CA2007 como erro), tornando seguro o carregamento bloqueante dos feriados no DI.
- Exemplo de login em `docs/criptografia.md` revelava a existência de usuários pelo tempo de resposta.

### Build e CI

- Versão do pacote: `0.0.1`.
- Vulnerabilidades de pacotes (`NU1901`–`NU1904`) quebram o build; lock file também no projeto de testes.
- `global.json` fixa o SDK `10.0.100` (`rollForward: latestFeature`).
- Actions fixadas por SHA e versões alinhadas entre `ci.yml` e `release.yml`.
- Workflows: a versão calculada é validada como SemVer em todos os caminhos (a base da prévia vem de uma tag) e chega aos passos por variável de ambiente, sem interpolação no script; o token do feed também; Release manual só publica se a tag apontar para commit da `main`; `persist-credentials: false` nos checkouts.
- `release.yml` cria a tag e o Release **antes** de publicar o pacote (falha na tag não deixa pacote órfão).
- `TargetFrameworks` `net8.0;net10.0` no pacote e nos testes; o CI instala também o .NET 8 (`actions/setup-dotnet` com `global-json-file` + `dotnet-version: 8.0.x`) e os resultados `.trx` usam nome padrão (um por TFM).
- `EnablePackageValidation` (sem baseline: a 0.0.1 é a primeira publicação) valida a consistência da API entre os TFMs no `dotnet pack`.

## [0.0.1]

- Versão inicial.
