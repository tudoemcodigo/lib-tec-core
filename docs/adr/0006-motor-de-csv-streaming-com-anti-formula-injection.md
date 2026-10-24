[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0006

# ADR-0006: Motor de CSV em Streaming com Proteção Contra Injeção de Fórmulas e Modo Estrito

- **Status:** Aceito
- **Data:** 2026-10-08 (atualizado em 2026-10-19)
- **Área:** `TEC.Core.Csv`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

A manipulação de arquivos CSV em sistemas corporativos apresenta armadilhas críticas de segurança, desempenho e interoperabilidade:
1. **Injeção de Fórmulas (CSV/Formula Injection — CWE-1236 / OWASP):** Se células de texto contendo caracteres especiais como `=`, `+`, `-`, `@`, `TAB` ou `CR` forem exportadas diretamente, softwares de planilhas (como Excel e LibreOffice Calc) as executam como fórmulas no momento da abertura, permitindo exfiltração de dados locais ou execução remota de comandos via DDE.
2. **Exaustão de Memória (DoS):** Leitores ingênuos carregam arquivos inteiros na memória (`File.ReadAllLines` / listas de strings), derrubando a aplicação quando processam arquivos gigabyte ou entradas com aspas nunca fechadas.
3. **Corrupção Silenciosa de Encoding:** Arquivos exportados por Excel brasileiro costumam usar Windows-1252/ANSI. Ao lê-los como UTF-8 tradicional sem validação, acentos são corrompidos silenciosamente para `�` sem gerar erro de alerta.
4. **Arquivos Truncados por Falha:** Escritores de arquivo que abrem diretamente o arquivo de destino deixam arquivos corrompidos ou pela metade caso ocorra uma exceção de rede, banco ou cancelamento no meio do fluxo.

## 💡 Decisão

Construir os motores [`CsvReader`](../../TEC.Core/Csv/CsvReader.cs) e [`CsvWriter`](../../TEC.Core/Csv/CsvWriter.cs) como componentes nativos de alto desempenho:

### Diretrizes de Projeto:
- **Proteção Ativa Anti-Formula Injection ([`CsvFormulaGuard`](../../TEC.Core/Csv/Internal/CsvFormulaGuard.cs)):**
  - Habilitada por padrão (`SanitizeFormulas = true`). Textos livres iniciados por `=`, `+`, `-`, `@`, `\t` ou `\r` recebem um apóstrofo `'` no início durante a escrita. Na leitura, o apóstrofo é removido de forma transparente.
  - Tipos primitivos seguros (números negativos, datas, booleanos e enums) **não** recebem apóstrofo.
- **Processamento 100% em Streaming (`IAsyncEnumerable<T>` e `Stream`):**
  - Leitura e escrita linha a linha com buffers parametrizados (`BufferSize = 64 KB`), sem retenção do arquivo completo em heap.
- **Limites Defensivos Rígidos contra DoS:**
  - `MaxFieldLength` (padrão 1 MB), `MaxColumns` (padrão 1.024) e `MaxRecordLength` (padrão 4 MB), impedindo que arquivos malformados esgotem os recursos do pod.
- **UTF-8 Estrito na Leitura:**
  - Decodificação UTF-8 com detecção estrita de bytes inválidos: arquivos em ANSI/Windows-1252 geram [`CsvException`](../../TEC.Core/Csv/CsvException.cs) imediata (indicando linha e coluna) em vez de corromper o banco silenciosamente.
- **Escrita Segura e Atômica em Arquivo Temporário:**
  - [`CsvWriter.WriteFileAsync`](../../TEC.Core/Csv/CsvWriter.cs) grava em arquivo `.tmp` único na mesma pasta e só o renomeia/substitui via `File.Move(..., overwrite: true)` no término bem-sucedido. Se a operação falhar ou for cancelada, o arquivo anterior permanece intacto.
- **Ida e Volta Precisa para Datas:**
  - `DateTime` (UTC) e `DateTimeOffset` sem formatação explícita utilizam o formato ISO 8601 estendido `"O"` (`RoundTripFormat`), preservando frações de segundo, Kind e offset.

## ⚖️ Consequências

### Positivas
- **Imunidade a Injeção de Fórmulas:** Segurança total para os usuários finais ao abrirem exportações no Excel.
- **Resiliência Operacional:** Processa arquivos com milhões de registros sob consumo constante de memória (baixo footprint de GC).
- **Sem Dados Corrompidos:** Falhas de escrita não deixam arquivos pela metade; incompatibilidade de encoding é detectada na primeira linha inválida.

### Negativas / Trade-offs
- Para abrir CSVs gerados pelo Excel em Windows-1252 antigo, o desenvolvedor precisa registrar o `CodePagesEncodingProvider` e informar o encoding explicitamente nas opções.

---

⬅️ [Anterior: ADR-0005](0005-hashing-e-verificacao-de-senhas-com-pbkdf2.md) · [🏛️ Índice dos ADRs](README.md) · [Próximo: ADR-0007](0007-calculo-de-dias-uteis-feriados-e-fuso-horario-de-brasilia.md) ➡️
