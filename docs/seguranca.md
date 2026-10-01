# 🛡️ Segurança

[⬅ Índice](README.md) · [README](../README.md)

O componente segue o princípio **Zero Trust**: toda entrada (parâmetros, arquivos, hashes armazenados, JSON, query string) é tratada como não confiável.

---

## Garantias do componente

| Área | Proteção |
|---|---|
| **AES-GCM** | Cifra autenticada; byte de versão de formato (autenticado); subchave por stream (HKDF); detecção de adulteração, reordenação, truncamento e dados extras; mensagens de erro genéricas. |
| **RSA** | OAEP-SHA256; assinatura PSS por padrão; chaves < 2048 bits recusadas (inclusive na importação); geração limitada a 8192 bits e importação a 16384 bits (PEM acima de 32 KB recusado); verificação por rótulo PEM antes de operações com a chave privada. |
| **Híbrida** | Byte de versão de formato; cabeçalho (versão + chave cifrada) vinculado ao conteúdo (AAD); qualquer falha gera uma única exceção genérica (sem oráculo de padding). |
| **Senhas** | PBKDF2-SHA256 com 600 mil iterações; salt aleatório de 16 bytes; comparação em tempo constante; verificação fictícia de mesmo custo para usuário inexistente (sem enumeração por tempo); normalização Unicode determinística (independente de ICU); limites de tamanho e de iterações contra hash adulterado (DoS). |
| **Material sensível** | Chaves e textos claros intermediários (bytes criados pela biblioteca; a `string` recebida do chamador é imutável e fica com ele) são zerados na memória (`CryptographicOperations.ZeroMemory`); a chave privada fica fora de `ToString()` e do JSON. |
| **Aleatoriedade** | Tudo usa `RandomNumberGenerator` (CSPRNG), inclusive os geradores de documentos de teste. |
| **CSV** | Anti-injeção de fórmulas ligado por padrão (em qualquer valor textual, não só `string`); `NaN`/infinito recusados; limites de tamanho de campo, de registro e de colunas (contra esgotamento de memória); modo estrito opcional para colunas ausentes e linhas curtas; separador de milhar só em grupos válidos; erros sem o conteúdo do arquivo. |
| **JSON** | Escape de caracteres sensíveis a HTML (`< > & ' +`); opções globais imutáveis; enums só por nome de membro definido (números, inclusive entre aspas, combinações em enums sem `[Flags]` e bits não definidos são recusados); profundidade máxima 64. |
| **API** | Erros internos (500) e de integração (502) nunca expõem mensagem ou detalhes; `NotFoundException` sem o identificador; mensagens genéricas de autenticação e autorização; paginação com tamanho máximo. |
| **Validação** | Regex com `\z` (sem aceitar quebra de linha no final); limite de tamanho das entradas; valores numéricos de enum não definidos são recusados. |
| **Disponibilidade** | Funciona com `InvariantGlobalization` e sem `tzdata` (containers enxutos). Laços de dias úteis têm limite de iterações (soma, busca, contagem e listagem). Texto UTF-16 inválido não derruba as funções de texto. |
| **Build** | Analisadores de segurança (CA2xxx, CA3xxx, CA5xxx) com as regras críticas como **erro**; NuGet Audit (incluindo dependências transitivas) com vulnerabilidades `NU1901`–`NU1904` como **erro**; lock file de pacotes em todos os projetos; *package source mapping* (`nuget.config`); SDK fixado (`global.json`); actions fixadas por SHA; CodeQL; build determinístico. |

---

## Responsabilidades de quem usa

### 🔑 Chaves

- **Nunca** coloque chaves no código, no `appsettings` versionado ou em logs.
- Use um cofre de segredos (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) e faça rotação periódica.
- No AES com nonce aleatório, não ultrapasse **2³² mensagens por chave**.

```csharp
// ✅ Chave vinda do cofre / variável de ambiente
var chave = builder.Configuration["Crypto:Key"]
    ?? throw new InvalidConfigurationException("Crypto:Key");

// ❌ Nunca
const string Chave = "MvBSvYickCZ+YJffelkX/1yWCeyRewn7UbA9GbJn3rs=";
```

### 📂 Caminhos de arquivo

`ReadFileAsync`, `WriteFileAsync`, `ComputeFileHashAsync` e `CsvHolidayProvider.FromFileAsync`/`FromFilesAsync` usam o caminho recebido. **Nunca** repasse nomes de arquivo vindos do usuário sem validar (risco de *path traversal*):

```csharp
var baseDir = Path.GetFullPath("/dados/importacao");
var caminho = Path.GetFullPath(Path.Combine(baseDir, nomeRecebido));

if (!caminho.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    throw new ForbiddenException();
```

### 🌊 Stream descriptografado

Se `AesGcmCryptography.DecryptAsync` lançar exceção, **descarte todo o conteúdo já gravado** na saída: ele pode estar incompleto.

```csharp
var temp = Path.GetTempFileName();
try
{
    await using (var saida = File.Create(temp))
        await aes.DecryptAsync(entrada, saida, chave);
    File.Move(temp, destino, overwrite: true);   // só publica depois de verificar tudo
}
catch
{
    File.Delete(temp);
    throw;
}
```

### ✍️ Criptografia híbrida

Garante **sigilo, não autoria**. Para garantir a origem, **assine** também (`RsaCryptography.SignData`) com a chave privada do remetente e verifique com a chave pública dele.

### 🚨 Erros 500

Registre os detalhes em log **sem dados pessoais** (use `SensitiveDataMasker`) e retorne `ApiResponse.InternalError()` ou use `ApiResponse.FromException`, que já faz isso.

### 📄 CSV

`IncludeRawValueInErrors = true` expõe o conteúdo do arquivo nas exceções. Use somente em ambiente controlado.

### 🧪 CI

Rode os testes também com a globalização invariante:

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet test --solution TEC.Core.slnx
```

---

## Checklist rápido

- [ ] Chaves e segredos vêm de um cofre, nunca do código.
- [ ] Senhas usam `IPasswordHasher` e o login chama `NeedsRehash`.
- [ ] Comparações de hash e token usam `HashHelper.FixedTimeEquals` (ordinal, diferencia maiúsculas de minúsculas) ou `HashHelper.FixedTimeEqualsHex` (hashes hexadecimais em qualquer caixa).
- [ ] O login chama `IPasswordHasher.Verify` **também** quando o usuário não existe (`Verify(senha, usuario?.SenhaHash)`), para não revelar contas pelo tempo de resposta.
- [ ] CSV de origem externa usa `StrictColumnCount`/`RequireAllColumns` quando colunas ausentes indicariam arquivo errado.
- [ ] Logs usam `SensitiveDataMasker` para CPF, e-mail, telefone e cartão.
- [ ] O middleware global usa `ApiResponse.FromException` e registra os erros 5xx.
- [ ] Caminhos de arquivo vindos do usuário são validados.
- [ ] Paginação usa `Paginate`/`CalculateSkip` com `maxPageSize` adequado.
- [ ] CSV exportado para usuários mantém `SanitizeFormulas = true`.

---

## Relatando vulnerabilidades

Não abra *issue* pública para vulnerabilidades. Envie os detalhes diretamente para o mantenedor, **Roberto Oliveira**, pelo e-mail [roberto@roberto.inf.br](mailto:roberto@roberto.inf.br).
