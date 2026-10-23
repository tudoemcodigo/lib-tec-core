# ADR-0013: Leitura Delimitada de Arquivos de Segredos e Configuração com Zeragem de Buffer

- **Status:** Aceito
- **Data:** 2026-10-08 (atualizado em 2026-10-19)
- **Área:** `TEC.Core.IO`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

Em arquiteturas conteinerizadas (Kubernetes / Docker), segredos de infraestrutura, certificados e tokens de API são frequentemente injetados como arquivos de texto montados em volumes (ex.: `/var/run/secrets/`, `/etc/secrets/token`).
As abordagens convencionais de leitura de arquivos em C# (`File.ReadAllText`) apresentam sérias vulnerabilidades operacionais:
1. **Exaustão de Memória com Arquivos Especiais:** Arquivos especiais de sistema, pipes nomeados ou links simbólicos para dispositivos de streaming informam `stream.Length == 0`, induzindo leitores a alocar buffers infinitos ou travar a aplicação caso o arquivo seja substituído por um stream ilimitado.
2. **Ausência de Limites de Leitura:** Sem um teto em bytes (`maxBytes`), a leitura consome memória proporcional ao tamanho do arquivo em disco, abrindo vetor para Denial of Service.
3. **Resíduos de Segredos em Memória:** Ao ler certificados PEM ou senhas via `File.ReadAllText`, múltiplos buffers intermediários de bytes ficam dispersos na memória gerenciada até que o Garbage Collector decida coletá-los, expondo credenciais a leituras de heap dump.
4. **Tratamento Inseguro de BOM:** Arquivos de texto gerados no Windows podem incluir o preâmbulo UTF-8 (BOM: `0xEF, 0xBB, 0xBF`), que frequentemente quebra parsers de certificados PEM ou decodificadores de chaves se não for descartado adequadamente.

## 💡 Decisão

Criar o utilitário estático [`BoundedFileReader`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/IO/BoundedFileReader.cs) no namespace `TEC.Core.IO`:

### Diretrizes de Projeto:
- **Teto Rígido de Memória:**
  - O método `TryReadUtf8(string path, int maxBytes, out string? text)` aceita um limite explícito de bytes.
  - A alocação nunca ultrapassa `maxBytes + 1` bytes. Se o arquivo físico exceder esse limite (ou informar tamanho inicial maior), o método aborta imediatamente retornando `false`, sem ler nem carregar o excesso em heap.
  - O cálculo do teto utiliza aritmética em `long` (`Math.Min((long)maxBytes + 1, Array.MaxLength)`), mitigando estouros inteiros (`OverflowException`) mesmo se `maxBytes` for `int.MaxValue`.
- **Compatibilidade com Pipes e Arquivos Especiais:**
  - Começa com um buffer inicial pequeno (4 KB) e cresce dinamicamente por duplicação até o limite configurado, permitindo ler pipes e arquivos virtuais do Linux onde `stream.Length` não está disponível.
- **Higienização Ativa de Memória:**
  - O buffer de bytes alocado é sistematicamente zerado via `CryptographicOperations.ZeroMemory` no bloco `finally`. Durante o redimensionamento dinâmico (`Grow`), o buffer anterior é zerado antes de ser descartado.
- **Descarte Transparente de BOM:**
  - Se os primeiros bytes forem o preâmbulo UTF-8 (`EF BB BF`), eles são fatiados e descartados antes da conversão para texto.
- **UTF-8 Estrito:**
  - Conteúdo que não represente UTF-8 válido lança `InvalidDataException` imediata, com mensagem neutra que **não reproduz nenhum trecho do arquivo**, impedindo vazamento de trechos de chaves em mensagens de exceção.

## ⚖️ Consequências

### Positivas
- **Segurança Reforçada para Segredos:** Reduz a janela de exposição de certificados e senhas na memória do processo.
- **Proteção Contra DoS:** Garante que nenhum pod ou worker esgote memória por leitura de arquivos montados adulterados.
- **Base para Componentes Superiores:** Utilizado diretamente pelo `TEC.Vault` para ler chaves PEM e credenciais locais.

### Negativas / Trade-offs
- Não é adequado para arquivos de texto massivos (acima de centenas de megabytes), para os quais streaming puro via `StreamReader` linha a linha é o padrão recomendado.
