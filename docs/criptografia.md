# 🔐 Criptografia

[⬅ Índice](README.md) · [README](../README.md)

Criptografia simétrica, assimétrica e híbrida, hashes, hash de senhas e geração de valores aleatórios seguros. Tudo usa apenas APIs nativas do .NET (`System.Security.Cryptography`).

- [Qual classe usar?](#qual-classe-usar)
- [AesGcmCryptography (simétrica)](#aesgcmcryptography)
- [RsaCryptography (assimétrica)](#rsacryptography)
- [RsaKeyPair e RsaSignatureMode](#rsakeypair-e-rsasignaturemode)
- [HybridCryptography (RSA + AES)](#hybridcryptography)
- [HashHelper (SHA / HMAC)](#hashhelper)
- [Pbkdf2PasswordHasher (senhas)](#pbkdf2passwordhasher)
- [SecureRandomGenerator](#securerandomgenerator)
- [Interfaces](#interfaces)

---

## Qual classe usar?

```mermaid
flowchart TD
    A{O que você precisa?} --> B[Guardar senha de usuário]
    A --> C[Cifrar dado que EU mesmo vou decifrar]
    A --> D[Cifrar dado para OUTRO sistema decifrar]
    A --> E[Provar autoria / integridade]
    A --> F[Checksum / assinar webhook]
    A --> G[Token, senha temporária, OTP]

    B --> B1["Pbkdf2PasswordHasher<br/>(irreversível)"]
    C --> C1["AesGcmCryptography<br/>(mesma chave cifra e decifra)"]
    D --> D0{Tamanho do dado}
    D0 -->|"≤ 190 bytes (2048 bits)"| D1[RsaCryptography.Encrypt]
    D0 -->|qualquer tamanho| D2[HybridCryptography]
    E --> E1["RsaCryptography.SignData / VerifyData"]
    F --> F1["HashHelper.ComputeHash / ComputeHmac"]
    G --> G1[SecureRandomGenerator]
```

> 🔒 **Nunca** use criptografia reversível (AES/RSA) para senhas. Use sempre `Pbkdf2PasswordHasher`.

---

## AesGcmCryptography

`TEC.Core.Cryptography.Symmetric` · `sealed class : ISymmetricCryptography`

Criptografia simétrica **autenticada** com AES-GCM: garante sigilo **e** integridade. Qualquer alteração no texto cifrado é detectada na decifragem.

### Métodos

| Método | Descrição |
|---|---|
| `byte[] GenerateKey()` | Gera uma chave aleatória de 32 bytes (AES-256). |
| `string GenerateKeyBase64()` | Gera uma chave em Base64 (44 caracteres). |
| `byte[] Encrypt(byte[] plainData, byte[] key, byte[]? associatedData = null)` | Cifra bytes. A chave deve ter 16, 24 ou 32 bytes. |
| `byte[] Decrypt(byte[] encryptedData, byte[] key, byte[]? associatedData = null)` | Decifra. Lança `CryptographicException` se a chave estiver errada ou os dados tiverem sido adulterados. |
| `string Encrypt(string plainText, string base64Key)` | Cifra texto UTF-8 e retorna Base64. |
| `string Decrypt(string cipherTextBase64, string base64Key)` | Decifra o Base64 gerado por `Encrypt(string, string)`. |
| `Task EncryptAsync(Stream input, Stream output, byte[] key, CancellationToken ct = default)` | Cifra um stream em blocos de 64 KB, sem carregar tudo em memória. |
| `Task DecryptAsync(Stream input, Stream output, byte[] key, CancellationToken ct = default)` | Decifra um stream gerado por `EncryptAsync`. |

### Exemplos

```csharp
var aes = new AesGcmCryptography();

// Texto
var chave = aes.GenerateKeyBase64();          // "MvBSvYickCZ+YJffelkX/1yWCeyRewn7UbA9GbJn3rs="
var cifrado = aes.Encrypt("Dado sigiloso", chave);
var texto = aes.Decrypt(cifrado, chave);      // "Dado sigiloso"

// Dados associados (AAD): autenticados, mas não cifrados.
// Vinculam o texto cifrado a um contexto; decifrar em outro contexto falha.
byte[] key = aes.GenerateKey();
byte[] aad = "cliente:42"u8.ToArray();
byte[] c = aes.Encrypt(dados, key, aad);
aes.Decrypt(c, key, "cliente:43"u8.ToArray());   // AuthenticationTagMismatchException

// Arquivos grandes (streaming)
await using (var entrada = File.OpenRead("backup.zip"))
await using (var saida = File.Create("backup.zip.enc"))
    await aes.EncryptAsync(entrada, saida, key);

await using (var entrada = File.OpenRead("backup.zip.enc"))
await using (var saida = File.Create("backup.zip"))
    await aes.DecryptAsync(entrada, saida, key);
```

### Formatos

```
Em memória:  [ versão 1 byte = 0x01 ][ nonce 12 bytes ][ tag 16 bytes ][ dados cifrados ]

Stream:      [ versão 1 byte = 0x01 ][ salt 32 bytes ]
             [ último? 1 ][ tamanho 4 ][ tag 16 ][ bloco cifrado ≤ 64 KB ]   ← repetido
```

- O **byte de versão** (`AesGcmCryptography.FormatVersion`) permite evoluir o formato sem ambiguidade. Uma versão desconhecida gera `CryptographicException` com mensagem explícita (*Versão de formato dos dados criptografados não suportada*).
- ⚠️ Até a versão 0.0.1 o formato em memória **não** tinha o byte de versão. Dados cifrados com ela precisam ser decifrados com a versão antiga e recifrados (veja o [CHANGELOG](../CHANGELOG.md)).
- Em memória, o tamanho final é `29 + tamanho dos dados` bytes.
- Um arquivo de 200.000 bytes cifrado em stream ocupa 200.117 bytes (33 de cabeçalho + 21 por bloco).
- Cada stream usa uma **subchave própria** (HKDF-SHA256 com a chave + salt aleatório). Isso elimina a reutilização de nonce entre arquivos.
- Cada bloco é autenticado com o seu índice e com a marcação de "último bloco", o que impede **reordenação, truncamento e dados extras** no final.

### Erros

| Situação | Exceção |
|---|---|
| Chave com tamanho diferente de 16/24/32 bytes | `ArgumentException` |
| Chave em Base64 inválido | `ArgumentException`: *A chave deve estar em Base64.* |
| Chave errada, dados ou AAD adulterados | `CryptographicException` (`AuthenticationTagMismatchException`) |
| Formato inválido ou truncado | `CryptographicException`: *Dados criptografados inválidos ou corrompidos.* |
| Byte de versão desconhecido (ex.: dado cifrado pela 0.0.1) | `CryptographicException`: *Versão de formato dos dados criptografados não suportada…* |
| `input` e `output` são o mesmo stream | `ArgumentException` |

> ⚠️ Em `DecryptAsync`, cada bloco é verificado antes de ser gravado, mas o truncamento só é detectado no final. **Se o método lançar exceção, descarte tudo o que já foi gravado em `output`.**

> 🔒 Com nonce aleatório, não cifre mais que 2³² mensagens com a mesma chave (recomendação do NIST). Faça rotação periódica.

---

## RsaCryptography

`TEC.Core.Cryptography.Asymmetric` · `sealed class : IAsymmetricCryptography`

Criptografia assimétrica RSA: a **chave pública** cifra e verifica; a **chave privada** decifra e assina.

| Configuração | Valor |
|---|---|
| Cifragem | OAEP com SHA-256 |
| Assinatura | SHA-256 + **PSS** (padrão) ou PKCS#1 v1.5 |
| Formato das chaves | PEM: pública `SubjectPublicKeyInfo`, privada `PKCS#8` |
| Tamanho mínimo (geração e importação) | `MinimumKeySize = 2048` bits |
| Tamanho máximo (geração) | `MaximumKeySize = 8192` bits |
| Tamanho máximo (importação) | `MaximumImportKeySize = 16384` bits; PEM acima de 32 KB é recusado antes da interpretação (proteção contra DoS com chaves enormes) |

### Métodos

| Método | Descrição |
|---|---|
| `RsaCryptography(RsaSignatureMode signatureMode = Pss)` | Construtor. Use `Pkcs1` apenas para compatibilidade com sistemas legados (ex.: JWT RS256). |
| `RsaKeyPair GenerateKeyPair(int keySizeInBits = 2048)` | Gera um par de chaves PEM. O tamanho deve ser múltiplo de 8, entre 2048 e 8192. |
| `byte[] Encrypt(byte[] data, string publicKeyPem)` | Cifra com a chave pública. Tamanho máximo: `GetMaxDataLength(keySize)`. |
| `byte[] Decrypt(byte[] encryptedData, string privateKeyPem)` | Decifra com a chave privada. |
| `string Encrypt(string plainText, string publicKeyPem)` | Cifra texto UTF-8 e retorna Base64. |
| `string Decrypt(string cipherTextBase64, string privateKeyPem)` | Decifra o Base64. |
| `byte[] SignData(byte[] data, string privateKeyPem)` | Assina com a chave privada. |
| `string SignData(string data, string privateKeyPem)` | Assina texto UTF-8 e retorna a assinatura em Base64. |
| `bool VerifyData(byte[] data, byte[] signature, string publicKeyPem)` | Verifica a assinatura. |
| `bool VerifyData(string data, string signatureBase64, string publicKeyPem)` | Verifica a assinatura Base64. Retorna `false` para Base64 inválido. |
| `static int GetMaxDataLength(int keySizeInBits)` | Tamanho máximo cifrável: `keySize/8 − 66`. |

| Tamanho da chave | `GetMaxDataLength` | Assinatura/cifra em Base64 |
|:---:|:---:|:---:|
| 2048 | 190 bytes | 344 caracteres |
| 3072 | 318 bytes | 512 caracteres |
| 4096 | 446 bytes | 684 caracteres |

### Exemplos

```csharp
var rsa = new RsaCryptography();
var par = rsa.GenerateKeyPair();                 // 2048 bits

// Cifrar pequenos segredos
var cifrado = rsa.Encrypt("segredo", par.PublicKeyPem);
var texto = rsa.Decrypt(cifrado, par.PrivateKeyPem);           // "segredo"

// Assinar e verificar
var assinatura = rsa.SignData("payload", par.PrivateKeyPem);
rsa.VerifyData("payload",  assinatura, par.PublicKeyPem);      // true
rsa.VerifyData("payload2", assinatura, par.PublicKeyPem);      // false

// Integração com sistema legado (PKCS#1 v1.5)
var rsaLegado = new RsaCryptography(RsaSignatureMode.Pkcs1);
```

### Erros

| Situação | Resultado |
|---|---|
| Dados maiores que o limite da chave | `ArgumentException`: *Para esta chave o tamanho máximo é 190 bytes. Para dados maiores use HybridCryptography.* |
| Assinar/decifrar com a chave **pública** | `ArgumentException`: *A operação exige a chave privada (PEM "PRIVATE KEY").* |
| PEM inválido | `ArgumentException`: *Chave PEM inválida.* |
| Chave importada < 2048 bits | `ArgumentException` |
| Chave importada > 16384 bits ou PEM acima de 32 KB | `ArgumentException` |
| `GenerateKeyPair(1024)` | `ArgumentOutOfRangeException` |
| Falha ao decifrar (chave errada, dado corrompido) | `CryptographicException` genérica: *Falha ao descriptografar os dados.* |
| Assinatura PSS verificada com instância `Pkcs1` (ou o contrário) | `false` |

🔒 Qualquer falha de decifragem gera **a mesma** exceção genérica, sem revelar a causa (proteção contra ataques de oráculo de padding).

---

## RsaKeyPair e RsaSignatureMode

`TEC.Core.Cryptography.Asymmetric`

```csharp
public sealed record RsaKeyPair(string PublicKeyPem, [property: JsonIgnore] string PrivateKeyPem);
```

| Membro | Descrição |
|---|---|
| `PublicKeyPem` | Chave pública (`-----BEGIN PUBLIC KEY-----`). Pode ser distribuída. |
| `PrivateKeyPem` | Chave privada (`-----BEGIN PRIVATE KEY-----`). Guarde em um cofre de segredos. |
| `ToString()` | 🔒 Oculta a chave privada: `RsaKeyPair { PublicKeyPem = ..., PrivateKeyPem = *** }` |
| Serialização JSON | 🔒 `PrivateKeyPem` é ignorada (`[JsonIgnore]`). |

| `RsaSignatureMode` | Descrição |
|---|---|
| `Pss` (0) | RSASSA-PSS. Padrão recomendado, com prova de segurança. |
| `Pkcs1` (1) | PKCS#1 v1.5. Apenas para compatibilidade com sistemas legados. |

---

## HybridCryptography

`TEC.Core.Cryptography.Hybrid` · `sealed class : IHybridCryptography`

Combina a praticidade da chave pública com o desempenho do AES e cifra **dados de qualquer tamanho** com uma chave pública RSA.

```mermaid
flowchart LR
    subgraph Cifrar["Encrypt (chave pública)"]
        D[Dados] --> A1[AES-GCM]
        K[Chave AES aleatória] --> A1
        K --> R1[RSA-OAEP]
        PUB[(Chave pública)] --> R1
        R1 -->|cabeçalho = AAD| A1
    end
    A1 --> OUT["[versão 1 byte][tam. 2 bytes][chave cifrada][dados cifrados]"]
    R1 --> OUT
```

| Método | Descrição |
|---|---|
| `HybridCryptography()` | Usa `AesGcmCryptography` e `RsaCryptography` padrão. |
| `HybridCryptography(ISymmetricCryptography, IAsymmetricCryptography)` | Usa implementações customizadas. |
| `byte[] Encrypt(byte[] data, string publicKeyPem)` | Cifra dados de qualquer tamanho. |
| `byte[] Decrypt(byte[] encryptedData, string privateKeyPem)` | Decifra. |
| `string Encrypt(string plainText, string publicKeyPem)` | Cifra texto e retorna Base64. |
| `string Decrypt(string cipherTextBase64, string privateKeyPem)` | Decifra o Base64. |

```csharp
var hibrida = new HybridCryptography();
var contrato = File.ReadAllText("contrato.xml");                 // 10.000 caracteres

var cifrado = hibrida.Encrypt(contrato, parceiro.PublicKeyPem);  // ok, sem limite de tamanho
var original = hibrida.Decrypt(cifrado, minhaChavePrivada);
```

- Formato: `[versão 1 byte = 0x01][tamanho da chave cifrada 2 bytes][chave AES cifrada com RSA][dados no formato em memória do AES-GCM]`.
- O cabeçalho inteiro (versão, tamanho e chave AES cifrada) é usado como **dado associado** do AES-GCM, vinculando as partes: alterar qualquer byte invalida os dados.
- Qualquer falha (formato, chave RSA ou integridade) gera a mesma `CryptographicException`: *Falha ao descriptografar os dados.* A única exceção é o byte de versão desconhecido (`HybridCryptography.FormatVersion`), informado explicitamente, pois não é secreto.
- ⚠️ Dados cifrados pela versão 0.0.1 (sem byte de versão) não são lidos por esta versão; veja o [CHANGELOG](../CHANGELOG.md).

> ⚠️ A criptografia híbrida garante **sigilo, não autoria**: qualquer pessoa com a chave pública consegue cifrar. Para garantir a origem, **assine** o conteúdo com `RsaCryptography.SignData` usando a chave privada do remetente.

---

## HashHelper

`TEC.Core.Cryptography.Hashing` · `static class`

Hashes SHA-2 e HMAC. Resultados em texto são **hexadecimais minúsculos**.

| Método | Descrição |
|---|---|
| `byte[] ComputeHash(byte[] data, HashAlgorithmType algorithm = Sha256)` | Hash de bytes. |
| `string ComputeHash(string text, HashAlgorithmType algorithm = Sha256)` | Hash de texto UTF-8, em hexadecimal. |
| `Task<string> ComputeHashAsync(Stream stream, HashAlgorithmType algorithm = Sha256, CancellationToken ct = default)` | Hash de um stream, sem carregá-lo em memória. |
| `Task<string> ComputeFileHashAsync(string filePath, HashAlgorithmType algorithm = Sha256, CancellationToken ct = default)` | Hash de um arquivo. |
| `byte[] ComputeHmac(byte[] data, byte[] key, HashAlgorithmType algorithm = Sha256)` | HMAC de bytes. A chave não pode ser vazia. |
| `string ComputeHmac(string text, string key, HashAlgorithmType algorithm = Sha256)` | HMAC de texto, em hexadecimal. Útil para assinar webhooks. |
| `bool FixedTimeEquals(string? hashA, string? hashB)` | Compara em **tempo constante** e de forma **ordinal** (diferencia maiúsculas de minúsculas, essencial para Base64 e tokens). `null` → `false`. |
| `bool FixedTimeEqualsHex(string? hexA, string? hexB)` | Compara dois hashes **hexadecimais** em tempo constante, sem diferenciar maiúsculas de minúsculas (os textos são decodificados para bytes). `null` ou hexadecimal inválido → `false`. |

`HashAlgorithmType`: `Sha256` (0, padrão), `Sha384` (1), `Sha512` (2).

| Chamada | Resultado |
|---|---|
| `HashHelper.ComputeHash("abc")` | `"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"` |
| `HashHelper.ComputeHash("abc", HashAlgorithmType.Sha384)` | `"cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7"` |
| `HashHelper.ComputeHmac("{\"pedido\":123}", "minha-chave-secreta")` | `"149000beef6d309205b79d898c8f96d1f46c0f692c8a5a5b965da8ea13f2b689"` |
| `HashHelper.FixedTimeEquals("ABCDEF", "abcdef")` | `false` (comparação ordinal) |
| `HashHelper.FixedTimeEqualsHex("ABCDEF", "abcdef")` | `true` |
| `HashHelper.FixedTimeEqualsHex("xyz", "xyz")` | `false` (não é hexadecimal) |

### Exemplo: validar webhook

```csharp
app.MapPost("/webhook", async (HttpRequest req, IConfiguration config) =>
{
    var corpo = await new StreamReader(req.Body).ReadToEndAsync();
    var esperado = HashHelper.ComputeHmac(corpo, config["Webhook:Secret"]!);
    var recebido = req.Headers["X-Signature"].ToString();

    // 🔒 nunca compare com ==; FixedTimeEqualsHex aceita a assinatura em maiúsculas ou minúsculas
    return HashHelper.FixedTimeEqualsHex(esperado, recebido)
        ? Results.Ok()
        : Results.Unauthorized();
});
```

---

## Pbkdf2PasswordHasher

`TEC.Core.Cryptography.Hashing` · `sealed class : IPasswordHasher`

Hash de senhas com **PBKDF2-SHA256**, nativo do .NET, sem dependências externas.

| Membro | Descrição |
|---|---|
| `const int DefaultIterations = 600_000` | Recomendação da OWASP para PBKDF2-SHA256. |
| `const int MaxPasswordLength = 1024` | Tamanho máximo de senha aceito. |
| `Pbkdf2PasswordHasher(int iterations = DefaultIterations)` | Aceita de 100.000 a 10.000.000 iterações. |
| `string Hash(string password)` | Gera o hash com salt aleatório de 16 bytes. Senha vazia ou acima do limite → `ArgumentException`. |
| `bool Verify(string password, string? hashedPassword)` | Verifica em tempo constante. Hash `null` (usuário inexistente) ou malformado → executa uma **derivação fictícia de mesmo custo** e retorna `false` (sem exceção), para que o tempo de resposta não revele quais usuários existem. |
| `bool NeedsRehash(string hashedPassword)` | `true` se o hash usa menos iterações que as atuais, tem menos de 32 bytes (ou é inválido). |

### Formato armazenado

```
PBKDF2-SHA256$600000$DwAbB/sXcpdsqzdJ5rU9jg==$gzWSoMtlYusmPYMR2G5nOlk4PAoJf2YZke3tt9m8tFg=
└── algoritmo ┘└ iter ┘└──── salt (Base64) ───┘└──────────── hash (Base64) ─────────────────┘
```

Os parâmetros ficam gravados junto do hash, então é possível **aumentar as iterações no futuro sem invalidar senhas antigas**.

### Exemplo: login com atualização transparente do hash

```csharp
public async Task<Result> LoginAsync(string email, string senha)
{
    var usuario = await _repo.ObterPorEmailAsync(email);

    // 🔒 Verifica SEMPRE, mesmo sem usuário: com hash null o hasher executa uma verificação fictícia de mesmo custo.
    //    Um "usuario is null ||" antes do Verify responderia em ~1 ms para e-mails inexistentes e em ~300 ms para
    //    existentes, revelando pelo tempo de resposta quais contas existem (enumeração de usuários).
    bool senhaValida = _hasher.Verify(senha, usuario?.SenhaHash);

    // 🔒 Mesma mensagem para usuário inexistente e senha errada
    if (usuario is null || !senhaValida)
        throw new UnauthenticatedException();

    if (_hasher.NeedsRehash(usuario.SenhaHash))
    {
        usuario.SenhaHash = _hasher.Hash(senha);   // atualiza para os parâmetros atuais
        await _repo.SalvarAsync(usuario);
    }

    return Result.Success();
}
```

🔒 Proteções:
- As letras acentuadas são normalizadas para a forma composta (NIST SP 800-63B). A normalização é **própria e determinística**: `string.Normalize` não é usado porque não faz nada com `InvariantGlobalization`, o que faria o mesmo hash deixar de validar em servidores diferentes.
- O hash armazenado também é tratado como entrada não confiável: iterações, salt e tamanhos são validados, o que impede que um registro adulterado cause consumo excessivo de CPU (DoS).

---

## SecureRandomGenerator

`TEC.Core.Cryptography.Generators` · `static class`

Valores aleatórios **criptograficamente seguros** (`RandomNumberGenerator`).

| Método | Limites | Descrição |
|---|---|---|
| `string GeneratePassword(int length = 16, bool includeUppercase = true, bool includeLowercase = true, bool includeDigits = true, bool includeSpecial = true)` | tamanho entre nº de grupos e 1024 | Senha com ao menos um caractere de cada grupo selecionado. Exclui caracteres ambíguos (`0 O 1 l I`). |
| `string GenerateToken(int byteLength = 32)` | 16 a 1024 bytes | Token em **Base64Url** (seguro para URLs). |
| `string GenerateNumericCode(int digits = 6)` | 4 a 12 dígitos | Código OTP. Preserva zeros à esquerda. |
| `string GenerateHex(int byteLength = 16)` | 1 a 1024 bytes | String hexadecimal minúscula. |

| Chamada | Exemplo de saída |
|---|---|
| `GeneratePassword()` | `"7G+qw4aK]xCZXK;;"` |
| `GeneratePassword(12, includeSpecial: false)` | `"gGCeR7tLUQ29"` |
| `GenerateToken()` | `"XIY00VGrX4IaM8MluRi1CfFHDjyvV7XHHNpzd6GWXFs"` |
| `GenerateNumericCode()` | `"346924"` |
| `GenerateHex()` | `"93ccfe3df7c0e5c529e890475b72cb94"` |

```csharp
// Link de redefinição de senha: guarde apenas o HASH do token no banco
var token = SecureRandomGenerator.GenerateToken();
await _repo.SalvarTokenAsync(usuario.Id, HashHelper.ComputeHash(token), expiraEm: DateTime.UtcNow.AddMinutes(30));
await _email.EnviarAsync(usuario.Email, $"https://app/redefinir?token={token}");
```

---

## Interfaces

`TEC.Core.Cryptography.Abstractions`: use as interfaces para injeção de dependência e para testes.

| Interface | Implementação padrão | Registrada por `AddTecCore()` |
|---|---|:---:|
| `ISymmetricCryptography` | `AesGcmCryptography` | ✅ |
| `IAsymmetricCryptography` | `RsaCryptography` | ✅ |
| `IHybridCryptography` | `HybridCryptography` | ✅ |
| `IPasswordHasher` | `Pbkdf2PasswordHasher` | ✅ |

```csharp
public class DocumentoService(ISymmetricCryptography crypto, IConfiguration config)
{
    private readonly string _chave = config["Crypto:Key"]
        ?? throw new InvalidConfigurationException("Crypto:Key");

    public string Proteger(string conteudo) => crypto.Encrypt(conteudo, _chave);
}
```
