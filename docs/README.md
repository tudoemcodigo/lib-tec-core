# 📚 Documentação do TEC.Core

[⬅ Voltar ao README](../README.md)

Referência completa de todas as classes e métodos públicos, organizada por módulo.

| # | Módulo | Conteúdo |
|:-:|---|---|
| 1 | [🧱 Common](common.md) | `Guard`, `Result`/`Result<T>`, `Error`, `ErrorType`, `ErrorTypeExtensions`, `JsonDefaults`, `JsonExtensions`, `BrazilianCulture` |
| 2 | [🔐 Criptografia](criptografia.md) | `AesGcmCryptography`, `RsaCryptography`, `RsaKeyPair`, `HybridCryptography`, `HashHelper`, `Pbkdf2PasswordHasher`, `SecureRandomGenerator` |
| 3 | [🔤 Texto](texto.md) | `DocumentFormatter`, `MaskFormatter`, `DocumentValidator`, `DocumentGenerator`, `SensitiveDataMasker`, `StringExtensions` |
| 4 | [🔢 Números](numeros.md) | `NumericExtensions`, `NumberToWordsConverter` |
| 5 | [📅 Datas](datas.md) | `DateFormatter`, `DateExtensions`, `BusinessDayCalculator`, `Holiday`, `InMemoryHolidayProvider`, `CsvHolidayProvider`, `BrazilTimeZone` |
| 6 | [🌐 Respostas de API](respostas-api.md) | `ApiResponse`, `ApiResponse<T>`, `PagedResponse<T>`, `ApiError`, `PagedResult<T>`, `PaginationInfo`, `PaginationExtensions` |
| 7 | [🚨 Exceções](excecoes.md) | `AppException` e as 10 exceções especializadas |
| 8 | [📄 CSV](csv.md) | `CsvReader`, `CsvWriter`, `CsvOptions`, `CsvColumnAttribute`, `CsvIgnoreAttribute`, `CsvEnumFormat`, `CsvException` |
| 9 | [🏷️ Enums](enums.md) | `EnumHelper`, `EnumExtensions`, `EnumItem<TEnum>` |
| 10 | [🧩 Injeção de dependência](injecao-dependencia.md) | `AddTecCore()`, `AddBusinessDayCalculator()` |
| 11 | [🛡️ Segurança](seguranca.md) | Garantias do componente e responsabilidades de quem usa |
| 12 | [⚙️ CI/CD e publicação](../.github/workflows/README.md) | Pipeline do GitHub Actions, versionamento automático, releases e Dependabot |

## Convenções desta documentação

- **Assinaturas** omitem `public static` quando o contexto deixa claro.
- **Exemplos de saída** foram obtidos executando o código em 30/09/2026 (os valores aleatórios mudam a cada execução).
- ⚠️ indica comportamento que costuma surpreender; 🔒 indica decisão de segurança.
- Nas tabelas, `→` separa a entrada do resultado.
