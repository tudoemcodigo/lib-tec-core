[🏠 TEC.Core](../README.md) › [📚 Documentação](README.md) › Criptografia

# 🔐 Criptografia

> Protege dados (simétrica, assimétrica e híbrida), assina e verifica conteúdo, calcula hashes e HMAC, guarda senhas de forma irreversível e gera valores aleatórios seguros, só com APIs nativas do .NET (`System.Security.Cryptography`).

## 📑 Sumário

- [🎯 Visão geral](#-visão-geral)
  - [Qual algoritmo usar](#qual-algoritmo-usar)
  - [Formatos binários (contrato)](#formatos-binários-contrato)
- [🚀 Uso](#-uso)
  - [AesGcmCryptography](#aesgcmcryptography)
  - [RsaCryptography](#rsacryptography)
  - [RsaKeyPair](#rsakeypair)
  - [RsaKeyPairJsonConverter](#rsakeypairjsonconverter)
  - [RsaSignatureMode](#rsasignaturemode)
  - [HybridCryptography](#hybridcryptography)
  - [HashHelper](#hashhelper)
  - [HashAlgorithmType](#hashalgorithmtype)
  - [Pbkdf2PasswordHasher](#pbkdf2passwordhasher)
  - [SecureRandomGenerator](#securerandomgenerator)
  - [Interfaces](#interfaces)
- [⚙️ Opções](#️-opções)
- [❌ Erros](#-erros)
- [🛡️ Segurança](#️-segurança)
- [❓ Perguntas frequentes](#-perguntas-frequentes)

---

## 🎯 Visão geral

| Namespace | Tipos públicos | Para que serve |
|---|---|---|
| `TEC.Core.Cryptography.Symmetric` | `AesGcmCryptography` | Cifrar dados que a própria aplicação vai decifrar (campos no banco, arquivos, backups) |
| `TEC.Core.Cryptography.Asymmetric` | `RsaCryptography`, `RsaKeyPair`, `RsaKeyPairJsonConverter`, `RsaSignatureMode` | Assinar/verificar e cifrar pequenos segredos com par de chaves |
| `TEC.Core.Cryptography.Hybrid` | `HybridCryptography` | Cifrar dados de qualquer tamanho para outro sistema (chave pública) |
| `TEC.Core.Cryptography.Hashing` | `HashHelper`, `HashAlgorithmType`, `Pbkdf2PasswordHasher` | SHA-2, HMAC, comparação em tempo constante e hash de senha |
| `TEC.Core.Cryptography.Generators` | `SecureRandomGenerator` | Tokens, senhas temporárias, códigos OTP e hexadecimais aleatórios |
| `TEC.Core.Cryptography.Abstractions` | `ISymmetricCryptography`, `IAsymmetricCryptography`, `IHybridCryptography`, `IPasswordHasher` | Contratos para injeção de dependência e testes |

| Classe | Interface | Registrada por `AddTecCore()` |
|---|---|:---:|
| `AesGcmCryptography` | `ISymmetricCryptography` | ✅ Singleton |
| `RsaCryptography` | `IAsymmetricCryptography` | ✅ Singleton (modo de `TecCoreOptions.RsaSignatureMode`, padrão `Pss`) |
| `HybridCryptography` | `IHybridCryptography` | ✅ Singleton (usa as duas implementações acima do container) |
| `Pbkdf2PasswordHasher` | `IPasswordHasher` | ✅ Singleton (iterações de `TecCoreOptions.PasswordHashIterations`) |
| `HashHelper` / `SecureRandomGenerator` | — (estáticas) | — |

Registro e opções: [injeção de dependência](injecao-dependencia.md#addteccore).

### Qual algoritmo usar

```mermaid
flowchart TD
    A{"O que você precisa?"} --> B["Guardar senha de usuário"]
    A --> C["Cifrar dado que EU mesmo vou decifrar"]
    A --> D["Cifrar dado para OUTRO sistema decifrar"]
    A --> E["Provar autoria / integridade"]
    A --> F["Checksum / assinar webhook"]
    A --> G["Token, senha temporária, OTP"]

    B --> B1["Pbkdf2PasswordHasher<br/>(irreversível)"]
    C --> C1["AesGcmCryptography<br/>(mesma chave cifra e decifra)"]
    D --> D0{"Tamanho do dado"}
    D0 -->|"≤ 190 bytes (2048 bits)"| D1["RsaCryptography.Encrypt"]
    D0 -->|"qualquer tamanho"| D2["HybridCryptography"]
    E --> E1["RsaCryptography.SignData / VerifyData"]
    F --> F1["HashHelper.ComputeHash / ComputeHmac"]
    G --> G1["SecureRandomGenerator"]
```

> [!CAUTION]
> 🔒 **Nunca** use criptografia reversível (AES/RSA) para senhas. Use sempre `Pbkdf2PasswordHasher` (ou outra implementação de `IPasswordHasher`).

### Formatos binários (contrato)

Todos os formatos binários começam com um **byte de versão**, para que dados gravados hoje continuem legíveis em versões futuras. Eles fazem parte do contrato público: uma mudança de formato só entra numa versão **MAJOR**.

```text
AES-GCM em memória:  [ versão 1 byte = 0x01 ][ nonce 12 bytes ][ tag 16 bytes ][ dados cifrados ]

AES-GCM em stream:   [ versão 1 byte = 0x01 ][ salt 32 bytes ]
                     [ último? 1 ][ tamanho 4, big-endian ][ tag 16 ][ bloco cifrado ≤ 64 KB ]   ← repetido

Híbrida:             [ versão 1 byte = 0x01 ][ tamanho da chave cifrada 2 bytes, big-endian ]
                     [ chave AES cifrada com RSA-OAEP ][ dados no formato AES-GCM em memória ]

Hash de senha:       PBKDF2-SHA256$<iterações>$<salt Base64>$<hash Base64>
```

| Formato | Tamanho resultante |
|---|---|
| AES-GCM em memória | `29 + tamanho dos dados` bytes |
| AES-GCM em stream | 33 bytes de cabeçalho + 21 bytes por bloco de até 64 KB (200.000 bytes → 4 blocos → 200.117 bytes); stream vazio gera um único bloco final vazio |
| Híbrida | `3 + tamanho da chave RSA em bytes + 29 + tamanho dos dados` (256 bytes de chave cifrada com RSA 2048) |

---

## 🚀 Uso

### AesGcmCryptography

> `TEC.Core.Cryptography.Symmetric` · `sealed class` (implementa `ISymmetricCryptography`)

Criptografia simétrica **autenticada** com AES-GCM: garante sigilo **e** integridade. Qualquer alteração no texto cifrado (ou nos dados associados) é detectada na decifragem. Nonce de 12 bytes e tag de 16 bytes. **Quando usar:** campos sigilosos no banco, arquivos e backups.

| Membro | Retorno | Descrição |
|---|---|---|
| `const FormatVersion = 1` | `byte` | Byte de versão gravado no início do formato em memória e do formato em stream. |
| `GenerateKey()` | `byte[]` | Chave aleatória de 32 bytes (AES-256). |
| `GenerateKeyBase64()` | `string` | Chave em Base64 (44 caracteres); os bytes intermediários são zerados. |
| `Encrypt(byte[] plainData, byte[] key, byte[]? associatedData = null)` | `byte[]` | Cifra bytes com nonce aleatório. Chave de 16, 24 ou 32 bytes. |
| `Decrypt(byte[] encryptedData, byte[] key, byte[]? associatedData = null)` | `byte[]` | Decifra. Falha se a chave estiver errada ou se os dados ou o `associatedData` tiverem sido alterados. |
| `Encrypt(string plainText, string base64Key)` | `string` | Cifra texto UTF-8 e retorna Base64. Zera a chave e o texto em bytes após o uso. |
| `Decrypt(string cipherTextBase64, string base64Key)` | `string` | Decifra o Base64 gerado por `Encrypt(string, string)`. |
| `EncryptAsync(Stream input, Stream output, byte[] key, CancellationToken cancellationToken = default)` | `Task` | Cifra um stream em blocos de 64 KB, sem carregar tudo em memória. |
| `DecryptAsync(Stream input, Stream output, byte[] key, CancellationToken cancellationToken = default)` | `Task` | Decifra um stream gerado por `EncryptAsync`. |

Como funciona:

- O **byte de versão** (`FormatVersion`) permite evoluir o formato sem ambiguidade. No formato em memória ele também é autenticado (entra no dado associado, antes do `associatedData` do chamador). Uma versão desconhecida gera `CryptographicException` com mensagem explícita (a versão não é segredo).
- Cada stream usa uma **subchave própria** (HKDF-SHA256 com a chave + salt aleatório de 32 bytes). O nonce de cada bloco é o contador do bloco, o que é seguro porque a subchave é única por stream.
- Cada bloco é autenticado com o cabeçalho do stream e a marcação de "último bloco"; com o contador no nonce, isso impede **reordenação, truncamento e dados extras** depois do bloco final.

```csharp
using TEC.Core.Cryptography.Symmetric;

var aes = new AesGcmCryptography();

// Texto
string key = aes.GenerateKeyBase64();            // ex.: "MvBSvYickCZ+YJffelkX/1yWCeyRewn7UbA9GbJn3rs="; guarde num cofre
string cipherText = aes.Encrypt("Dado sigiloso", key);
string plainText = aes.Decrypt(cipherText, key); // "Dado sigiloso"

// Dados associados (AAD): autenticados, mas não cifrados.
// Vinculam o texto cifrado a um contexto; decifrar em outro contexto falha.
byte[] rawKey = aes.GenerateKey();
byte[] data = "conteúdo"u8.ToArray();
byte[] encrypted = aes.Encrypt(data, rawKey, "customer:42"u8.ToArray());
aes.Decrypt(encrypted, rawKey, "customer:43"u8.ToArray());   // AuthenticationTagMismatchException

// Arquivos grandes (streaming, blocos de 64 KB)
await using (var input = File.OpenRead("backup.zip"))
await using (var output = File.Create("backup.zip.enc"))
    await aes.EncryptAsync(input, output, rawKey);

await using (var input = File.OpenRead("backup.zip.enc"))
await using (var output = File.Create("backup.zip"))
    await aes.DecryptAsync(input, output, rawKey);
```

<details>
<summary>📄 Exemplo completo: serviço com injeção de dependência</summary>

```csharp
using Microsoft.Extensions.Configuration;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Exceptions;

public sealed class DocumentService(ISymmetricCryptography crypto, IConfiguration config)
{
    // A chave vem de um cofre de segredos (ex.: TEC.Vault), nunca do appsettings versionado
    private readonly string _key = config["Crypto:Key"]
        ?? throw new InvalidConfigurationException("Crypto:Key");

    public string Protect(string content) => crypto.Encrypt(content, _key);

    public string Unprotect(string cipherText) => crypto.Decrypt(cipherText, _key);

    // Arquivos grandes, em blocos de 64 KB; grava num temporário e só publica após sucesso
    public async Task DecryptFileAsync(string source, string destination, byte[] key, CancellationToken ct)
    {
        string temp = destination + ".tmp";
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = File.Create(temp))
                await crypto.DecryptAsync(input, output, key, ct);

            File.Move(temp, destination, overwrite: true);
        }
        catch
        {
            File.Delete(temp);   // descarta a saída parcial
            throw;
        }
    }
}
```

</details>

> [!WARNING]
> Em `DecryptAsync`, cada bloco é verificado antes de ser gravado, mas o truncamento só é detectado no final. **Se o método lançar exceção, descarte tudo o que já foi gravado em `output`.**

### RsaCryptography

> `TEC.Core.Cryptography.Asymmetric` · `sealed class` (implementa `IAsymmetricCryptography`)

Criptografia assimétrica RSA: a **chave pública** cifra e verifica; a **chave privada** decifra e assina. **Quando usar:** integração entre sistemas, prova de autoria e envio de pequenos segredos a parceiros.

| Configuração | Valor |
|---|---|
| Cifragem | OAEP com SHA-256 |
| Assinatura | SHA-256 + **PSS** (padrão) ou PKCS#1 v1.5 |
| Formato das chaves geradas | PEM: pública `SubjectPublicKeyInfo` (`PUBLIC KEY`), privada `PKCS#8` (`PRIVATE KEY`) |
| Formato aceito na importação | Qualquer PEM RSA lido por `RSA.ImportFromPem` (inclusive `RSA PRIVATE KEY`/`RSA PUBLIC KEY`); operações com a chave privada exigem rótulo com `PRIVATE` |
| Tamanho mínimo (geração e importação) | `MinimumKeySize = 2048` bits |
| Tamanho máximo (geração) | `MaximumKeySize = 8192` bits |
| Tamanho máximo (importação) | `MaximumImportKeySize = 16384` bits; PEM acima de 32 KB é recusado antes da interpretação (proteção contra DoS com chaves enormes) |

| Membro | Retorno | Descrição |
|---|---|---|
| `const MinimumKeySize = 2048` | `int` | Menor tamanho de chave aceito na geração e na importação. |
| `const MaximumKeySize = 8192` | `int` | Maior tamanho de chave aceito na geração. |
| `const MaximumImportKeySize = 16384` | `int` | Maior tamanho de chave aceito na importação. |
| `RsaCryptography(RsaSignatureMode signatureMode = RsaSignatureMode.Pss)` | — | Use `Pkcs1` apenas para compatibilidade com sistemas legados (ex.: JWT RS256). |
| `GenerateKeyPair(int keySizeInBits = 2048)` | `RsaKeyPair` | Par de chaves PEM. Tamanho múltiplo de 8, entre 2048 e 8192. |
| `Encrypt(byte[] data, string publicKeyPem)` | `byte[]` | Cifra com a chave pública. Tamanho máximo: `GetMaxDataLength(keySize)`. |
| `Decrypt(byte[] encryptedData, string privateKeyPem)` | `byte[]` | Decifra com a chave privada. |
| `Encrypt(string plainText, string publicKeyPem)` | `string` | Cifra texto UTF-8 e retorna Base64. |
| `Decrypt(string cipherTextBase64, string privateKeyPem)` | `string` | Decifra o Base64. |
| `SignData(byte[] data, string privateKeyPem)` | `byte[]` | Assina com a chave privada. |
| `SignData(string data, string privateKeyPem)` | `string` | Assina texto UTF-8 e retorna a assinatura em Base64. |
| `VerifyData(byte[] data, byte[] signature, string publicKeyPem)` | `bool` | Verifica a assinatura. Assinatura com tamanho diferente do módulo da chave → `false`. |
| `VerifyData(string data, string signatureBase64, string publicKeyPem)` | `bool` | Verifica a assinatura Base64. Assinatura vazia, Base64 inválido ou longo demais → `false`. |
| `static GetMaxDataLength(int keySizeInBits)` | `int` | Tamanho máximo cifrável com OAEP-SHA256: `keySize/8 − 66`. |

| Tamanho da chave | `GetMaxDataLength` | Assinatura/cifra em Base64 |
|:---:|:---:|:---:|
| 2048 | 190 bytes | 344 caracteres |
| 3072 | 318 bytes | 512 caracteres |
| 4096 | 446 bytes | 684 caracteres |

```csharp
using TEC.Core.Cryptography.Asymmetric;

var rsa = new RsaCryptography();
RsaKeyPair keys = rsa.GenerateKeyPair();                        // 2048 bits, PEM

// Cifrar pequenos segredos
string cipherText = rsa.Encrypt("segredo", keys.PublicKeyPem);
string plainText = rsa.Decrypt(cipherText, keys.PrivateKeyPem); // "segredo"

// Assinar e verificar
string signature = rsa.SignData("payload", keys.PrivateKeyPem);
rsa.VerifyData("payload", signature, keys.PublicKeyPem);        // true
rsa.VerifyData("payload2", signature, keys.PublicKeyPem);       // false

// Integração com sistema legado (PKCS#1 v1.5)
var legacyRsa = new RsaCryptography(RsaSignatureMode.Pkcs1);
```

<details>
<summary>📄 Exemplo completo: troca de contrato com parceiro (sigilo + autoria)</summary>

```csharp
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Exceptions;

public sealed record SignedContract(string EncryptedContent, string Signature);

public sealed class PartnerIntegration(IAsymmetricCryptography rsa, IHybridCryptography hybrid)
{
    // Cifra com a chave pública do parceiro (sigilo) e assina com a minha chave privada (autoria)
    public SignedContract Send(string contractXml, string myPrivateKeyPem, string partnerPublicKeyPem)
    {
        string encrypted = hybrid.Encrypt(contractXml, partnerPublicKeyPem);
        return new SignedContract(encrypted, rsa.SignData(encrypted, myPrivateKeyPem));
    }

    public string Receive(SignedContract received, string partnerPublicKeyPem, string myPrivateKeyPem)
    {
        if (!rsa.VerifyData(received.EncryptedContent, received.Signature, partnerPublicKeyPem))
            throw new BusinessException("ASSINATURA_INVALIDA", "Assinatura do parceiro inválida.");

        return hybrid.Decrypt(received.EncryptedContent, myPrivateKeyPem);
    }
}
```

</details>

> [!WARNING]
> `RsaCryptography.Encrypt` cifra no máximo `GetMaxDataLength(keySize)` bytes (190 com 2048 bits); para mais, use [`HybridCryptography`](#hybridcryptography).

### RsaKeyPair

> `TEC.Core.Cryptography.Asymmetric` · `sealed record`

Par de chaves PEM gerado por `RsaCryptography.GenerateKeyPair`.

```csharp
[JsonConverter(typeof(RsaKeyPairJsonConverter))]
public sealed record RsaKeyPair(string PublicKeyPem, string PrivateKeyPem);
```

| Membro | Tipo | Descrição |
|---|---|---|
| `RsaKeyPair(string PublicKeyPem, string PrivateKeyPem)` | — | As duas chaves são obrigatórias: nulas, vazias ou só com espaços lançam exceção (também em `with`). |
| `PublicKeyPem` | `string` | Chave pública (`-----BEGIN PUBLIC KEY-----`). Pode ser distribuída. |
| `PrivateKeyPem` | `string` | Chave privada (`-----BEGIN PRIVATE KEY-----`). Guarde num cofre de segredos. 🔒 Nunca vai para o JSON. |
| `ToString()` | `string` | 🔒 Oculta a chave privada: `RsaKeyPair { PublicKeyPem = ..., PrivateKeyPem = *** }`. |

```csharp
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Asymmetric;

RsaKeyPair keys = new RsaCryptography().GenerateKeyPair(3072);
Console.WriteLine(keys);           // a chave privada aparece como ***
string json = keys.ToJson();       // {"publicKeyPem":"-----BEGIN PUBLIC KEY-----..."}

// Recriar o par a partir do cofre
var restored = new RsaKeyPair(publicKeyFromVault, privateKeyFromVault);
```

### RsaKeyPairJsonConverter

> `TEC.Core.Cryptography.Asymmetric` · `sealed class` (`JsonConverter<RsaKeyPair>`)

Conversor aplicado a `RsaKeyPair` pelo atributo `[JsonConverter]`. Grava **só a chave pública** e recusa a leitura. Não usa reflexão: funciona com `JsonSerializerContext` e Native AOT.

| Membro | Retorno | Descrição |
|---|---|---|
| `Write(Utf8JsonWriter writer, RsaKeyPair value, JsonSerializerOptions options)` | `void` | Grava `{"PublicKeyPem":"..."}`, com o nome convertido pela `PropertyNamingPolicy` das opções (camelCase em `JsonDefaults`: `publicKeyPem`). |
| `Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)` | `RsaKeyPair` | Sempre lança `JsonException`: sem a chave privada, o par não pode ser reconstruído. |

```csharp
string json = keys.ToJson();                            // só a chave pública
bool ok = json.TryFromJson<RsaKeyPair>(out _);          // false
```

### RsaSignatureMode

> `TEC.Core.Cryptography.Asymmetric` · `enum`

| Membro | Valor | Descrição |
|---|:---:|---|
| `Pss` | 0 | RSASSA-PSS. Padrão recomendado, com prova de segurança. |
| `Pkcs1` | 1 | PKCS#1 v1.5. Apenas para compatibilidade com sistemas legados (ex.: validar JWT RS256 de outro sistema). |

No container, o modo vem de `TecCoreOptions.RsaSignatureMode`:

```csharp
builder.Services.AddTecCore(options => options.RsaSignatureMode = RsaSignatureMode.Pkcs1);   // só para legado
```

> [!NOTE]
> Uma assinatura PSS verificada por uma instância `Pkcs1` (ou o contrário) retorna `false`.

### HybridCryptography

> `TEC.Core.Cryptography.Hybrid` · `sealed class` (implementa `IHybridCryptography`)

Cifra **dados de qualquer tamanho** com uma chave pública RSA: gera uma chave AES aleatória, cifra os dados com AES-GCM e a chave AES com RSA-OAEP. O cabeçalho inteiro (versão, tamanho e chave AES cifrada) é usado como **dado associado** do AES-GCM, vinculando as partes: alterar qualquer byte invalida os dados.

```mermaid
flowchart LR
    subgraph Cifrar["Encrypt (chave pública)"]
        D["Dados"] --> A1["AES-GCM"]
        K["Chave AES aleatória"] --> A1
        K --> R1["RSA-OAEP"]
        PUB[("Chave pública")] --> R1
        R1 -->|"cabeçalho = AAD"| A1
    end
    A1 --> OUT["versão · tamanho · chave cifrada · dados cifrados"]
    R1 --> OUT
```

| Membro | Retorno | Descrição |
|---|---|---|
| `const FormatVersion = 1` | `byte` | Byte de versão gravado no início do formato híbrido. |
| `HybridCryptography()` | — | Usa `AesGcmCryptography` e `RsaCryptography` padrão (PSS). |
| `HybridCryptography(ISymmetricCryptography symmetric, IAsymmetricCryptography asymmetric)` | — | Usa implementações customizadas. |
| `Encrypt(byte[] data, string publicKeyPem)` | `byte[]` | Cifra dados de qualquer tamanho. A chave AES é zerada após o uso. |
| `Decrypt(byte[] encryptedData, string privateKeyPem)` | `byte[]` | Decifra. |
| `Encrypt(string plainText, string publicKeyPem)` | `string` | Cifra texto UTF-8 e retorna Base64. |
| `Decrypt(string cipherTextBase64, string privateKeyPem)` | `string` | Decifra o Base64. |

```csharp
using TEC.Core.Cryptography.Hybrid;

var hybrid = new HybridCryptography();
string contract = File.ReadAllText("contrato.xml");                     // ex.: 10.000 caracteres

string encrypted = hybrid.Encrypt(contract, partnerPublicKeyPem);        // sem limite de tamanho
string original = hybrid.Decrypt(encrypted, myPrivateKeyPem);
```

> [!WARNING]
> A criptografia híbrida garante **sigilo, não autoria**: qualquer pessoa com a chave pública consegue cifrar. Para garantir a origem, **assine** o conteúdo com `RsaCryptography.SignData` usando a chave privada do remetente (veja o [exemplo completo](#rsacryptography)).

### HashHelper

> `TEC.Core.Cryptography.Hashing` · `static class`

Hashes SHA-2, HMAC e comparação em tempo constante. Resultados em texto são **hexadecimais minúsculos**. **Quando usar:** checksums, webhooks e tokens de redefinição de senha (para senhas, use [`Pbkdf2PasswordHasher`](#pbkdf2passwordhasher), nunca SHA puro).

| Membro | Retorno | Descrição |
|---|---|---|
| `ComputeHash(byte[] data, HashAlgorithmType algorithm = Sha256)` | `byte[]` | Hash de bytes. |
| `ComputeHash(string text, HashAlgorithmType algorithm = Sha256)` | `string` | Hash de texto UTF-8, em hexadecimal. |
| `ComputeHashAsync(Stream stream, HashAlgorithmType algorithm = Sha256, CancellationToken cancellationToken = default)` | `Task<string>` | Hash de um stream, sem carregá-lo em memória. |
| `ComputeFileHashAsync(string filePath, HashAlgorithmType algorithm = Sha256, CancellationToken cancellationToken = default)` | `Task<string>` | Hash de um arquivo. |
| `ComputeHmac(byte[] data, byte[] key, HashAlgorithmType algorithm = Sha256)` | `byte[]` | HMAC de bytes. A chave não pode ser vazia. |
| `ComputeHmac(string text, string key, HashAlgorithmType algorithm = Sha256)` | `string` | HMAC de texto UTF-8, em hexadecimal. Útil para assinar webhooks. Zera os bytes intermediários da chave. |
| `FixedTimeEquals(string? hashA, string? hashB)` | `bool` | Compara em **tempo constante** e de forma **ordinal** (diferencia maiúsculas, essencial para Base64 e tokens). `null` → `false`. O tamanho dos textos não é protegido. |
| `FixedTimeEqualsHex(string? hexA, string? hexB)` | `bool` | Compara dois valores **hexadecimais** em tempo constante, sem diferenciar maiúsculas (os textos são decodificados para bytes). `null` ou hexadecimal inválido → `false`. |

| Chamada | Resultado |
|---|---|
| `HashHelper.ComputeHash("abc")` | `"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"` |
| `HashHelper.ComputeHash("abc", HashAlgorithmType.Sha384)` | `"cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7"` |
| `HashHelper.ComputeHmac("{\"pedido\":123}", "minha-chave-secreta")` | `"149000beef6d309205b79d898c8f96d1f46c0f692c8a5a5b965da8ea13f2b689"` |
| `HashHelper.FixedTimeEquals("ABCDEF", "abcdef")` | `false` (comparação ordinal) |
| `HashHelper.FixedTimeEqualsHex("ABCDEF", "abcdef")` | `true` |
| `HashHelper.FixedTimeEqualsHex("xyz", "xyz")` | `false` (não é hexadecimal) |

```csharp
// Validar webhook (ASP.NET Core)
app.MapPost("/webhook", async (HttpRequest request, IConfiguration config) =>
{
    string body = await new StreamReader(request.Body).ReadToEndAsync();
    string expected = HashHelper.ComputeHmac(body, config["Webhook:Secret"]!);
    string received = request.Headers["X-Signature"].ToString();

    // 🔒 nunca compare com ==; FixedTimeEqualsHex aceita a assinatura em maiúsculas ou minúsculas
    return HashHelper.FixedTimeEqualsHex(expected, received) ? Results.Ok() : Results.Unauthorized();
});
```

<details>
<summary>📄 Exemplo completo: webhook e token de redefinição de senha</summary>

```csharp
using TEC.Core.Cryptography.Generators;
using TEC.Core.Cryptography.Hashing;

public static class Webhook
{
    public static bool IsSignatureValid(string body, string? receivedSignature, string secret)
    {
        if (string.IsNullOrEmpty(receivedSignature))
            return false;

        string expected = HashHelper.ComputeHmac(body, secret, HashAlgorithmType.Sha256);
        return HashHelper.FixedTimeEqualsHex(expected, receivedSignature);   // tempo constante, sem diferenciar maiúsculas
    }

    // Link de redefinição de senha: o token vai no e-mail; no banco, só o hash
    public static (string Token, string HashForDatabase) NewResetToken()
    {
        string token = SecureRandomGenerator.GenerateToken(32);
        return (token, HashHelper.ComputeHash(token));
    }
}
```

</details>

### HashAlgorithmType

> `TEC.Core.Cryptography.Hashing` · `enum`

| Membro | Valor | Descrição |
|---|:---:|---|
| `Sha256` | 0 | SHA-256 / HMAC-SHA256 (padrão). |
| `Sha384` | 1 | SHA-384 / HMAC-SHA384. |
| `Sha512` | 2 | SHA-512 / HMAC-SHA512. |

```csharp
string sha512 = await HashHelper.ComputeFileHashAsync("backup.zip", HashAlgorithmType.Sha512, ct);
```

### Pbkdf2PasswordHasher

> `TEC.Core.Cryptography.Hashing` · `sealed class` (implementa `IPasswordHasher`)

Hash de senhas com **PBKDF2-SHA256**, nativo do .NET, com salt aleatório de 16 bytes e hash de 32 bytes. **Quando usar:** cadastro e login de usuários.

| Membro | Retorno | Descrição |
|---|---|---|
| `const DefaultIterations = 600_000` | `int` | Recomendação da OWASP para PBKDF2-SHA256. |
| `const MaxPasswordLength = 1024` | `int` | Tamanho máximo de senha aceito (caracteres). |
| `Pbkdf2PasswordHasher(int iterations = DefaultIterations)` | — | Aceita de 100.000 a 10.000.000 iterações. |
| `Hash(string password)` | `string` | Gera o hash com salt aleatório de 16 bytes. |
| `Verify(string password, string? hashedPassword)` | `bool` | Verifica em tempo constante. Hash `null` (usuário inexistente) ou malformado → executa uma **derivação fictícia de mesmo custo** e retorna `false` (sem exceção), para que o tempo de resposta não revele quais usuários existem. Senha vazia ou acima de `MaxPasswordLength` → `false`. |
| `NeedsRehash(string hashedPassword)` | `bool` | `true` se o hash é inválido, usa menos iterações que as da instância ou tem menos de 32 bytes. |

```text
PBKDF2-SHA256$600000$DwAbB/sXcpdsqzdJ5rU9jg==$gzWSoMtlYusmPYMR2G5nOlk4PAoJf2YZke3tt9m8tFg=
└── algoritmo ┘└ iter ┘└──── salt (Base64) ───┘└──────────── hash (Base64) ─────────────────┘
```

Os parâmetros ficam gravados junto do hash: é possível **aumentar as iterações no futuro sem invalidar senhas antigas** (`NeedsRehash` + novo `Hash` no próximo login). Na leitura, o hash armazenado é validado: até 256 caracteres, 4 partes, prefixo `PBKDF2-SHA256`, iterações entre 100.000 e 10.000.000, salt e hash entre 16 e 64 bytes.

```csharp
using TEC.Core.Cryptography.Hashing;

var hasher = new Pbkdf2PasswordHasher();
string hash = hasher.Hash("Senha@123");        // "PBKDF2-SHA256$600000$<salt>$<hash>"
bool ok = hasher.Verify("Senha@123", hash);    // true
bool no = hasher.Verify("Senha@123", null);    // false, com o mesmo custo (usuário inexistente)
```

<details>
<summary>📄 Exemplo completo: login com atualização transparente do hash</summary>

```csharp
using TEC.Core.Common.Results;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Exceptions;

public sealed class LoginService(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<Result> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email, ct);

        // 🔒 Verifica SEMPRE, mesmo sem usuário: com hash null o hasher executa uma verificação fictícia de mesmo custo.
        //    Um "user is null ||" antes do Verify responderia em ~1 ms para e-mails inexistentes e em ~300 ms para
        //    existentes, revelando pelo tempo de resposta quais contas existem (enumeração de usuários).
        bool passwordValid = hasher.Verify(password, user?.PasswordHash);

        // 🔒 Mesma mensagem para usuário inexistente e senha errada
        if (user is null || !passwordValid)
            throw new UnauthenticatedException();

        if (hasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = hasher.Hash(password);   // atualiza para os parâmetros atuais
            await users.SaveAsync(user, ct);
        }

        return Result.Success();
    }
}
```

</details>

### SecureRandomGenerator

> `TEC.Core.Cryptography.Generators` · `static class`

Valores aleatórios **criptograficamente seguros** (`RandomNumberGenerator`). **Quando usar:** links de redefinição de senha, chaves de API, senhas temporárias e códigos OTP.

| Membro | Retorno | Descrição |
|---|---|---|
| `GeneratePassword(int length = 16, bool includeUppercase = true, bool includeLowercase = true, bool includeDigits = true, bool includeSpecial = true)` | `string` | Senha com ao menos um caractere de cada grupo selecionado, embaralhada. Exclui caracteres ambíguos (`0 O 1 l I`). Tamanho entre o nº de grupos selecionados e 1024. Especiais: `!@#$%&*()-_=+[]{};:,.?`. |
| `GenerateToken(int byteLength = 32)` | `string` | Token em **Base64Url** sem preenchimento (seguro para URLs, sem `=`), gerado por [`Base64UrlEncoder`](texto.md#base64urlencoder). 16 a 1024 bytes. |
| `GenerateNumericCode(int digits = 6)` | `string` | Código numérico (OTP) com dígitos `0`–`9`. Preserva zeros à esquerda. 4 a 12 dígitos. |
| `GenerateHex(int byteLength = 16)` | `string` | Texto hexadecimal minúsculo. 1 a 1024 bytes. |

| Chamada | Exemplo de saída |
|---|---|
| `GeneratePassword()` | `"7G+qw4aK]xCZXK;;"` |
| `GeneratePassword(12, includeSpecial: false)` | `"gGCeR7tLUQ29"` |
| `GenerateToken()` | `"XIY00VGrX4IaM8MluRi1CfFHDjyvV7XHHNpzd6GWXFs"` |
| `GenerateNumericCode()` | `"346924"` |
| `GenerateHex()` | `"93ccfe3df7c0e5c529e890475b72cb94"` |

```csharp
// Link de redefinição de senha: guarde apenas o HASH do token no banco
string token = SecureRandomGenerator.GenerateToken();
await tokens.SaveAsync(user.Id, HashHelper.ComputeHash(token), expiresAt: DateTime.UtcNow.AddMinutes(30));
await email.SendAsync(user.Email, $"https://<sua-aplicacao>/redefinir?token={token}");
```

> [!TIP]
> Para validar ou decodificar um token Base64Url recebido (sem lançar exceção), use [`Base64UrlEncoder.TryDecode`](texto.md#base64urlencoder).

### Interfaces

> `TEC.Core.Cryptography.Abstractions` · `interface`

Injete as interfaces em vez das classes concretas (facilita testes e troca de implementação). Os erros dependem da implementação; os das padrão estão nas seções acima.

| Interface | Implementação padrão | Membros |
|---|---|---|
| `ISymmetricCryptography` | [`AesGcmCryptography`](#aesgcmcryptography) | `GenerateKey()`, `GenerateKeyBase64()`, `Encrypt`/`Decrypt` (bytes com `associatedData` opcional; texto com chave Base64), `EncryptAsync`/`DecryptAsync` (streams) |
| `IAsymmetricCryptography` | [`RsaCryptography`](#rsacryptography) | `GenerateKeyPair(int keySizeInBits = 2048)`, `Encrypt`/`Decrypt` (bytes e texto Base64), `SignData`/`VerifyData` (bytes e texto Base64) |
| `IHybridCryptography` | [`HybridCryptography`](#hybridcryptography) | `Encrypt`/`Decrypt` (bytes e texto Base64) |
| `IPasswordHasher` | [`Pbkdf2PasswordHasher`](#pbkdf2passwordhasher) | `Hash(string)`, `Verify(string, string?)`, `NeedsRehash(string)` |

```csharp
public sealed class ReceiptService(IAsymmetricCryptography rsa)
{
    public string Sign(string receipt, string privateKeyPem) => rsa.SignData(receipt, privateKeyPem);
}

public sealed class PartnerExport(IHybridCryptography hybrid)
{
    public byte[] Pack(byte[] file, string partnerPublicKeyPem) => hybrid.Encrypt(file, partnerPublicKeyPem);
}
```

> [!WARNING]
> Contrato de `IPasswordHasher.Verify`: com `hashedPassword` nulo (usuário inexistente) ou malformado, a implementação **deve** executar uma verificação fictícia de custo equivalente e retornar `false`. Implementações próprias que não seguirem isso deixam o tempo de resposta do login revelar quais usuários existem.

---

## ⚙️ Opções

| Opção / parâmetro | Padrão | Descrição |
|---|---|---|
| Tamanho da chave AES | 32 bytes (AES-256) | `GenerateKey`; `Encrypt`/`Decrypt` aceitam 16, 24 ou 32 bytes |
| `associatedData` | `null` | Dado autenticado, não cifrado, que vincula o texto cifrado a um contexto |
| `AesGcmCryptography.FormatVersion` / `HybridCryptography.FormatVersion` | `1` | Byte de versão no início de todo dado cifrado |
| Bloco do stream AES-GCM | 64 KB | Fixo |
| `RsaCryptography(signatureMode)` · `TecCoreOptions.RsaSignatureMode` | `RsaSignatureMode.Pss` | `Pkcs1` só para sistemas legados |
| `GenerateKeyPair(keySizeInBits)` | 2048 | Entre 2048 e 8192, múltiplo de 8 |
| `RsaCryptography.MaximumImportKeySize` | 16384 | Maior chave aceita na importação (PEM até 32 KB) |
| `Pbkdf2PasswordHasher(iterations)` · `TecCoreOptions.PasswordHashIterations` | 600.000 (`DefaultIterations`) | De 100.000 a 10.000.000 |
| `Pbkdf2PasswordHasher.MaxPasswordLength` | 1024 | Tamanho máximo da senha |
| `HashAlgorithmType` | `Sha256` | `Sha384` e `Sha512` disponíveis |
| `SecureRandomGenerator.GenerateToken(byteLength)` | 32 | 16 a 1024 bytes |
| `GenerateNumericCode(digits)` | 6 | 4 a 12 dígitos |
| `GeneratePassword(length)` | 16 | Nº de grupos selecionados a 1024 |
| `GenerateHex(byteLength)` | 16 | 1 a 1024 bytes |

```csharp
// No container (detalhes em injecao-dependencia.md)
builder.Services.AddTecCore(options =>
{
    options.PasswordHashIterations = 800_000;
    options.RsaSignatureMode = RsaSignatureMode.Pss;
});
```

---

## ❌ Erros

| Exceção | Onde / quando ocorre | Mensagem / o que fazer |
|---|---|---|
| `ArgumentNullException` | Qualquer argumento nulo (dados, chave, PEM, stream, implementação) | Valide a entrada antes da chamada |
| `ArgumentException` | Chave AES `byte[]` com tamanho diferente de 16/24/32 bytes | *A chave AES deve ter 16, 24 ou 32 bytes.* |
| `ArgumentException` | Chave AES em Base64 vazia ou inválida | *A chave deve estar em Base64.* |
| `ArgumentException` | Chave AES em Base64 com tamanho errado | *A chave AES deve ter 16, 24 ou 32 bytes (Base64 de 24, 32 ou 44 caracteres).* |
| `ArgumentException` | Stream de entrada sem leitura / saída sem escrita / mesmo stream | *O stream de entrada não permite leitura.* · *O stream de saída não permite escrita.* · *Os streams de entrada e saída devem ser diferentes.* |
| `AuthenticationTagMismatchException` (`CryptographicException`) | AES-GCM com chave errada ou dados/AAD adulterados | Confira a chave e o contexto (`associatedData`) |
| `CryptographicException` | AES-GCM: formato inválido, truncado ou Base64 do texto cifrado inválido | *Dados criptografados inválidos ou corrompidos.* |
| `CryptographicException` | AES-GCM ou híbrida: byte de versão desconhecido | *Versão de formato dos dados criptografados não suportada: {v} (esperado 1). Formato não suportado: byte de versão ausente ou desconhecido.* |
| `CryptographicException` | RSA ou híbrida: falha ao decifrar (chave errada, dado corrompido/truncado, Base64 inválido, adulteração) | Mensagem única e genérica: *Falha ao descriptografar os dados.* |
| `ArgumentOutOfRangeException` | `RsaCryptography` com `signatureMode` fora do enum | No construtor |
| `ArgumentOutOfRangeException` | `GenerateKeyPair` fora de 2048–8192 ou não múltiplo de 8 | *O tamanho da chave deve ser múltiplo de 8, entre 2048 e 8192 bits.* |
| `ArgumentException` | `RsaCryptography.Encrypt` com dados acima do limite da chave | *Para esta chave o tamanho máximo é 190 bytes. Para dados maiores use HybridCryptography.* |
| `ArgumentException` | Assinar/decifrar com PEM sem rótulo `PRIVATE` | *A operação exige a chave privada (PEM "PRIVATE KEY").* |
| `ArgumentException` | PEM inválido | *Chave PEM inválida.* |
| `ArgumentException` | PEM acima de 32 KB | *Chave PEM excede o tamanho máximo aceito (16384 bits).* |
| `ArgumentException` | Chave importada < 2048 bits / > 16384 bits | *Chaves RSA com menos de 2048 bits não são aceitas.* · *Chaves RSA com mais de 16384 bits não são aceitas.* |
| `ArgumentException` / `ArgumentNullException` | `RsaKeyPair` com chave nula, vazia ou só espaços (construtor ou `with`) | Informe as duas chaves PEM |
| `JsonException` | Qualquer desserialização de `RsaKeyPair` (`FromJson`, `JsonSerializer.Deserialize`) | *RsaKeyPair não pode ser desserializado de JSON: a chave privada (PrivateKeyPem) não é serializada, por segurança…* `TryFromJson` retorna `false` |
| `ArgumentException` | `HashHelper.ComputeHmac` com chave vazia | *A chave do HMAC não pode ser vazia.* |
| `ArgumentException` | `ComputeHashAsync` com stream sem leitura; `ComputeFileHashAsync` com caminho vazio | *O stream não permite leitura.* |
| `ArgumentOutOfRangeException` | `HashAlgorithmType` fora do enum | — |
| `FileNotFoundException` | `ComputeFileHashAsync` com arquivo inexistente | — |
| `ArgumentOutOfRangeException` | `Pbkdf2PasswordHasher` com iterações fora de 100.000–10.000.000; `AddTecCore` com `PasswordHashIterations` inválido (na subida) | *O valor deve estar entre 100000 e 10000000.* |
| `ArgumentException` | `Hash` com senha vazia / acima de 1024 caracteres | *A senha não pode ser vazia.* · *A senha deve ter no máximo 1024 caracteres.* |
| — | `Verify` com hash nulo, malformado ou adulterado | Retorna `false`, sem exceção |
| `ArgumentOutOfRangeException` | `SecureRandomGenerator` com tamanho fora dos limites | *O valor deve estar entre {min} e {max}.* |
| `ArgumentException` | `GeneratePassword` sem nenhum grupo selecionado | *Selecione ao menos um grupo de caracteres.* |

---

## 🛡️ Segurança

| Área | Garantia |
|---|---|
| AES-GCM | Cifra autenticada; byte de versão de formato autenticado; subchave por stream (HKDF); detecta adulteração, reordenação, truncamento e dados extras; mensagens de erro genéricas |
| RSA | OAEP-SHA256; assinatura PSS por padrão; chaves < 2048 bits recusadas (inclusive na importação); geração limitada a 8192 bits e importação a 16384 bits (PEM até 32 KB) |
| Híbrida | Byte de versão; cabeçalho com a chave cifrada vinculado ao conteúdo (AAD); falhas geram uma exceção única (sem oráculo de padding) |
| Senhas | PBKDF2-SHA256 com 600 mil iterações; salt aleatório; comparação em tempo constante; verificação fictícia para usuário inexistente; normalização determinística; limites contra hash adulterado |
| Material sensível | Chaves e textos claros intermediários são zerados na memória; a chave privada fica fora de `ToString()` e do JSON |

> [!IMPORTANT]
> 🔒 Qualquer falha de decifragem RSA ou híbrida gera **a mesma** exceção genérica, sem revelar a causa (proteção contra ataques de oráculo de padding). Só a versão de formato desconhecida é informada explicitamente (o byte de versão não é secreto).

> [!IMPORTANT]
> 🔒 Com nonce aleatório (formato em memória), não cifre mais que 2³² mensagens com a mesma chave (recomendação do NIST). Faça rotação periódica.

> [!IMPORTANT]
> 🔒 Proteções do hash de senha:
> - Letras acentuadas são normalizadas para a forma composta (NIST SP 800-63B). A normalização é **própria e determinística**: `string.Normalize` não é usado porque não faz nada com `InvariantGlobalization`, o que faria o mesmo hash deixar de validar em servidores diferentes.
> - O hash armazenado também é tratado como entrada não confiável: iterações, salt e tamanhos são validados, o que impede que um registro adulterado cause consumo excessivo de CPU (DoS).

> [!WARNING]
> - Guarde chaves AES e chaves privadas RSA num cofre de segredos (ex.: **TEC.Vault**), nunca no `appsettings` versionado nem em logs. Guarde as duas chaves PEM como texto e recrie o par com `new RsaKeyPair(...)`; nunca persista um `RsaKeyPair` serializado esperando recuperar a chave privada.
> - Se `DecryptAsync` lançar exceção, descarte o que já foi gravado (grave num arquivo temporário e só publique após sucesso).
> - Assine o que é cifrado com chave pública: a híbrida não prova autoria.
> - Compare HMAC, tokens e assinaturas com `FixedTimeEquals`/`FixedTimeEqualsHex`, nunca com `==`.
> - Guarde no banco apenas o **hash** de tokens de redefinição de senha.

---

## ❓ Perguntas frequentes

<details>
<summary><code>CryptographicException</code>: formato não suportado ao decifrar</summary>

**Causa:** o primeiro byte dos dados não é um byte de versão conhecido: os dados não foram produzidos pelo `AesGcmCryptography`/`HybridCryptography` do TEC.Core ou foram truncados/corrompidos.
**Solução:** confira a origem dos dados e o formato descrito em [Formatos binários](#formatos-binários-contrato).

</details>

<details>
<summary>Recebo <code>AuthenticationTagMismatchException</code> ao decifrar com AES</summary>

**Causa:** chave diferente da usada na cifragem, dados alterados ou `associatedData` diferente do usado em `Encrypt`.
**Solução:** use exatamente a mesma chave e o mesmo contexto. Não tente "recuperar" dados com tag inválida: eles foram adulterados.

</details>

<details>
<summary>Como aumentar as iterações do PBKDF2 sem invalidar as senhas atuais?</summary>

Configure o novo valor (`options.PasswordHashIterations` no `AddTecCore` ou `new Pbkdf2PasswordHasher(iterations)`). Hashes antigos continuam validando, porque as iterações ficam gravadas no próprio hash; no login, `NeedsRehash` retorna `true` e você grava o novo `Hash` (veja o [exemplo completo](#pbkdf2passwordhasher)).

</details>

<details>
<summary>Por que <code>FixedTimeEquals("ABCDEF", "abcdef")</code> retorna <code>false</code>?</summary>

A comparação é **ordinal** (essencial para Base64 e tokens). Para hashes hexadecimais que podem vir em maiúsculas, como assinaturas de webhook, use `FixedTimeEqualsHex`.

</details>

<details>
<summary>Preciso cifrar mais de 190 bytes com a chave pública do parceiro</summary>

Use `HybridCryptography` (ou `IHybridCryptography`): não há limite de tamanho. `RsaCryptography.Encrypt` é limitado a `GetMaxDataLength(keySize)` bytes.

</details>

<details>
<summary>Posso desserializar um <code>RsaKeyPair</code> de JSON?</summary>

Não: o JSON contém só a chave pública e a leitura sempre lança `JsonException`. Guarde as duas chaves PEM no cofre e recrie o par com o construtor.

</details>

---

⬅️ [Anterior](datas.md) · [📚 Índice](README.md) · [Próximo](csv.md) ➡️
