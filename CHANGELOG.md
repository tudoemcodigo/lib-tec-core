# Changelog — TEC.Core

Este changelog começa com a versão inicial do pacote. Não há versões anteriores do TEC.Core neste projeto.

## [0.0.1] - 2026-10-30

Versão inicial.

- **Adicionado**: primitivos de resultado e erro, respostas paginadas, domínio, criptografia, validação de documentos, datas e dias úteis, CSV, utilitários e registros de injeção de dependência.
- **Adicionado**: alvo único `net10.0` (o pacote só traz `lib/net10.0`), com verificações de trimming e Native AOT. Apps em .NET 8 precisam migrar para o .NET 10 antes de adotar o pacote. Não há polyfills internos (`EncodingCompat`, `OverloadResolutionPriorityAttribute`), versões 8.x de `Microsoft.Extensions.DependencyInjection*` nem referência a `System.Linq.AsyncEnumerable` nos testes.
- **Adicionado**: `DecryptFromFileAsync` de `ISymmetricCryptography`/`AesGcmCryptography` e de `IHybridCryptography`/`HybridCryptography` tem o parâmetro opcional `maxBytes` antes do `CancellationToken`, com padrão de 16 MiB (`DefaultMaxFileBytes`). Arquivo acima do limite gera `InvalidDataException` sem ser lido além do limite. Para decifrar em memória arquivos maiores que 16 MiB, informe `maxBytes` ou use `DecryptFileAsync`; quem passa o `CancellationToken` precisa nomeá-lo.
- **Adicionado**: `DateFormatter.TryParseDate` (e `TryParseDateTime`, que recorre a ele) tenta `yyyyMMdd` antes de `ddMMyyyy`: `"20120101"` vira 01/01/2012, não 20/12/0101. Oito dígitos válidos nos dois formatos, o que só acontece com `ddMMyyyy` de ano entre 0100 e 1299, são lidos como `yyyyMMdd`.
- **Adicionado**: o mapeamento de CSV ignora propriedades sem getter público. Uma propriedade `{ private get; set; }` fica fora do CSV na escrita e na leitura (não ocupa posição na leitura sem cabeçalho).
- **Adicionado**: testes unitários, testes de carga, benchmarks, documentação de API e decisões arquiteturais.
- **Documentação**: XML docs de `TecCoreOptions.PasswordHashIterations` (faixa real: 100.000 a 2.000.000) e de `IBusinessDayCalculator` (`Local` também é convertido para Brasília); limitação de desempenho de `DomainEntity<TId>.GetHashCode` em [docs/dominio.md](docs/dominio.md); links do README absolutos para funcionarem na página do pacote NuGet.
- **Licença**: Apache-2.0, com atribuições em [NOTICE](NOTICE).
