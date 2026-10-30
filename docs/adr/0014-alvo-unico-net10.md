[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0014

# ADR-0014: Alvo Único net10.0

- **Status:** Aceito
- **Data:** 2026-10-29
- **Área:** Build, pacote e dependências
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

O pacote saía com dois alvos, `net8.0` e `net10.0`. Manter o `net8.0` custava:

1. **Polyfills e `#if` no código:** `EncodingCompat` (hexadecimal), `OverloadResolutionPriorityAttribute` e o caminho alternativo do `Base64UrlEncoder` existiam só para o .NET 8, com testes próprios para provar que os dois caminhos davam o mesmo resultado.
2. **Dependências pareadas por TFM:** `Microsoft.Extensions.DependencyInjection*` em 8.x no `net8.0` e 10.x no `net10.0`, com condições no `Directory.Packages.props` e regras específicas no Dependabot.
3. **CI e ambiente mais pesados:** uma perna a mais na matriz de testes e os runtimes .NET 8 e ASP.NET Core 8 instalados para rodar a suíte.
4. **Limitação para quem consome:** projetos `net8.0` usam C# 12, o que obrigava a documentar exceções como `params IEnumerable<…>` sem argumentos avulsos.

O .NET 10 é LTS. O time decidiu concentrar o suporte nele e não manter mais o alvo `net8.0`.

## 💡 Decisão

- O template canônico (`build/Tec.Build.props`, no `tec-workflows`) define `TargetFrameworks` como `net10.0`, e o TEC.Core não redefine.
- Saem os polyfills, todos os `#if NET*` e as versões 8.x de `Microsoft.Extensions.*`. O código usa direto as APIs do .NET 9 e 10.
- O pacote publica só `lib/net10.0`.

## ⚖️ Consequências

### Positivas
- Código sem ramificação por framework: um caminho só para testar e revisar.
- `Directory.Packages.props` sem condições por TFM.
- Matriz de CI menor (net10.0 com e sem ICU) e ambiente de desenvolvimento só com o SDK do `global.json`.

### Negativas / Trade-offs
- **Sem .NET 8 ou 9:** apps nesses runtimes não conseguem referenciar o pacote (`NU1202`) e precisam migrar para o .NET 10 antes de adotar o TEC.Core.
- Reintroduzir um alvo exige revisar o template canônico, o CI, o Dependabot e este ADR.

---

⬅️ [Anterior: ADR-0013](0013-leitura-delimitada-de-arquivos-sensiveis-com-boundedfilereader.md) · [🏛️ Índice dos ADRs](README.md)
