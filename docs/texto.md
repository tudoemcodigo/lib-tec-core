[🏠 TEC.Core](../README.md) › [📚 Documentação](README.md) › Texto

# 🔤 Texto

> Formata, valida, gera e mascara documentos brasileiros (CPF, CNPJ inclusive alfanumérico, PIS, CEP, telefone, e-mail) e oferece utilitários de texto: acentos, slug, caixa, corte seguro, Base64 e Base64Url.

## 📑 Sumário

- [🎯 Visão geral](#-visão-geral)
- [🚀 Uso](#-uso)
  - [DocumentFormatter](#documentformatter)
  - [MaskFormatter](#maskformatter)
  - [DocumentValidator](#documentvalidator)
  - [DocumentGenerator](#documentgenerator)
  - [SensitiveDataMasker](#sensitivedatamasker)
  - [StringExtensions](#stringextensions)
  - [Base64UrlEncoder](#base64urlencoder)
  - [BoundedFileReader](#boundedfilereader)
  - [CNPJ alfanumérico](#cnpj-alfanumérico)
- [⚙️ Opções](#️-opções)
- [❌ Erros](#-erros)
- [🛡️ Segurança](#️-segurança)
- [❓ Perguntas frequentes](#-perguntas-frequentes)

---

## 🎯 Visão geral

| Namespace | Tipos | Para que serve |
|---|---|---|
| `TEC.Core.Text.Formatting` | `DocumentFormatter`, `MaskFormatter` | Máscara oficial de CPF, CNPJ, CEP, PIS e telefone; remoção de máscara; máscaras genéricas com `#` |
| `TEC.Core.Text.Validation` | `DocumentValidator` | Validação de documentos, telefone e e-mail |
| `TEC.Core.Text.Generation` | `DocumentGenerator` | Documentos válidos e fictícios para testes |
| `TEC.Core.Text.Masking` | `SensitiveDataMasker` | Mascaramento de dados pessoais para tela e log (LGPD) |
| `TEC.Core.Text.Extensions` | `StringExtensions` | Acentos, slug, caixa, corte seguro, comparação e Base64 de texto |
| `TEC.Core.Text.Codecs` | `Base64UrlEncoder` | Base64 seguro para URL, sem preenchimento, com decodificação estrita |
| `TEC.Core.IO` | `BoundedFileReader` | Leitura de arquivo de texto sensível com limite de tamanho, UTF-8 estrito e buffers zerados |

**Quando usar:** cadastros, telas, importações, logs, slugs, buscas sem acento, tokens em URL.

Fluxo típico de um documento:

```mermaid
flowchart LR
    IN["Entrada do usuário<br/>529.982.247-25"] --> V{"DocumentValidator<br/>IsValidCpf"}
    V -->|válido| N["DocumentFormatter.RemoveMask<br/>52998224725"]
    N --> DB[("Banco de dados")]
    DB --> F["DocumentFormatter.FormatCpf<br/>tela: 529.982.247-25"]
    DB --> M["SensitiveDataMasker.MaskCpf<br/>log: ***.982.247-**"]
    V -->|inválido| E["RequestValidationException"]
```

---

## 🚀 Uso

Exemplo completo: normalizar um documento recebido, gravar sem máscara e exibir formatado.

```csharp
using TEC.Core.Common.Results;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Generation;
using TEC.Core.Text.Validation;

public static Result<string> NormalizeDocument(string? document)
{
    if (string.IsNullOrWhiteSpace(document))
        return Error.Validation("DOCUMENTO_OBRIGATORIO", "Informe o CPF ou CNPJ.", "document");

    if (!DocumentValidator.IsValidCpfOrCnpj(document))
        return Error.Validation("DOCUMENTO_INVALIDO", "CPF ou CNPJ inválido.", "document");

    return DocumentFormatter.RemoveMask(document);   // "12.ABC.345/01DE-35" → "12ABC34501DE35"
}

// Na tela: formata o valor guardado sem máscara
string display = DocumentFormatter.FormatCpfOrCnpj("12ABC34501DE35");   // "12.ABC.345/01DE-35"

// Outros dados
bool cepOk = DocumentValidator.IsValidCep("01310-100");                  // true
bool emailOk = DocumentValidator.IsValidEmail("maria@empresa.com.br");   // true

// Só em testes: documentos válidos e fictícios
string testCpf = DocumentGenerator.GenerateCpf(formatted: true);
```

### DocumentFormatter

> `TEC.Core.Text.Formatting` · `static class`

Aplica a máscara oficial dos documentos. Aceita valores com ou sem máscara.

> [!TIP]
> Se a quantidade de caracteres não corresponder ao documento, ou se houver caracteres que não são do documento nem de máscara (ex.: letras em CPF, `#` no CEP), **o valor original é devolvido sem alteração** (nunca lança exceção nem descarta caracteres em silêncio). `null` → `""`. Caracteres de máscara aceitos (os mesmos do `DocumentValidator`): `.`, `-` e espaço em CPF/PIS/CEP; também `/` e letras no CNPJ; `+ ( ) . -` e espaço no telefone.

| Membro | Retorno | Descrição |
|---|---|---|
| `FormatCpf(string? cpf)` | `string` | Máscara `000.000.000-00`. |
| `FormatCnpj(string? cnpj)` | `string` | Máscara `AA.AAA.AAA/AAAA-00` (letras convertidas para maiúsculas). |
| `FormatCpfOrCnpj(string? document)` | `string` | CPF (11) ou CNPJ (14) conforme o tamanho. |
| `FormatCep(string? cep)` | `string` | Máscara `00000-000`. |
| `FormatPis(string? pis)` | `string` | Máscara `000.00000.00-0`. |
| `FormatPhone(string? phone)` | `string` | Conforme a quantidade de dígitos (tabela abaixo). |
| `RemoveMask(string? value)` | `string` | Mantém apenas letras e dígitos, **em maiúsculas**: formato recomendado para gravar no banco. |

| Chamada | Resultado |
|---|---|
| `FormatCpf("12345678909")` | `"123.456.789-09"` |
| `FormatCnpj("11222333000181")` | `"11.222.333/0001-81"` |
| `FormatCnpj("12abc34501de35")` | `"12.ABC.345/01DE-35"` |
| `FormatCpfOrCnpj("12345678909")` | `"123.456.789-09"` |
| `FormatCep("01310100")` | `"01310-100"` |
| `FormatPis("12345678919")` | `"123.45678.91-9"` |
| `RemoveMask("12.ABC.345/01DE-35")` | `"12ABC34501DE35"` |
| `FormatCpf("123")` (tamanho inválido) | `"123"` |
| `FormatCpf("abc12345678901")` (letras: não é CPF) | `"abc12345678901"` |
| `FormatCep("01310-100x")` | `"01310-100x"` |

`FormatPhone`:

| Dígitos | Entrada → saída |
|:---:|---|
| 8 | `"33334444"` → `"3333-4444"` |
| 9 | `"987654321"` → `"98765-4321"` |
| 10 | `"1133334444"` → `"(11) 3333-4444"` |
| 11 | `"11987654321"` → `"(11) 98765-4321"` |
| 12 (com 55) | `"551133334444"` → `"+55 (11) 3333-4444"` |
| 13 (com 55) | `"5511987654321"` → `"+55 (11) 98765-4321"` |

### MaskFormatter

> `TEC.Core.Text.Formatting` · `static class`

Máscaras genéricas. Cada `#` é substituído pelo próximo caractere do valor; os demais caracteres da máscara são mantidos.

| Membro | Retorno | Descrição |
|---|---|---|
| `Placeholder` (const) | `char` | `'#'`: posição preenchida pelo valor. |
| `Apply(string? value, string mask)` | `string` | Aplica a máscara. Se o valor for menor que a máscara, a formatação para no último caractere disponível. Se tiver **mais** caracteres que as posições `#`, é devolvido sem alteração (nenhum caractere é descartado). |

| Chamada | Resultado |
|---|---|
| `MaskFormatter.Apply("12345678", "#####-###")` | `"12345-678"` |
| `MaskFormatter.Apply("ABC1D23", "###-####")` | `"ABC-1D23"` (placa Mercosul) |
| `MaskFormatter.Apply("123", "#####-###")` | `"123"` |
| `MaskFormatter.Apply("1234567890", "#####-###")` | `"1234567890"` (excede a máscara) |
| `MaskFormatter.Apply(null, "###")` | `""` |

### DocumentValidator

> `TEC.Core.Text.Validation` · `static class`

Validação de documentos e dados brasileiros. Aceita valores **com ou sem máscara**, mas recusa caracteres estranhos (ex.: `"abc529.982.247-25"` → `false`). Os caracteres de máscara aceitos vêm da **mesma fonte** usada pelo `DocumentFormatter`, então validar e formatar nunca divergem: só o espaço comum é aceito (TAB, quebra de linha, espaço não separável e `_` são recusados). Tamanho e caracteres são conferidos **antes** de qualquer alocação (entradas de documento e telefone limitadas a 32 caracteres; e-mail a 254).

| Membro | Retorno | Descrição |
|---|---|---|
| `IsValidCpf(string? cpf)` | `bool` | 11 dígitos, dígitos verificadores corretos e sem sequências repetidas (`111.111.111-11`). |
| `IsValidCnpj(string? cnpj)` | `bool` | 14 caracteres. Os 12 primeiros podem ser **alfanuméricos**; os 2 verificadores são sempre numéricos. |
| `IsValidCpfOrCnpj(string? document)` | `bool` | Decide pelo tamanho (11 ou 14 caracteres úteis). |
| `IsValidPis(string? pis)` | `bool` | PIS/PASEP/NIT: 11 dígitos e dígito verificador correto. |
| `IsValidCep(string? cep)` | `bool` | 8 dígitos, não todos iguais. |
| `IsValidPhone(string? phone)` | `bool` | DDD existente (lista da Anatel); fixo com 10 dígitos (3º dígito de 2 a 5) ou celular com 11 dígitos (3º dígito = 9). Aceita o prefixo `+55`. |
| `IsValidEmail(string? email)` | `bool` | Formato RFC 5321: até 254 caracteres, parte local até 64, sem pontos no início, no fim ou consecutivos, e domínio com TLD. A expressão usa `\z` (não aceita quebra de linha no final). |

| Chamada | Resultado |
|---|:---:|
| `IsValidCpf("529.982.247-25")` | `true` |
| `IsValidCpf("111.111.111-11")` | `false` |
| `IsValidCnpj("11.222.333/0001-81")` | `true` |
| `IsValidCnpj("12.ABC.345/01DE-35")` | `true` |
| `IsValidCpfOrCnpj("52998224725")` | `true` |
| `IsValidPis("120.56118.29-9")` | `true` |
| `IsValidCep("01310-100")` | `true` |
| `IsValidCep("00000000")` | `false` |
| `IsValidPhone("(11) 98765-4321")` | `true` |
| `IsValidPhone("+55 11 3333-4444")` | `true` |
| `IsValidPhone("(20) 98765-4321")` | `false` (DDD 20 não existe) |
| `IsValidPhone("(11) 88765-4321")` | `false` (celular deve começar com 9) |
| `IsValidEmail("joao.silva@empresa.com.br")` | `true` |
| `IsValidEmail("joao..silva@empresa.com")` | `false` |
| `IsValidEmail("joao@localhost")` | `false` |

> [!TIP]
> `IsValidEmail` valida só o **formato**. Para confirmar que o e-mail existe, envie um código de verificação.

### DocumentGenerator

> `TEC.Core.Text.Generation` · `static class`

Gera documentos **matematicamente válidos, porém fictícios**, para testes e massa de dados. Usa gerador aleatório criptograficamente seguro.

| Membro | Retorno | Descrição |
|---|---|---|
| `GenerateCpf(bool formatted = false)` | `string` | CPF válido. |
| `GenerateCnpj(bool formatted = false, bool alphanumeric = false)` | `string` | CNPJ válido de matriz (ordem `0001`), numérico ou alfanumérico. |
| `GeneratePis(bool formatted = false)` | `string` | PIS/PASEP/NIT válido. |

| Chamada | Exemplo de saída |
|---|---|
| `GenerateCpf()` | `"07584285023"` |
| `GenerateCpf(formatted: true)` | `"123.082.460-06"` |
| `GenerateCnpj(formatted: true)` | `"24.489.831/0001-37"` |
| `GenerateCnpj(formatted: true, alphanumeric: true)` | `"EW.NB8.AE6/0001-11"` |
| `GeneratePis(formatted: true)` | `"779.74802.94-6"` |

```csharp
// TUnit
[Test]
public async Task Generated_cpf_is_valid()
{
    var cpf = DocumentGenerator.GenerateCpf();
    await Assert.That(DocumentValidator.IsValidCpf(cpf)).IsTrue();
}
```

### SensitiveDataMasker

> `TEC.Core.Text.Masking` · `static class`

Esconde parte de CPF, CNPJ, e-mail, telefone e cartão para exibição parcial, telas de confirmação, relatórios e **logs (LGPD)**.

| Membro | Retorno | Descrição |
|---|---|---|
| `DefaultMaskChar` (const) | `char` | `'*'` |
| `Mask(string? value, int visibleStart = 0, int visibleEnd = 0, char maskChar = '*')` | `string` | Mascara mantendo os primeiros e/ou últimos caracteres. 🔒 Se o valor for curto demais para os caracteres visíveis, mascara **tudo**. Nunca deixa meio caractere visível: se a fronteira cair no meio de um par surrogate (emoji), o caractere inteiro é mascarado. |
| `MaskCpf(string? cpf)` | `string` | Padrão adotado pelo governo (`***.456.789-**`). Sem 11 dígitos, mascara o valor inteiro. |
| `MaskCnpj(string? cnpj)` | `string` | Mantém o miolo do CNPJ (`**.222.333/****-**`), inclusive alfanumérico. Sem 14 caracteres, mascara o valor inteiro. |
| `MaskEmail(string? email)` | `string` | Mantém os 2 primeiros caracteres (1, se a parte local tiver até 2) e o domínio. Sem `@`, mantém só o primeiro caractere. Com caractere de controle, mascara o valor inteiro. Nulo ou só espaços → `""`. |
| `MaskPhone(string? phone)` | `string` | Com 10/11 dígitos, mantém o DDD e os 4 últimos dígitos (e o prefixo `+55`, com 12/13 dígitos iniciando com 55). Outros tamanhos: só os dígitos, com até 4 finais visíveis (no máximo metade). |
| `MaskCreditCard(string? cardNumber)` | `string` | Com 12 dígitos ou mais, mantém os 4 últimos (`**** **** **** 1234`); com menos, mascara todos os dígitos. |

| Chamada | Resultado |
|---|---|
| `Mask("123456789", 2, 2)` | `"12*****89"` |
| `Mask("abc", 2, 2)` | `"***"` |
| `Mask("senha", maskChar: '#')` | `"#####"` |
| `MaskCpf("123.456.789-09")` | `"***.456.789-**"` |
| `MaskCnpj("11.222.333/0001-81")` | `"**.222.333/****-**"` |
| `MaskEmail("joao.silva@empresa.com")` | `"jo********@empresa.com"` |
| `MaskEmail("jo@empresa.com")` | `"j*@empresa.com"` |
| `MaskPhone("(11) 98765-4321")` | `"(11) *****-4321"` |
| `MaskPhone("1133334444")` | `"(11) ****-4444"` |
| `MaskPhone("+55 11 98765-4321")` | `"+55 (11) *****-4321"` |
| `MaskCreditCard("4111 1111 1111 1234")` | `"**** **** **** 1234"` |

```csharp
using Microsoft.Extensions.Logging;
using TEC.Core.Text.Masking;

public sealed class SignupAudit(ILogger<SignupAudit> logger)
{
    public void Record(string cpf, string email, string phone, string card) =>
        logger.LogInformation("Cadastro: CPF {Cpf}, e-mail {Email}, telefone {Phone}, cartão {Card}",
            SensitiveDataMasker.MaskCpf(cpf),              // "***.456.789-**"
            SensitiveDataMasker.MaskEmail(email),          // "jo********@empresa.com"
            SensitiveDataMasker.MaskPhone(phone),          // "(11) *****-4321"
            SensitiveDataMasker.MaskCreditCard(card));     // "**** **** **** 1234"
}
```

#### Texto não confiável no log: `DescribeUntrusted`

Quando uma entrada é **recusada** (nome inválido, parâmetro malformado), registrar o texto recebido é arriscado: pode ser um segredo colado no campo errado. `DescribeUntrusted` devolve só o tamanho e um identificador curto (prefixo do HMAC-SHA256 com chave aleatória do processo):

| Chamada | Resultado |
|---|---|
| `DescribeUntrusted("sk-live-abc123")` | `"<14 caracteres, hmac:3fa0c19b7e21>"` (o identificador muda a cada reinício do processo) |
| `DescribeUntrusted(null)` | `"<nulo>"` |
| `DescribeUntrusted("")` | `"<vazio>"` |

```csharp
logger.LogWarning("Nome de segredo recusado: {Name}", SensitiveDataMasker.DescribeUntrusted(name));
```

> [!IMPORTANT]
> O mesmo texto gera o mesmo identificador **dentro do mesmo processo** (dá para correlacionar ocorrências), mas um hash
> sem chave permitiria confirmar palpites fora dele: por isso a chave é aleatória, só existe em memória e nunca é exposta.

### StringExtensions

> `TEC.Core.Text.Extensions` · `static class` (extensões de `string?`)

Limpa, corta, compara e converte textos considerando o português (acentos, conectivos). Todos aceitam `null` (retornam `""` ou `false`, conforme o caso), exceto quando indicado.

| Membro | Retorno | Descrição |
|---|---|---|
| `HasValue()` | `bool` | `false` para nulo, vazio ou só espaços. |
| `NullIfWhiteSpace()` | `string?` | Texto aparado, ou `null` se vazio. |
| `RemoveAccents()` | `string` | Remove acentos e cedilha (sem depender de ICU). |
| `OnlyDigits()` | `string` | Mantém só dígitos ASCII. |
| `OnlyLettersAndDigits()` | `string` | Mantém letras (inclusive acentuadas) e dígitos. |
| `CollapseWhitespace()` | `string` | Troca sequências de espaços por um único espaço e apara. |
| `Truncate(int maxLength, string suffix = "...")` | `string` | Corta em `maxLength`; o sufixo conta dentro do limite. Se o sufixo não couber (`suffix.Length >= maxLength`), corta sem sufixo. |
| `Left(int length)` | `string` | Primeiros caracteres. |
| `Right(int length)` | `string` | Últimos caracteres. |
| `ToSlug()` | `string` | Texto para URL. |
| `ToTitleCase()` | `string` | Iniciais maiúsculas, com conectivos do português em minúsculo. |
| `ToPascalCase()` | `string` | `NomeDoCliente` |
| `ToCamelCase()` | `string` | `nomeDoCliente` |
| `ToSnakeCase()` | `string` | `nome_do_cliente` |
| `ToKebabCase()` | `string` | `nome-do-cliente` |
| `EqualsIgnoreCaseAndAccents(string? other)` | `bool` | Igualdade sem caixa e sem acentos. Dois `null` são iguais; `null` e texto, não. |
| `ContainsIgnoreCaseAndAccents(string? search)` | `bool` | Contém, sem caixa e sem acentos. `false` se algum dos dois for `null`. |
| `ToBase64()` | `string` | Base64 padrão do texto em UTF-8. |
| `FromBase64()` | `string` | Decodifica Base64 com UTF-8 estrito. |
| `TryFromBase64(out string result)` | `bool` | Versão sem exceção. Nulo ou vazio → `true` com `result = ""`. |

**Verificação e limpeza**

| Chamada | Resultado |
|---|---|
| `"  ".HasValue()` | `false` |
| `"  abc ".NullIfWhiteSpace()` / `"  ".NullIfWhiteSpace()` | `"abc"` / `null` |
| `"Ação Pública".RemoveAccents()` | `"Acao Publica"` |
| `"(11) 98765-4321".OnlyDigits()` | `"11987654321"` |
| `"Olá, mundo! 123".OnlyLettersAndDigits()` | `"Olámundo123"` |
| `"  a   b \t c  ".CollapseWhitespace()` | `"a b c"` |

**Corte**

| Chamada | Resultado |
|---|---|
| `"Texto muito longo".Truncate(10)` | `"Texto m..."` |
| `"Texto muito longo".Truncate(10, "…")` | `"Texto mui…"` |
| `"abcdef".Left(3)` | `"abc"` |
| `"abcdef".Right(2)` | `"ef"` |

> [!NOTE]
> 🔒 `Truncate`, `Left` e `Right` **nunca cortam um emoji ao meio** (par surrogate).

**Caixa e formato**

| Chamada | Resultado |
|---|---|
| `"Promoção de Verão 2026!".ToSlug()` | `"promocao-de-verao-2026"` |
| `"MARIA DA SILVA E SOUZA".ToTitleCase()` | `"Maria da Silva e Souza"` |
| `"nome do cliente".ToPascalCase()` | `"NomeDoCliente"` |
| `"Nome do Cliente".ToCamelCase()` | `"nomeDoCliente"` |
| `"NomeDoCliente".ToSnakeCase()` | `"nome_do_cliente"` |
| `"HTTPServerError".ToSnakeCase()` | `"http_server_error"` |
| `"NomeDoCliente".ToKebabCase()` | `"nome-do-cliente"` |

`ToTitleCase` mantém em minúsculo os conectivos do português (`a, à, as, às, o, os, e, de, da, das, do, dos, em, na, nas, no, nos, com, por, para`), exceto na primeira palavra.

**Comparação e Base64**

| Chamada | Resultado |
|---|---|
| `"JOSÉ".EqualsIgnoreCaseAndAccents("jose")` | `true` |
| `"São Paulo".ContainsIgnoreCaseAndAccents("sao")` | `true` |
| `"Olá".ToBase64()` | `"T2zDoQ=="` |
| `"T2zDoQ==".FromBase64()` | `"Olá"` |
| `"%%%".TryFromBase64(out _)` | `false` |

Exemplo completo:

```csharp
using TEC.Core.Text.Extensions;

string search = "  sao   PAULO ".CollapseWhitespace();                         // "sao PAULO"
string[] cities = ["São Paulo", "Santo André", "Osasco"];
var found = cities.Where(c => c.ContainsIgnoreCaseAndAccents(search));       // ["São Paulo"]

string name = "JOSÉ DAS NEVES".ToTitleCase();                                // "José das Neves"
string file = $"{"Relatório de Vendas – Março".ToSlug()}.csv";               // "relatorio-de-vendas-marco.csv"
string phone = "(11) 98765-4321".OnlyDigits();                               // "11987654321"
string summary = "Descrição bem longa do produto".Truncate(15);              // "Descrição be..."
bool same = "AÇÃO".EqualsIgnoreCaseAndAccents("acao");                       // true
```

> [!TIP]
> `RemoveAccents`, `EqualsIgnoreCaseAndAccents` e `ContainsIgnoreCaseAndAccents` não dependem de ICU e funcionam com `InvariantGlobalization`. `FromBase64` usa UTF-8 estrito: bytes inválidos geram erro em vez de `�`. Texto UTF-16 inválido (surrogate isolado) não derruba as funções.

### Base64UrlEncoder

> `TEC.Core.Text.Codecs` · `static class`

Base64 seguro para URL **sem preenchimento** (RFC 4648 §5): alfabeto `A-Z a-z 0-9 - _`, sem `=`. Use para tokens, chaves de API, identificadores e hashes que trafegam em URL, cabeçalho HTTP ou nome de arquivo. O resultado é idêntico em `net8.0` e `net10.0` (no .NET 9+ delega para `System.Buffers.Text.Base64Url`). Trabalha com **bytes**; para texto, converta antes com `Encoding.UTF8`.

| Membro | Retorno | Descrição |
|---|---|---|
| `Encode(ReadOnlySpan<byte> bytes)` | `string` | Codifica em Base64Url sem preenchimento (vazio para entrada vazia). |
| `IsValid(ReadOnlySpan<char> value)` | `bool` | Confere alfabeto, tamanho (resto da divisão por 4 diferente de 1) e forma canônica, sem decodificar: `true` se, e somente se, `TryDecode` aceita o texto. |
| `TryDecode(string? value, out byte[] bytes)` | `bool` | Decodifica sem lançar exceção. **Estrito:** recusa `null`, preenchimento `=`, espaços, quebras de linha, caracteres fora do alfabeto (inclusive `+` e `/` do Base64 padrão) e textos **não canônicos** (bits finais diferentes de zero). Em falha, `bytes` é um array vazio. |

| Chamada | Resultado |
|---|---|
| `Base64UrlEncoder.Encode([0xFB, 0xFF])` | `"-_8"` |
| `Base64UrlEncoder.Encode("Olá"u8)` | `"T2zDoQ"` |
| `Base64UrlEncoder.TryDecode("T2zDoQ", out var b)` | `true`, `b = [0x4F, 0x6C, 0xC3, 0xA1]` |
| `Base64UrlEncoder.TryDecode("T2zDoQ==", out _)` | `false` (preenchimento) |
| `Base64UrlEncoder.TryDecode("T2zDoR", out _)` | `false` (não canônico: mesmos bytes de `"T2zDoQ"`) |
| `Base64UrlEncoder.IsValid("abcde")` | `false` (tamanho impossível) |
| `Base64UrlEncoder.IsValid("T2zDoR")` | `false` (não canônico) |

```csharp
using System.Security.Cryptography;
using TEC.Core.Text.Codecs;

// Identificador opaco para usar em URL
string id = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(16));   // 22 caracteres, sem '='

// Leitura de entrada não confiável: sem exceção, sem aceitar variações
if (!Base64UrlEncoder.TryDecode(request.Query["id"], out byte[] raw) || raw.Length != 16)
    return Results.BadRequest();
```

> [!NOTE]
> 🔒 A forma canônica garante que cada sequência de bytes tenha **um único** texto válido: assim um token não pode ser "variado" (ex.: `...Q` e `...R`) para burlar comparação, cache ou lista de revogação. Para tokens aleatórios prontos, veja `SecureRandomGenerator.GenerateToken` em [Criptografia](criptografia.md).

### BoundedFileReader

`TEC.Core.IO.BoundedFileReader` lê arquivos de texto **pequenos e sensíveis** (credencial montada em volume, segredo do Kubernetes, `.env`) com segurança:

| Garantia | Detalhe |
|---|---|
| 📏 Tamanho limitado | Nunca guarda mais que `maxBytes + 1` bytes em memória, mesmo em pipe ou arquivo especial que informa tamanho 0 |
| 🔤 UTF-8 estrito | Bytes inválidos lançam `InvalidDataException` com mensagem **sem** trecho do conteúdo |
| 🪟 Sem BOM | O BOM UTF-8 inicial (arquivo salvo no Windows) é descartado |
| 🧹 Buffers zerados | Os bytes intermediários são apagados depois do uso |

```csharp
using TEC.Core.IO;

if (!BoundedFileReader.TryReadUtf8("/var/run/secrets/token", maxBytes: 64 * 1024, out string? token))
    throw new InvalidOperationException("Arquivo de credencial acima de 64 KB.");
```

| Situação | Resultado |
|---|---|
| Arquivo dentro do limite | `true` e o texto |
| Arquivo acima do limite | `false` (nada além do limite é lido) |
| UTF-8 inválido | `InvalidDataException` |
| `path` vazio / `maxBytes <= 0` | `ArgumentException` / `ArgumentOutOfRangeException` |
| Arquivo inexistente, sem permissão | `IOException` / `UnauthorizedAccessException` |

### CNPJ alfanumérico

A partir de **julho de 2026**, a Receita Federal passou a emitir CNPJs com letras nas 12 primeiras posições (ex.: `12.ABC.345/01DE-35`). O TEC.Core suporta o novo formato em todas as operações:

| Operação | Suporte |
|---|:---:|
| `DocumentValidator.IsValidCnpj` / `IsValidCpfOrCnpj` | ✅ |
| `DocumentFormatter.FormatCnpj` / `FormatCpfOrCnpj` (converte para maiúsculas) | ✅ |
| `DocumentFormatter.RemoveMask` (mantém as letras) | ✅ |
| `SensitiveDataMasker.MaskCnpj` | ✅ |
| `DocumentGenerator.GenerateCnpj(alphanumeric: true)` | ✅ |

```csharp
if (DocumentValidator.IsValidCnpj(dto.Cnpj))
    dto.Cnpj = DocumentFormatter.RemoveMask(dto.Cnpj);   // grava "12ABC34501DE35" no banco
```

> [!IMPORTANT]
> O CNPJ alfanumérico exige colunas `CHAR(14)`/`VARCHAR(14)` (não `BIGINT`/`NUMERIC`) e normalização com `DocumentFormatter.RemoveMask` (não com `OnlyDigits()`, que descartaria as letras).

---

## ⚙️ Opções

O módulo não tem opções configuráveis; os limites e constantes são fixos:

| Constante / limite | Valor | Descrição |
|---|---|---|
| `MaskFormatter.Placeholder` | `'#'` | Posição preenchida pelo valor na máscara. |
| `SensitiveDataMasker.DefaultMaskChar` | `'*'` | Caractere de máscara padrão de `Mask`. |
| Tamanho máximo de documento/telefone em `DocumentValidator` | 32 caracteres | Entradas maiores retornam `false` sem alocar. |
| Tamanho máximo de e-mail | 254 caracteres (parte local até 64) | RFC 5321. |
| `Truncate(..., suffix)` | `"..."` | Sufixo padrão (conta dentro do limite). |

---

## ❌ Erros

`DocumentFormatter`, `DocumentValidator`, `DocumentGenerator`, os métodos específicos de `SensitiveDataMasker` (`MaskCpf`, `MaskEmail`...) e `Base64UrlEncoder` **não lançam exceções**: formatadores devolvem o valor recebido (ou `""` para `null`) e validadores/decodificadores retornam `false`.

| Exceção | Quando ocorre | O que fazer |
|---|---|---|
| `ArgumentNullException` | `MaskFormatter.Apply` com `mask` nula; `Truncate` com `suffix` nulo | Informe a máscara/sufixo. |
| `ArgumentOutOfRangeException` | `SensitiveDataMasker.Mask` com `visibleStart`/`visibleEnd` negativos; `Truncate` com `maxLength` negativo; `Left`/`Right` com `length` negativo | Use valores ≥ 0. |
| `ArgumentException` | `SensitiveDataMasker.Mask` com `maskChar` de controle ou surrogate: *Caractere de máscara inválido.* | Use um caractere visível simples (ex.: `*`, `#`). |
| `FormatException` | `FromBase64` com Base64 ou UTF-8 inválido: *O valor não é um Base64 de texto UTF-8 válido.* | Use `TryFromBase64` para entradas não confiáveis. |

---

## 🛡️ Segurança

> [!WARNING]
> `DocumentGenerator` é **só para testes**: gera documentos válidos, porém fictícios. Não use em produção.

> [!CAUTION]
> Nunca registre CPF, CNPJ, e-mail, telefone ou cartão em log sem passar por `SensitiveDataMasker`. Se o valor for curto demais para os caracteres visíveis, tudo é mascarado; nenhum emoji é cortado ao meio.

> [!WARNING]
> Para tokens e identificadores em URL, use `Base64UrlEncoder` (ou `SecureRandomGenerator.GenerateToken`), não `ToBase64()`: o Base64 padrão usa `+`, `/` e `=`, que precisam de escape em URL. `TryDecode` e `IsValid` são estritos e canônicos: aceitam exatamente os mesmos textos.

- 🔒 As expressões regulares de `StringExtensions` (espaços e slug) usam `RegexOptions.NonBacktracking` (tempo linear, sem ReDoS); a de separação de palavras (`ToPascalCase`, `ToSnakeCase`...) usa lookahead, é linear por construção e é coberta por teste de negação de serviço.
- 🔒 `DocumentValidator` confere tamanho e caracteres antes de alocar, e a regex de e-mail usa `\z` (sem aceitar quebra de linha final).
- 🔒 `FromBase64`/`TryFromBase64` usam UTF-8 estrito: dados corrompidos não viram `�` em silêncio.

---

## ❓ Perguntas frequentes

<details>
<summary>CNPJ alfanumérico é recusado pelo banco</summary>

**Causa:** coluna numérica (`BIGINT`/`NUMERIC`) ou normalização com `OnlyDigits()`.
**Solução:** use `CHAR(14)`/`VARCHAR(14)` e `DocumentFormatter.RemoveMask`.

</details>

<details>
<summary>Por que <code>FormatCpf</code> devolveu o valor sem máscara?</summary>

O valor não tem 11 dígitos ou contém caracteres que não são de CPF nem de máscara. O formatador nunca descarta caracteres em silêncio; valide antes com `DocumentValidator.IsValidCpf`.

</details>

<details>
<summary>Qual a diferença entre <code>ToBase64()</code> e <code>Base64UrlEncoder.Encode</code>?</summary>

`ToBase64()` codifica um **texto** (UTF-8) em Base64 padrão, com `+`, `/` e `=`. `Base64UrlEncoder.Encode` codifica **bytes** no alfabeto seguro para URL, sem preenchimento, e `TryDecode` só aceita a forma canônica.

</details>

<details>
<summary>As funções sem acento funcionam em container sem ICU?</summary>

Sim. `RemoveAccents`, `EqualsIgnoreCaseAndAccents`, `ContainsIgnoreCaseAndAccents` e `ToSlug` usam uma tabela própria de diacríticos latinos e funcionam com `InvariantGlobalization=true`. Veja [Compatibilidade](compatibilidade.md).

</details>

---
⬅️ [Exceções](excecoes.md) · [📚 Índice](README.md) · [Números](numeros.md) ➡️
