[🏠 TEC.Core](../README.md) › [📚 Documentação](README.md) › Revisão técnica

# 🔎 Revisão técnica

> Análise de segurança, bugs, implementação, organização e documentação do TEC.Core, com cada achado conferido no código-fonte e, quando possível, reproduzido.

## 📑 Sumário

- [🎯 Escopo e método](#-escopo-e-método)
- [🧪 Build e testes](#-build-e-testes)
- [📊 Resumo dos achados](#-resumo-dos-achados)
- [🔍 Achados em detalhe](#-achados-em-detalhe)
  - [R01 · SingleFlight cancela dentro da trava](#r01--singleflight-cancela-dentro-da-trava)
  - [R02 · CSV: DateTime e DateTimeOffset sem ida e volta exata](#r02--csv-datetime-e-datetimeoffset-sem-ida-e-volta-exata)
  - [R03 · CSV: arquivo fora de UTF-8 é corrompido em silêncio](#r03--csv-arquivo-fora-de-utf-8-é-corrompido-em-silêncio)
  - [R04 · Feriados em CSV: data vazia vira 01/01/0001](#r04--feriados-em-csv-data-vazia-vira-01010001)
  - [R05 · PBKDF2: custo da verificação fictícia difere do real](#r05--pbkdf2-custo-da-verificação-fictícia-difere-do-real)
  - [R06 · PBKDF2: normalização parcial e sem versão](#r06--pbkdf2-normalização-parcial-e-sem-versão)
  - [R07 · RSA: chave PEM importada a cada operação](#r07--rsa-chave-pem-importada-a-cada-operação)
  - [R08 · BoundedFileReader estoura com maxBytes = int.MaxValue](#r08--boundedfilereader-estoura-com-maxbytes--intmaxvalue)
  - [R09 · ToRelativeTime compara Brasília com o fuso do servidor](#r09--torelativetime-compara-brasília-com-o-fuso-do-servidor)
  - [R10 · Zeragem de material sensível menor que a anunciada](#r10--zeragem-de-material-sensível-menor-que-a-anunciada)
  - [R11 · Abstractions 10.x imposta a consumidores net8.0](#r11--abstractions-10x-imposta-a-consumidores-net80)
  - [R12 · CsvWriter.WriteFileAsync destrói o arquivo anterior em caso de falha](#r12--csvwriterwritefileasync-destrói-o-arquivo-anterior-em-caso-de-falha)
  - [R13 a R15 · Informativos](#r13-a-r15--informativos)
- [💡 Melhorias sugeridas](#-melhorias-sugeridas)
- [📝 Ajustes na documentação](#-ajustes-na-documentação)
- [✅ Correções aplicadas — 2026-10-19](#-correções-aplicadas--2026-10-19)

---

## 🎯 Escopo e método

| Item | Valor |
|---|---|
| Data | 2026-10-19 |
| Commit analisado | `3b4c627 CHANGELOG: 0.1.0 publicada em 2026-10-09 (#5)` (árvore de trabalho com `.gitignore` alterado e `.omc/` não versionado, sem mudanças no código) |
| Código revisado | `TEC.Core/` inteiro (Result/Guard, domínio, respostas e paginação, exceções, documentos, mascaramento, datas/feriados/fuso, criptografia, CSV, `BoundedFileReader`, `SingleFlight`, DI, serialização), `build/`, `Directory.*.props`, workflows e o `dotnet-ci.yml` do `tec-workflows` |
| Também consultados | Testes (`TEC.Core.Tests`), samples e os docs em `docs/` |
| Método | Leitura do código, verificação de cada trecho antes do registro, e um programa de prova (fora do repositório, referenciando o `TEC.Core.dll` de Release) para reproduzir R01, R02, R03, R04, R08 e R09 |

Severidades: **Crítica** (exploração direta ou perda de dados grave), **Alta** (falha de segurança ou bug com impacto provável), **Média** (bug real com impacto em cenário plausível), **Baixa** (caso de borda, desempenho ou divergência pequena), **Info** (organização e sugestões).

---

## 🧪 Build e testes

| Comando | Resultado |
|---|---|
| `dotnet build TEC.Core.slnx -c Release` (SDK 10.0.401) | ✅ Compilação com êxito, **0 avisos, 0 erros** (pacote com `TreatWarningsAsErrors`, analisadores de segurança e AOT) |
| `dotnet test --project TEC.Core.Tests -c Release` | ✅ **1122 testes aprovados**, 0 falhas, 0 ignorados, em `net8.0` e `net10.0` (cerca de 7 s) |
| Testes de carga e integração | Não executados (fora do escopo pedido) |

A suíte é extensa (fuzzing com FsCheck, DoS, vazamento, tempo constante) e passa. Os achados abaixo são casos que ela não cobre.

---

## 📊 Resumo dos achados

| Severidade | Quantidade |
|---|:-:|
| Crítica | 0 |
| Alta | 0 |
| Média | 4 |
| Baixa | 8 |
| Info | 3 |

| ID | Severidade | Categoria | Local | Resumo | Status |
|---|---|---|---|---|---|
| R01 | Média | Concorrência | `Threading/SingleFlight.cs:118-135` | `Cancel()` roda os callbacks da operação dentro da trava global; exceção de callback escapa como `AggregateException` | ✅ Corrigido |
| R02 | Média | Bug (fuso/dados) | `Csv/Internal/CsvValueConverter.cs:61,70,139` | Leitura converte `Z`/offset para o fuso do servidor (`Kind=Local`); escrita sem `Format` perde milissegundos e o `Kind` | ✅ Corrigido |
| R03 | Média | Bug (dados) | `Csv/CsvOptions.cs:36`, `Csv/CsvReader.cs:50` | CSV em Windows-1252 (padrão do Excel pt-BR) é lido com `�` no lugar dos acentos, sem erro | ✅ Corrigido |
| R04 | Média | Bug (dados) | `Dates/Holidays/Sources/CsvHolidaySource.cs:49`, `Csv/Internal/CsvValueConverter.cs:46` | Linha de feriado com data vazia (ou sem a coluna `Data`) vira feriado em 01/01/0001, sem erro | ✅ Corrigido |
| R05 | Baixa | Segurança (tempo) | `Cryptography/Hashing/Pbkdf2PasswordHasher.cs:79,83` | Verificação fictícia usa as iterações atuais; hashes antigos (menos iterações) respondem mais rápido | ✅ Corrigido |
| R06 | Baixa | Segurança / contrato | `Pbkdf2PasswordHasher.cs:94`, `Text/Internal/LatinDiacritics.cs:65` | Normalização da senha cobre só Latin-1 e não é versionada no formato do hash | ✅ Corrigido (contrato congelado; escopo da normalização mantido) |
| R07 | Baixa | Desempenho | `Cryptography/Asymmetric/RsaCryptography.cs:64,82,130,146,184` | PEM interpretado e chave importada a cada `Encrypt`/`Decrypt`/`Sign`/`Verify` | ✅ Corrigido |
| R08 | Baixa | Bug (overflow) | `IO/BoundedFileReader.cs:49-50` | `maxBytes = int.MaxValue` gera `OverflowException` | ✅ Corrigido |
| R09 | Baixa | Bug (fuso) | `Dates/Formatting/DateFormatter.cs:84,113-115` | `BrazilTimeZone.Now.ToRelativeTime()` em servidor UTC devolve "há 3 horas" | ✅ Corrigido |
| R10 | Baixa | Docs × código | `HashHelper.cs:31,89`, `SecureRandomGenerator.cs:46`, `Pbkdf2PasswordHasher.cs:94` | Buffers de texto claro não zerados, ao contrário do que `seguranca.md` afirma | ✅ Corrigido |
| R11 | Baixa | Organização / pacotes | `Directory.Packages.props:11`, `TEC.Core/TEC.Core.csproj:15` | `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12 também no alvo `net8.0` | ✅ Corrigido |
| R12 | Baixa | Bug (dados) | `Csv/CsvWriter.cs:84` | `WriteFileAsync` abre com `FileMode.Create`: falha ou cancelamento deixa o arquivo truncado no lugar do anterior | ✅ Corrigido |
| R13 | Info | API | `Responses/ApiResponse.cs:19-41` | `init` públicos permitem envelopes incoerentes (`Success = true` com 500) | ⏸️ Não corrigido |
| R14 | Info | Desempenho | `Cryptography/Hybrid/HybridCryptography.cs:59`, `Csv/CsvWriter.cs:62` | Cópias e alocações evitáveis (concatenação na híbrida, LINQ por linha no CSV) | ⏸️ Não corrigido |
| R15 | Info | Organização | `TEC.Core.Tests/Security/*AuditTests.cs` | Testes nomeados pela rodada de auditoria, não pelo comportamento | ⏸️ Não corrigido |

Nenhuma falha de criptografia foi encontrada: AES-GCM com nonce aleatório de 12 bytes via `RandomNumberGenerator`, tag de 16 bytes, versão autenticada no AAD; stream com subchave HKDF-SHA256 por arquivo, nonce por contador, flag de último bloco autenticada e checagem de dados extras; RSA com OAEP-SHA256 e PSS, mínimo de 2048 bits, limite de importação e de PEM; híbrida com cabeçalho no AAD e erro único; PBKDF2-SHA256 com 600 mil iterações, salt de 16 bytes, `FixedTimeEquals` e parâmetros armazenados validados; geradores usando só CSPRNG (`GetInt32`, `GetString`, `Shuffle`).

---

## 🔍 Achados em detalhe

### R01 · SingleFlight cancela dentro da trava

**Severidade:** Média · **Categoria:** concorrência

**Status:** ✅ **Corrigido.** `Threading/SingleFlight.cs`: `Leave` marca `Flight.Abandoned` dentro da trava e chama `Cancel()` fora dela; `AggregateException` de callback é descartada (o chamador recebe a `OperationCanceledException`); `RunAsync` testa `Abandoned` para criar outra execução. Testes: `SingleFlightTests.Throwing_cancellation_callback_does_not_replace_the_callers_cancellation` e `Slow_cancellation_callback_does_not_block_other_keys` (falhavam antes: `AggregateException` e espera de 2 s pela outra chave).

**Problema.** Quando o último chamador desiste, `Leave` chama `CancellationTokenSource.Cancel()` **com a trava `_sync` adquirida**. `Cancel()` executa de forma síncrona todos os callbacks registrados no token da operação (`ct.Register(...)`, inclusive os do `HttpClient`, do driver de banco etc.). Enquanto eles rodam, nenhuma outra chamada de `RunAsync`, de **qualquer chave**, entra na trava. Se um callback lança, `Cancel()` lança `AggregateException`, que sai do `finally` de `RunAsync` e substitui a `OperationCanceledException` esperada pelo chamador.

**Evidência.**

```csharp
// Threading/SingleFlight.cs:118-127
private void Leave(Flight flight)
{
    lock (_sync)
    {
        flight.Waiters--;
        if (flight.Waiters == 0 && !flight.Completion.Task.IsCompleted)
        {
            try
            {
                flight.Cancellation.Cancel();   // callbacks da operação rodam aqui, dentro da trava
```

Reprodução (programa de prova, `net10.0`):

```text
1)  AggregateException: One or more errors occurred. (callback)      ← esperado: OperationCanceledException
1b) outra chave esperou 1907 ms                                       ← callback de 2 s bloqueou a chave "b"
```

**Impacto.** Um `SingleFlight` singleton protegendo consultas a banco: uma requisição cancelada (timeout do cliente) faz o driver enviar o pedido de cancelamento dentro do callback; durante esse tempo, todas as requisições do serviço que usam o mesmo `SingleFlight` ficam paradas. A documentação ([concorrencia.md](concorrencia.md)) garante "nunca dentro da trava interna".

**Correção sugerida.** Marcar o abandono dentro da trava e cancelar fora dela; quem chegar depois cria outra execução pela marcação (o laço de nova tentativa em `RunAsync:78` já cobre a corrida).

```csharp
private void Leave(Flight flight)
{
    bool cancel;
    lock (_sync)
    {
        flight.Waiters--;
        cancel = flight.Waiters == 0 && !flight.Completion.Task.IsCompleted;
        if (cancel)
            flight.Abandoned = true;   // RunAsync passa a testar flight.Abandoned em vez de IsCancellationRequested
    }

    if (!cancel)
        return;

    try
    {
        flight.Cancellation.Cancel();  // fora da trava
    }
    catch (ObjectDisposedException)
    {
        // a execução terminou e liberou o token
    }
    catch (AggregateException)
    {
        // falha em callback da operação: não pode trocar a exceção do chamador (registre via evento/diagnóstico)
    }
}
```

Teste a acrescentar: callback que lança e callback lento, conferindo o tipo da exceção e que outra chave não espera.

---

### R02 · CSV: DateTime e DateTimeOffset sem ida e volta exata

**Severidade:** Média · **Categoria:** bug (fuso e perda de dados)

**Status:** ✅ **Corrigido (muda comportamento).** `Csv/Internal/CsvValueConverter.cs`: leitura de `DateTime` com `DateTimeStyles.AdjustToUniversal` (com `Z`/offset → UTC, `Kind = Utc`; sem fuso → `Unspecified`), também em `ParseExact`; escrita sem `Format` de `DateTimeOffset` e de `DateTime` `Kind = Utc` no formato `O`. `DateTime` `Local`/`Unspecified` continua em `G` (o Excel reconhece), com a recomendação de `Format = "O"` em [csv.md](csv.md). Testes: `CsvTests.Read_DateTimeWithZoneDesignator_IsUtcInstant_IndependentOfServerZone` (3 casos), `Read_DateTimeWithoutZone_StaysUnspecified`, `WriteAndRead_UtcDateTimeAndDateTimeOffset_RoundTripExactly`, `Write_UnspecifiedDateTime_KeepsCultureFormat`.

**Problema.** Sem `[CsvColumn(Format = ...)]`:

- Na leitura, `DateTime.Parse(text, culture)` sem `DateTimeStyles` **converte** valores com `Z` ou offset para o fuso do servidor e devolve `Kind = Local`. O mesmo arquivo dá instantes diferentes em servidores com fusos diferentes.
- Na escrita, `IFormattable.ToString(null, pt-BR)` usa o padrão `G` (`dd/MM/yyyy HH:mm:ss`): perde as frações de segundo e o `Kind` (um UTC volta como `Unspecified`).

**Evidência.**

```csharp
// Csv/Internal/CsvValueConverter.cs:61 e 70
return format is null ? DateTime.Parse(text, culture) : DateTime.ParseExact(text, format, culture);
return format is null ? DateTimeOffset.Parse(text, culture) : DateTimeOffset.ParseExact(text, format, culture);
// :139
return formattable.ToString(format, options.Culture);
```

Reprodução:

```text
2)  Quando=2026-01-01T00:00:00.0000000-03:00 Kind=Local          ← lido de "2026-01-01T03:00:00Z"
2b) Quando;Offset | 01/01/2026 03:00:00;01/01/2026 03:00:00 +00:00   ← escrito de 03:00:00.123 UTC
```

**Impacto.** Exportar eventos com horário UTC e reimportar: o valor volta como `Unspecified` 03:00 e, se o código o tratar como horário de Brasília, fica deslocado em 3 horas. Arquivos de parceiros com `Z` são lidos de forma diferente em máquina de desenvolvimento (Brasília) e em container (UTC). O fuzzing de ida e volta não cobre `DateTime` com milissegundos nem `Kind`.

**Correção sugerida.**

```csharp
const DateTimeStyles Styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind;

if (type == typeof(DateTime))
    return format is null ? DateTime.Parse(text, culture, Styles) : DateTime.ParseExact(text, format, culture, Styles);
```

Na escrita, ou usar `"O"` como padrão para `DateTimeOffset` (e para `DateTime` com `Kind = Utc`), ou criar `CsvOptions.DateTimeFormat` e documentar em [csv.md](csv.md) que o padrão `G` descarta frações e `Kind`, recomendando `[CsvColumn(Format = "O")]` para instantes.

---

### R03 · CSV: arquivo fora de UTF-8 é corrompido em silêncio

**Severidade:** Média · **Categoria:** bug (integridade de dados)

**Status:** ✅ **Corrigido (muda comportamento).** `Csv/CsvReader.cs`: qualquer UTF-8 configurado é lido com `UTF8Encoding(true, throwOnInvalidBytes: true)` (com preâmbulo, para o BOM ser consumido sem trocar de codificação); `Csv/Internal/CsvRecordParser.cs` converte `DecoderFallbackException` em `CsvException` com a linha, sem conteúdo do arquivo. Teste: `CsvTests.Read_Windows1252File_ThrowsInsteadOfReplacingAccents` (sem BOM, com BOM e com `Encoding = 1252`).

**Problema.** A codificação padrão é `new UTF8Encoding(true)`, que **substitui** bytes inválidos por `U+FFFD` em vez de falhar. O Excel em português salva "CSV (separado por vírgulas)" em Windows-1252 por padrão; esse arquivo é lido sem erro e com os acentos trocados.

**Evidência.**

```csharp
// Csv/CsvOptions.cs:36
} = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
// Csv/CsvReader.cs:50
using var textReader = new StreamReader(stream, _options.Encoding, detectEncodingFromByteOrderMarks: true, ...
```

Reprodução: `"Nome\r\nJosé da Conceição"` codificado em 1252 é lido como `Jos� da Concei��o`.

**Impacto.** Importação de clientes ou funcionários grava nomes corrompidos no banco sem nenhum aviso. Contraste: `BoundedFileReader` e `StringExtensions.FromBase64` usam UTF-8 estrito.

**Correção sugerida.** Na leitura, decodificar UTF-8 em modo estrito e transformar `DecoderFallbackException` em `CsvException` ("O arquivo não está em UTF-8; salve como CSV UTF-8 ou configure `CsvOptions.Encoding`"). Documentar como ler Windows-1252:

```csharp
// Leitura: UTF-8 estrito por padrão (escrita continua com BOM)
var encoding = _options.Encoding is UTF8Encoding
    ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
    : _options.Encoding;

// Consumidor que recebe arquivos ANSI do Excel:
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var options = new CsvOptions { Encoding = Encoding.GetEncoding(1252) };
```

---

### R04 · Feriados em CSV: data vazia vira 01/01/0001

**Severidade:** Média · **Categoria:** bug (dados silenciosamente errados)

**Status:** ✅ **Corrigido (muda comportamento).** `Dates/Holidays/Sources/CsvHolidaySource.cs` valida data e descrição de cada item por um gancho interno novo do leitor (`CsvReader.ReadValidatedAsync`/`ReadFileValidatedAsync`), que gera `CsvException` com linha e coluna e respeita `SkipInvalidRows`. Coluna ausente no cabeçalho cai na mesma regra (o campo fica vazio). Testes: `HolidayCalendarTests.CsvSource_MissingDateOrDescription_FailsWithLineNumber` (4 casos) e `CsvSource_MissingDate_WithSkipInvalidRows_SkipsTheRow`.

**Problema.** `CsvHolidaySource` usa o `CsvReader` genérico sobre `Holiday`. Campo vazio em propriedade não anulável recebe o valor padrão (`DateOnly` = 01/01/0001), e basta **uma** coluna do cabeçalho corresponder para o mapeamento ser aceito. As fontes JSON e de banco recusam data ausente (`FormatException`); a de CSV não.

**Evidência.**

```csharp
// Csv/Internal/CsvValueConverter.cs:46
return underlying is not null || !type.IsValueType ? null : DefaultValue(type);
// Dates/Holidays/Sources/CsvHolidaySource.cs:49
var reader = new CsvReader(_options);   // sem RequireAllColumns e sem validação da data/descrição
```

Reprodução: `"Data;Descricao;Uf\r\n;Aniversário da cidade;SP"` gera `01/01/0001 - Aniversário da cidade (SP)`, e `BuildAsync` termina com sucesso.

**Impacto.** Um feriado municipal digitado com a data em branco (ou um CSV com a coluna `Data` renomeada) simplesmente desaparece do calendário: vencimentos são calculados como se o dia fosse útil, sem log nem exceção.

**Correção sugerida.** Validar cada item na fonte, com a mesma regra das demais:

```csharp
await foreach (var holiday in reader.ReadAsync<Holiday>(stream, cancellationToken).ConfigureAwait(false))
{
    if (holiday.Date == default)
        throw new FormatException("Feriado sem data no CSV.");
    if (string.IsNullOrWhiteSpace(holiday.Description))
        throw new FormatException("Feriado sem descrição no CSV.");
    yield return holiday;
}
```

E criar o leitor com `RequireAllColumns` para `Data` e `Descricao` (ou conferir o cabeçalho na fonte), mantendo `Uf` e `CodigoIbge` opcionais.

---

### R05 · PBKDF2: custo da verificação fictícia difere do real

**Severidade:** Baixa · **Categoria:** segurança (canal lateral de tempo)

**Status:** ✅ **Corrigido.** `Cryptography/Hashing/Pbkdf2PasswordHasher.cs`: com hash válido de menos iterações, completa `_iterations - iterations` com derivação fictícia. Teste: `ConstantTimeTests.PasswordVerify_HashWithFewerIterations_CostsTheSameAsUnknownUser` (razão mediana antes: 0,17; exigido: 0,5 a 2,0).

**Problema.** Para hash `null`/malformado, a derivação fictícia usa as iterações **da instância**; para um hash válido, usa as iterações **gravadas no hash**. Depois de subir as iterações (fluxo recomendado com `NeedsRehash`), usuários que ainda não logaram têm hash com menos iterações e respondem mais rápido que um usuário inexistente.

**Evidência.**

```csharp
// Cryptography/Hashing/Pbkdf2PasswordHasher.cs:79 e 83
_ = Derive(password, DummySalt, _iterations, HashSize);
var actualHash = Derive(password, salt, iterations, expectedHash.Length);
```

**Impacto.** Após migrar de 100 mil para 600 mil iterações, um atacante distingue "conta existente com hash antigo" (~6× mais rápida) de "conta inexistente", reabrindo a enumeração de usuários até todos fazerem login. Os testes de tempo constante usam as mesmas iterações nos dois lados, então não detectam.

**Correção sugerida.** Igualar o custo ao máximo entre os dois, sem alterar o resultado:

```csharp
var actualHash = Derive(password, salt, iterations, expectedHash.Length);
if (iterations < _iterations)
    _ = Derive(password, DummySalt, _iterations - iterations, HashSize);   // completa o custo da configuração atual
return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
```

---

### R06 · PBKDF2: normalização parcial e sem versão

**Severidade:** Baixa · **Categoria:** segurança / contrato do formato

**Status:** ✅ **Corrigido no que é seguro: contrato congelado.** Teste `HashingTests.PasswordHasher_FixedVectors_FormatAndNormalizationAreFrozen` com vetores fixos (salt `00..0F`, 100 mil iterações, hash calculado fora do .NET sobre a senha em NFC), inclusive a forma decomposta. A tabela **não** foi ampliada para NFKC: mudaria o hash de senhas já gravadas e exigiria o prefixo versionado (`PBKDF2-SHA256-N2`), registrado no XML doc e em [criptografia.md](criptografia.md) como regra para uma mudança futura.

**Problema.** `LatinDiacritics.Compose` compõe apenas letra + acento do Latin-1 (equivalente ao NFC só nesse subconjunto). O NIST SP 800-63B recomenda NFKC/NFKD; caracteres fora do Latin-1 (ex.: `ő`, `ł`, letras de largura total) não são normalizados. Além disso, a tabela faz parte do cálculo do hash, mas o prefixo `PBKDF2-SHA256` não tem versão de normalização: ampliar a tabela numa versão futura faz senhas já gravadas com a forma decomposta deixarem de validar.

**Evidência.** `Pbkdf2PasswordHasher.cs:94` (`Encoding.UTF8.GetBytes(LatinDiacritics.Compose(password))`) e `Text/Internal/LatinDiacritics.cs:65-82`.

**Impacto.** Baixo para usuários brasileiros (o português está coberto), mas o comportamento é um contrato permanente que hoje não está protegido por teste de regressão com vetores fixos nem por versão.

**Correção sugerida.** Congelar a tabela atual com um teste de vetores conhecidos (senha → hash com salt fixo). Se a normalização mudar, gravar um novo prefixo (ex.: `PBKDF2-SHA256-N2`) e manter a antiga para verificar hashes existentes. Ajustar a documentação para não atribuir a regra ao NIST sem ressalva (feito, ver [Ajustes](#-ajustes-na-documentação)).

---

### R07 · RSA: chave PEM importada a cada operação

**Severidade:** Baixa · **Categoria:** desempenho / amplificação de DoS

**Status:** ✅ **Corrigido (API nova, sem quebra).** `Cryptography/Asymmetric/RsaKey.cs` (`FromPublicKeyPem`/`FromPrivateKeyPem`, `KeySize`, `HasPrivateKey`, `IDisposable`) e sobrecargas `Encrypt`/`Decrypt`/`SignData`/`VerifyData` com `RsaKey` em `RsaCryptography` (as de PEM passaram a delegar a elas, mesmas validações). `IAsymmetricCryptography` não foi alterada (evita quebrar implementações). Testes: `AsymmetricCryptographyTests.RsaKey_ImportedOnce_InteroperatesWithPemOverloads`, `RsaKey_PublicOnly_RefusesPrivateOperations_AndDisposedKeyThrows`, `RsaKey_SharedAcrossThreads_SignsAndVerifiesConcurrently`.

**Problema.** Toda chamada faz `PemEncoding.TryFind`, `RSA.Create()` e `ImportFromPem`, inclusive de chave privada (que no Windows/CNG e no OpenSSL inclui validação dos parâmetros CRT). Em decifragem híbrida por mensagem ou verificação de assinatura por requisição, o custo de importação se soma ao da operação.

**Evidência.** `RsaCryptography.cs:64, 82, 130, 146` chamam `ImportKey`; `RsaCryptography.cs:184-218` cria e importa a chave do zero.

**Impacto.** Vazão menor em cenários de alto volume (webhooks assinados, mensagens cifradas) e maior custo de CPU por requisição forjada. Não há bug funcional.

**Correção sugerida.** Oferecer um tipo de chave importada reutilizável (thread-safe para operações de leitura), mantendo a API por PEM:

```csharp
public sealed class RsaKey : IDisposable
{
    internal RSA Rsa { get; }
    public static RsaKey FromPem(string pem, bool requirePrivate) => new(ImportKey(pem, requirePrivate));
    // ...
}

public byte[] Decrypt(byte[] encryptedData, RsaKey privateKey);
public bool VerifyData(byte[] data, byte[] signature, RsaKey publicKey);
```

---

### R08 · BoundedFileReader estoura com maxBytes = int.MaxValue

**Severidade:** Baixa · **Categoria:** bug (overflow)

**Status:** ✅ **Corrigido.** `IO/BoundedFileReader.cs`: limite calculado em `long` e limitado a `Array.MaxLength`; buffer cheio no limite devolve `false`. Teste: `BoundedFileReaderTests.MaxBytes_at_int_max_value_does_not_overflow`.

**Problema.** `maxBytes + 1` em aritmética não verificada vira `int.MinValue` e o tamanho do buffer fica negativo.

**Evidência.**

```csharp
// IO/BoundedFileReader.cs:49-50
int limit = maxBytes + 1;
var buffer = new byte[reported > 0 ? (int)Math.Min(reported + 1, limit) : Math.Min(InitialBufferBytes, limit)];
```

Reprodução: `TryReadUtf8(arquivoDe3Bytes, int.MaxValue, out _)` → `OverflowException: Arithmetic operation resulted in an overflow.`

**Correção sugerida.** Limitar o parâmetro (`ArgumentOutOfRangeException.ThrowIfGreaterThan(maxBytes, Array.MaxLength - 1)`) ou calcular em `long`:

```csharp
long limit = (long)maxBytes + 1;
int initial = (int)Math.Min(reported > 0 ? reported + 1 : InitialBufferBytes, Math.Min(limit, Array.MaxLength));
```

---

### R09 · ToRelativeTime compara Brasília com o fuso do servidor

**Severidade:** Baixa · **Categoria:** bug (fuso)

**Status:** ✅ **Corrigido (API nova, sem quebra).** `Dates/Formatting/DateFormatter.cs`: `ToRelativeTimeFromBrasilia(this DateTime, TimeProvider? = null)`, que compara com `BrazilTimeZone.GetNow` (`Utc`/`Local` convertidos antes); as sobrecargas existentes mantêm o comportamento, com aviso no XML doc e em [datas.md](datas.md). Teste: `DateFormattingTests.ToRelativeTimeFromBrasilia_ComparesWithBrasiliaNow_OnUtcServer` (relógio com fuso local UTC).

**Problema.** Para `Kind = Unspecified`, "agora" é `GetLocalNow()` (fuso do servidor). Mas a própria biblioteca produz `Unspecified` em horário de Brasília (`BrazilTimeZone.Now`, `GetNow`). Em container UTC, a combinação natural dá 3 horas de erro.

**Evidência.** `DateFormatter.cs:84` e `:113-115` (`kind == DateTimeKind.Utc ? GetUtcNow() : GetLocalNow().DateTime`).

Reprodução com relógio cujo fuso local é UTC: `BrazilTimeZone.GetNow(clock).ToRelativeTime(clock)` → `"há 3 horas"`.

**Impacto.** Telas de "atualizado há…" erradas em produção (UTC) e certas em desenvolvimento (Brasília). O comportamento está documentado em [datas.md](datas.md) (aviso na seção `DateFormatter`), por isso a severidade baixa.

**Correção sugerida.** Uma sobrecarga explícita, sem mudar o comportamento atual: `ToRelativeTimeFromBrasilia(this DateTime brasiliaTime, TimeProvider? clock = null)`, que compara com `BrazilTimeZone.GetNow(clock)`; ou aceitar `DateTimeOffset`, que não tem ambiguidade.

---

### R10 · Zeragem de material sensível menor que a anunciada

**Severidade:** Baixa · **Categoria:** divergência entre documentação e código

**Status:** ✅ **Corrigido.** `HashHelper.ComputeHash(string)` e `ComputeHmac(string, string)` zeram os bytes UTF-8 do texto; `SecureRandomGenerator.GeneratePassword` zera o `char[]` de trabalho em `finally`. A `string` normalizada do PBKDF2 continua sem zeragem possível (comentário no código e ressalva em [seguranca.md](seguranca.md)). Sem teste automatizado: a zeragem de buffer local não é observável de fora sem instrumentação de memória.

**Problema.** [seguranca.md](seguranca.md) afirmava que "textos claros intermediários criados pela biblioteca são zerados". Há buffers criados pela biblioteca que não são:

| Local | Buffer não zerado |
|---|---|
| `Cryptography/Hashing/HashHelper.cs:31` | Bytes UTF-8 do texto em `ComputeHash(string)` (usado para tokens) |
| `Cryptography/Hashing/HashHelper.cs:89` | Bytes UTF-8 do texto em `ComputeHmac(string, string)` (a chave é zerada; o texto não) |
| `Cryptography/Generators/SecureRandomGenerator.cs:46` | `char[]` da senha gerada em `GeneratePassword` |
| `Cryptography/Hashing/Pbkdf2PasswordHasher.cs:94` | `string` da senha normalizada (não pode ser zerada) |

**Correção sugerida.** Zerar os `byte[]`/`char[]` em `finally` (custo desprezível) e manter na documentação a ressalva sobre `string` (feito).

---

### R11 · Abstractions 10.x imposta a consumidores net8.0

**Severidade:** Baixa · **Categoria:** organização / versões de pacotes

**Status:** ✅ **Corrigido.** `Directory.Packages.props`: `Microsoft.Extensions.DependencyInjection(.Abstractions)` 8.0.1/8.0.2 no `net8.0` e 10.0.12 no `net10.0`, com `Condition` por TFM; lock files regenerados por `dotnet restore --force-evaluate` (nuget.org; o `tec-interno` não foi necessário). Teste: `CompatibilityTests.DependencyInjectionAbstractions_MatchesTheRuntimeMajorVersion` (falhava no `net8.0`: 10). Pendente fora deste repositório: o `dependabot.yml` é canônico do `tec-workflows` e ainda não ignora `Microsoft.Extensions.DependencyInjection*`; sem isso, o Dependabot pode propor 10.x no bloco do `net8.0`.

**Problema.** `Microsoft.Extensions.DependencyInjection.Abstractions` é referenciado em 10.0.12 para os dois alvos (o lock do `net8.0` resolve 10.0.12). Um app ASP.NET Core 8 que usa o TEC.Core passa a carregar a Abstractions 10.x no lugar da do framework compartilhado 8.x.

**Evidência.** `Directory.Packages.props:11` e `TEC.Core/TEC.Core.csproj:15`; `TEC.Core/packages.lock.json`, bloco `net8.0`.

**Impacto.** Funciona (o pacote 10.x suporta `net8.0`), mas mistura versões de `Microsoft.Extensions.*` no app 8.x e pode gerar avisos de downgrade (NU1605) em quem fixa 8.x. O próprio `dependabot.yml` já trata ASP.NET Core e EF Core com uma versão por TFM.

**Correção sugerida.** Versão por TFM no `Directory.Packages.props` (8.0.x para `net8.0`, 10.0.x para `net10.0`), como já se faz com ASP.NET Core/EF Core, incluindo a dependência na regra de "dependências pareadas".

---

### R12 · CsvWriter.WriteFileAsync destrói o arquivo anterior em caso de falha

**Severidade:** Baixa · **Categoria:** bug (perda de dados)

**Status:** ✅ **Corrigido.** `Csv/CsvWriter.cs`: grava em `<arquivo>.<guid>.tmp` (`FileMode.CreateNew`, mesma pasta), `File.Move(..., overwrite: true)` no fim e apaga o temporário em falha. Teste: `CsvTests.WriteFile_FailureDuringEnumeration_KeepsPreviousFile` (falha na enumeração e cancelamento).

**Problema.** O arquivo de destino é aberto com `FileMode.Create` (trunca na hora). Se a enumeração dos itens falhar (consulta ao banco caiu) ou for cancelada, sobra um CSV parcial no lugar do anterior, sem marcação de incompleto.

**Evidência.** `Csv/CsvWriter.cs:84` (`new FileStream(filePath, FileMode.Create, ...)`).

**Correção sugerida.** Gravar num temporário na mesma pasta e publicar só no fim, como a documentação já recomenda para o `DecryptAsync`:

```csharp
var temp = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
try
{
    await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        await WriteAsync(stream, items, cancellationToken).ConfigureAwait(false);
    File.Move(temp, filePath, overwrite: true);
}
catch
{
    File.Delete(temp);
    throw;
}
```

---

### R13 a R15 · Informativos

| ID | Local | Observação | Sugestão | Status |
|---|---|---|---|---|
| R13 | `Responses/ApiResponse.cs:19-41` | `Success`, `StatusCode`, `Errors` e `Message` têm `init` públicos: `ApiResponse.Ok() with { StatusCode = 500 }` ou `new ApiResponse { Success = true, StatusCode = 404 }` compilam. As fábricas são consistentes; o tipo não. | Validar a coerência `Success` ⇔ status 2xx/3xx no `init` de `StatusCode`/`Success`, ou tornar os `init` `protected` e manter `with` só para `TraceId`. | ⏸️ Não corrigido: validar a coerência no `init` ou restringir os `init` muda a API pública e o comportamento de `with` usado por consumidores; não é trivial nem seguro numa correção de revisão |
| R14 | `Cryptography/Hybrid/HybridCryptography.cs:59`; `Csv/CsvWriter.cs:62` | A híbrida aloca cabeçalho, texto cifrado e a concatenação (3 cópias do tamanho dos dados); o `CsvWriter` cria um `Select` com closure por linha. | Escrever direto num buffer final (`ISymmetricCryptography` com sobrecarga em `Span`) e iterar as colunas num laço simples. | ⏸️ Não corrigido: exige sobrecargas com `Span` em `ISymmetricCryptography` (mudança de contrato) e reescrita do laço de escrita; otimização sem bug associado |
| R15 | `TEC.Core.Tests/Security/SecurityAuditTests.cs`, `ThirdAuditTests.cs`, `FourthAuditTests.cs` | Nomes pela rodada da auditoria, não pelo comportamento: difícil achar onde está o teste de uma regra. | Redistribuir pelos arquivos de cada tema (ou `Security/<Tema>RegressionTests.cs`). | ⏸️ Não corrigido: redistribuir os testes é reorganização ampla sem efeito funcional; fica para uma mudança própria |

---

## 💡 Melhorias sugeridas

Em ordem de prioridade:

| # | Melhoria | Por que faz sentido aqui |
|:-:|---|---|
| 1 | Corrigir R01 a R04 com testes de regressão (callback que lança no `SingleFlight`; `DateTime` com `Z` e milissegundos no fuzzing de CSV; arquivo 1252; feriado sem data) | São os únicos achados com impacto real em produção |
| 2 | `RsaKey`/chave importada reutilizável e sobrecargas com `ReadOnlySpan<byte>` em AES/RSA/hash (R07, R14) | Reduz CPU e alocação nos caminhos quentes de webhooks, mensageria (TEC.Messaging) e ORM com campos cifrados |
| 3 | Identificador de chave (`keyId`) opcional no formato AES-GCM e na híbrida | Permite rotação de chaves (o documento já pede rotação antes de 2³² mensagens) sem tentar todas as chaves; encaixa com o TEC.Vault |
| 4 | `associatedData` em `EncryptAsync`/`DecryptAsync` | Vincular arquivos cifrados ao contexto (id do registro, tenant), como já é possível em memória |
| 5 | `CsvOptions.MaxRecords` e `CsvOptions.DateTimeFormat`/`DateTimeStyles` | Limite de linhas para uploads (hoje só há limites por campo/registro) e controle explícito de fuso (R02) |
| 6 | `IPasswordHasher` com Argon2id (implementação opcional em pacote separado) | OWASP prefere Argon2id; o formato com prefixo já permite conviver com PBKDF2 |
| 7 | `SingleFlight`: opção de tempo limite da operação compartilhada e métrica (`System.Diagnostics.Metrics`) de execuções coalescidas | Evita entradas presas por operação que ignora o token e dá visibilidade no TEC.Observability |
| 8 | `HolidayCalendarBuilder`: validação de feriado comum a todas as fontes | Garante a mesma regra (data e descrição obrigatórias) em CSV, JSON, banco e delegate (R04) |
| 9 | Teste de vetores fixos do PBKDF2 (senha, salt e iterações → hash esperado) | Protege o contrato do formato e da normalização (R06) |

---

## 📝 Ajustes na documentação

Corrigidos diretamente nesta revisão (o documento dizia X, o código faz Y):

| Arquivo | Antes | Depois |
|---|---|---|
| [concorrencia.md](concorrencia.md) | "nunca dentro da trava interna" sem ressalva | Ressalva de que, no cancelamento, os callbacks registrados no token da operação rodam dentro da trava (R01) |
| [criptografia.md](criptografia.md) | Verificação fictícia "de mesmo custo" | Mesmo custo das iterações configuradas; hashes com menos iterações respondem mais rápido até o rehash (R05) |
| [criptografia.md](criptografia.md) | Normalização "para a forma composta (NIST SP 800-63B)" | Equivalente ao NFC só para letras do Latin-1; o NIST recomenda NFKC/NFKD (R06) |
| [seguranca.md](seguranca.md) | "textos claros intermediários criados pela biblioteca são zerados" | Lista os buffers que não são zerados e a limitação de `string` (R10) |
| [seguranca.md](seguranca.md) | Verificação fictícia "de mesmo custo" | Mesma ressalva de R05 |
| [README.md](README.md) | — | Linha 18 na tabela de temas e nó no mapa |
| [desenvolvimento.md](desenvolvimento.md) | Rodapé terminava no índice | Link para esta revisão |

Pendentes na revisão, **feitos junto com as correções** (2026-10-19), já descrevendo o comportamento novo:

- ✅ [csv.md](csv.md): tabela de tipos com a leitura de `DateTime` com `Z`/offset em UTC, a escrita `O` de `DateTimeOffset`/`DateTime` UTC, o padrão `G` (sem frações) de `Local`/`Unspecified` e a recomendação de `Format = "O"` (R02).
- ✅ [csv.md](csv.md): seção "Arquivos do Excel em ANSI (Windows-1252)" com `CodePagesEncodingProvider`, leitura UTF-8 estrita e a nova linha de erro (R03); `WriteFileAsync` com arquivo temporário (R12).
- ✅ [datas.md](datas.md): `CsvHolidaySource` com data e descrição obrigatórias (R04), em vez do aviso provisório; `ToRelativeTimeFromBrasilia` e o aviso sobre `Unspecified` (R09).
- ✅ [compatibilidade.md](compatibilidade.md): `CS7036` em `ApiResponse.BadRequest("msg")` sem erros num projeto `net8.0` com C# 12 (passe `[]`); dependência por TFM (R11).
- ✅ [concorrencia.md](concorrencia.md), [criptografia.md](criptografia.md) e [seguranca.md](seguranca.md): as ressalvas de R01, R05 e R10 registradas acima foram reescritas para o comportamento corrigido; `RsaKey` documentado (R07) e contrato do formato PBKDF2 (R06).

---

## ✅ Correções aplicadas — 2026-10-19

Branch `fix/revisao-tecnica-2026-10-19` (a partir de `3b4c627`), sem commit. Versão do pacote: **0.1.0 → 0.2.0** (MINOR: em `0.x`, R02, R03, R04 e R11 mudam comportamento; detalhes no [CHANGELOG](../CHANGELOG.md)).

| Resultado | Quantidade |
|---|:-:|
| Corrigidos (Média) | 4 de 4 (R01 a R04) |
| Corrigidos (Baixa) | 8 de 8 (R05 a R12) |
| Info não corrigidos | 3 (R13 a R15, motivos na tabela) |

Cada correção veio com teste de regressão escrito antes; com o código-fonte anterior (mantidos os testes novos), **16 testes falham no `net8.0` e 15 no `net10.0`** (R11 só falha no `net8.0`). R07 e R09 são APIs novas (o teste não compila sem elas); R06 congela o comportamento atual (passa antes e depois, por definição); R10 não tem teste automatizado.

| Comando | Resultado |
|---|---|
| `dotnet restore TEC.Core.slnx --force-evaluate` | ✅ Lock files dos 6 projetos atualizados (só nuget.org; nada do `tec-interno` foi necessário) |
| `dotnet build TEC.Core.slnx -c Release --no-incremental` (SDK 10.0.401) | ✅ **0 avisos, 0 erros** |
| `dotnet test --project TEC.Core.Tests -c Release` | ✅ **1172 testes aprovados** (586 por TFM, +25 por TFM), 0 falhas, 0 ignorados, em `net8.0` (runtime 8.0.31) e `net10.0` (10.0.12); repetido 3 vezes, sem instabilidade |
| Testes de carga, `[Explicit]` e samples em execução | Não executados. O ASP.NET Core 8 não está instalado nesta máquina (só o 10.0.12), então o `TEC.Core.SampleApi` no `net8.0` só foi compilado |

Arquivos de código alterados: `Threading/SingleFlight.cs`; `Csv/CsvReader.cs`, `Csv/CsvWriter.cs`, `Csv/CsvOptions.cs`, `Csv/Abstractions/ICsvWriter.cs`, `Csv/Internal/CsvValueConverter.cs`, `Csv/Internal/CsvRecordParser.cs`; `Dates/Holidays/Sources/CsvHolidaySource.cs`; `Dates/Formatting/DateFormatter.cs`; `Cryptography/Hashing/Pbkdf2PasswordHasher.cs`, `Cryptography/Hashing/HashHelper.cs`, `Cryptography/Generators/SecureRandomGenerator.cs`; `Cryptography/Asymmetric/RsaCryptography.cs` e `RsaKey.cs` (novo); `IO/BoundedFileReader.cs`; `Directory.Packages.props`, `Directory.Build.props` e os `packages.lock.json`.

Decisões a revisar:

- **R02, `DateTime` Local/Unspecified continua em `G`** na escrita (sem frações de segundo), para não piorar a abertura no Excel; só instantes inequívocos (`DateTimeOffset`, `Kind = Utc`) passaram a `O`. A leitura de `DateTimeOffset` sem offset continua assumindo o fuso do servidor.
- **R03, UTF-8 estrito para qualquer UTF-8 configurado**, não só o padrão: quem configurou `new UTF8Encoding(false)` também passa a receber erro em arquivo inválido. Não há opção para voltar ao modo tolerante.
- **R04 usa um gancho interno do `CsvReader`** (`ReadValidatedAsync`) para manter linha e coluna no erro; não foi exposto como API pública.
- **R11 depende do `tec-workflows`**: o `dependabot.yml` canônico precisa ignorar `Microsoft.Extensions.DependencyInjection*` (como já faz com ASP.NET Core/EF Core), senão o Dependabot pode propor 10.x no bloco do `net8.0`.

---

⬅️ [Anterior: Desenvolvimento](desenvolvimento.md) · [📚 Índice](README.md)
