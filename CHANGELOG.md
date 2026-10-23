# 📝 Changelog

Todas as mudanças relevantes do **TEC.Core** são registradas aqui.
O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Enquanto a versão for `0.x`, mudanças incompatíveis podem ocorrer em versões MINOR.

## [0.0.1] - Não publicado (Protótipo)

Primeira versão do pacote **TEC.Core**, a base dos componentes TEC. Em desenvolvimento e ainda não publicado em produção.

### ✨ Recursos e Funcionalidades

#### 🔐 Criptografia e Arquivos
- `AesGcmCryptography` (AES-GCM em memória, stream e arquivos):
  - Cifragem e decifragem de arquivos com gravação atômica via arquivo temporário (`EncryptFileAsync`, `DecryptFileAsync`, `EncryptToFileAsync`, `DecryptFromFileAsync`) com chave `byte[]` ou Base64.
  - Streaming em blocos com subchaves derivadas por HKDF-SHA256, autenticação por bloco, verificação contra truncamento e limpeza de memória sensível (`CryptographicOperations.ZeroMemory`).
- `HybridCryptography` (RSA + AES-GCM):
  - Suporte a chaves RSA pré-importadas via `RsaKey` (`Encrypt`, `Decrypt`) em memória e texto Base64.
  - Cifragem e decifragem atômica de arquivos híbridos (`EncryptFileAsync`, `DecryptFileAsync`, `EncryptToFileAsync`, `DecryptFromFileAsync`) tanto via PEM quanto via `RsaKey`.
- `RsaCryptography` (RSA-OAEP-SHA256 e PSS/PKCS#1) e `RsaKey` (chave importada e reutilizável entre threads sem reparsear PEM).
- `Pbkdf2PasswordHasher` (PBKDF2-SHA256 com 600 mil iterações, verificação de custo fictício contra ataques de temporização e normalização determinística Latin-1 sem dependência de ICU).
- `HashHelper` (SHA256/384/512, HMAC, `FixedTimeEquals`, `FixedTimeEqualsHex`, hash de streams e arquivos) e `SecureRandomGenerator`.

#### 🧱 Common e Tratamento de Erros
- `Result` e `Result<T>` com encadeamento funcional (`Bind`, `BindAsync`, `Map`, `MapAsync`, `Ensure`), execução seletiva via `Match` (com sobrecargas `Func` e `Action`) e operadores de conversão implícita.
- `Error`, `ErrorType` e `ErrorTypeExtensions` (mapeamento semântico para status HTTP e controle de exposição ao cliente).
- `Guard` para validação de argumentos com mensagens defensivas sem expor valores sensíveis.
- `JsonDefaults` e `JsonExtensions` (serialização camelCase, omissão de nulos, enums estritos, opções imutáveis congeladas e compatibilidade total com Native AOT).
- `BrazilianCulture` com fallback de formatação pt-BR em ambientes sem ICU.

#### 🏛️ Domínio
- `DomainEntity<TId>` (igualdade estrutural por tipo concreto e `Id`, proteção de identidade para entidades transientes).
- `AggregateRoot<TId>` (raiz de agregado, eventos de domínio com teto defensivo de 1000 eventos pendentes via `MaxPendingEvents`).
- `IDomainEvent` e `IHasDomainEvents` para integração desacoplada com padrão Transactional Outbox.

#### 🌐 Respostas de API e Exceções
- `ApiResponse`, `ApiResponse<T>`, `PagedResponse<T>` e `ApiError`: envelope REST universal com sanitização estrita de erros 500/502.
- `PagedResult<T>`, `PaginationInfo` e `PaginationExtensions` (inclusive com `IQueryable<T>`).
- `AppException` e 10 exceções especializadas com status HTTP correspondente.

#### 🔤 Texto, Documentos e Números
- `DocumentValidator`, `DocumentFormatter` e `DocumentGenerator` para CPF, CNPJ (inclusive o novo alfanumérico conforme IN RFB nº 2.229/2024), PIS, CEP, telefone (DDDs Anatel) e e-mail RFC 5321 (regex não retroativo contra ReDoS).
- `SensitiveDataMasker` (mascaramento seguro de PII sem cortar pares surrogate) e `DescribeUntrusted` (descritor de texto não confiável via HMAC com chave efêmera de processo).
- `NumericExtensions` (formatação de moeda R$, percentual, arredondamentos bancários e matemáticos) e `NumberToWordsConverter` (número e moeda por extenso).
- `Base64UrlEncoder` (RFC 4648 sem preenchimento, determinístico).
- `BoundedFileReader` (leitura segura e delimitada de arquivos de configuração e segredos montados em volume com limpeza de memória).

#### 📅 Datas e Dias Úteis
- `BusinessDayCalculator` e `BusinessDayCalculatorFactory` (cálculo bidirecional de dias úteis com regra D+0 e limites de busca contra loops).
- `BrazilianNationalHolidays` (feriados nacionais fixos e móveis para qualquer ano pelo algoritmo Meeus/Jones/Butcher; vigências da Lei 6.802/1980 e Lei 14.759/2023).
- `BrazilTimeZone` (fuso de Brasília com fallback para UTC-03:00 em containers sem `tzdata` e relógio injetável `TimeProvider`).
- Fontes de feriados `CsvHolidaySource`, `JsonHolidaySource`, `DbHolidaySource`, `HttpHolidaySource` e `DelegateHolidaySource`.

#### 📄 CSV e Enums
- `CsvReader` e `CsvWriter` em streaming com anti-injeção de fórmulas OWASP (`SanitizeFormulas`), UTF-8 estrito, escrita transacional atômica em `.tmp` e tetos contra DoS.
- `EnumHelper`, `EnumExtensions` e `EnumItem<TEnum>`.

#### 🔀 Concorrência, Injeção de Dependência e Identidade
- `SingleFlight<TKey, TValue>` (prevenção de Cache Stampede com cancelamento cooperativo fora de travas).
- `ICurrentUser` e `PrincipalKind` (abstração pura de identidade de execução para auditoria e multitenancy).
- `AddTecCore` e `AddBusinessDayCalculator` em `ServiceCollectionExtensions`.
