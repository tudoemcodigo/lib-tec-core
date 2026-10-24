# Instruções para agentes — TEC.Core

Este arquivo complementa as instruções herdadas de `D:\Projetos\Componentes\AGENTS.md`. O TEC.Core é uma biblioteca de runtime e a fundação dos componentes TEC; suas APIs públicas e seus formatos persistidos são contratos para consumidores externos.

## Escopo e segurança de alterações

- O escopo padrão é este repositório. Não altere outros repositórios `TEC.*`, não publique pacotes/tags/releases e não rode sincronizações que escrevam fora daqui sem pedido explícito.
- Antes de editar, confira `git status` e `git diff`. Preserve alterações não commitadas; não use reset, checkout destrutivo ou limpeza para descartar trabalho existente.
- Mudanças em APIs públicas, formatos de hash/criptografia, serialização, convenções de DI ou alvos .NET exigem revisão de compatibilidade, documentação, changelog e consumidores.
- Testes devem atravessar contratos públicos e verificar resultados observáveis. Para mudanças de comportamento, cubra entradas inválidas, limites, falhas e concorrência quando aplicável.
- Trate todo dado externo como não confiável. Não exponha segredos, PII ou conteúdo controlado pelo usuário em exceções, logs, URLs ou sumários.

## Mapa da implementação

- `TEC.Core/`: biblioteca multi-target (`net8.0` e `net10.0`), sem acesso próprio a banco, rede ou telemetria.
  - `Common/`: guards, `Result`/`Error`, JSON estrito e cultura pt-BR.
  - `Cryptography/`: AES-GCM versionado, RSA, criptografia híbrida, PBKDF2, hashes e geração aleatória.
  - `Csv/`: leitura/escrita em streaming, conversão tipada, limites e proteção contra injeção de fórmulas.
  - `Dates/`: formatação, feriados, dias úteis e fuso de Brasília.
  - `Domain/`: entidades, agregados e eventos de domínio sem dependência de persistência.
  - `IO/`: leitura delimitada de arquivos.
  - `Responses/`, `Exceptions/`, `Text/`, `Numbers/`, `Enums/`, `Threading/`, `Security/` e `DependencyInjection/`: contratos e utilitários por domínio.
- `TEC.Core.Tests/`: testes de comportamento, regressão, segurança e propriedades.
- `TEC.Core.LoadTests/`: carga rápida e suítes de carga longa.
- `TEC.Core.Benchmarks/`: medições BenchmarkDotNet.
- `samples/`: API demonstrativa e gerador de carga; não são pacotes publicáveis.
- `build/`: props/targets compartilhados de compilação e polyfills necessários ao `net8.0`.
- `docs/adr/`: decisões arquiteturais; `docs/`: guias por API, segurança, compatibilidade, testes e desenvolvimento.

## Contratos relevantes

- `Directory.Build.props` é a fonte única de versão do pacote. A versão inicial é `0.0.1`; não introduza versões nos `.csproj`.
- `Directory.Packages.props` pode declarar versões distintas por TFM para `Microsoft.Extensions.*`; condições por TFM são intencionais para manter compatibilidade com os frameworks compartilhados.
- Dependências TEC.* são declaradas com `<TecReference Include="..." />`, não com `PackageReference` direto.
- `DomainEntity<TId>` deve manter `Equals` e `GetHashCode` compatíveis durante a transição de entidade transiente para persistida e com proxies de ORM.
- Hashes de senha, envelopes criptográficos e serialização são formatos versionados: qualquer mudança requer análise de migração antes da implementação.
- Os workflows dos consumidores chamam `tudoemcodigo/tec-workflows`; confira as tags realmente publicadas antes de alterar referências. No momento desta instrução, `v1`/`v1.0.0` existem e `v2` não foi publicada.
- A licença é Apache-2.0. Mantenha `LICENSE`, `NOTICE`, badge/README e `<PackageLicenseExpression>` coerentes; o pacote inclui `NOTICE` quando presente.

## Documentação

- `README.md` e `CHANGELOG.md` têm conteúdo próprio do TEC.Core: siga o padrão do template, mantenha a marca Tudo em Código em todos os READMEs e não substitua o conteúdo específico por placeholders genéricos. A sincronização central preserva esses dois arquivos quando já existem.
- Atualize `README.md`, `CHANGELOG.md` e os guias em `docs/` quando uma mudança alterar comportamento ou contrato.
- Registre decisões transversais em um ADR numerado e mantenha `docs/adr/README.md` e `docs/README.md` atualizados.
- Consulte `docs/revisao-tecnica.md` como relatório de auditoria com data e escopo; não trate resultados históricos como estado atual sem nova verificação.
- Workflows e configurações centrais do consumidor estão em `.github/workflows/`; mantenha `.github/workflows/README.md` fiel aos gatilhos, permissões e inputs reais.

## Verificação local

```powershell
dotnet restore TEC.Core.slnx
dotnet build TEC.Core.slnx -c Release
dotnet test --solution TEC.Core.slnx -c Release
$env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = "1"; dotnet test --solution TEC.Core.slnx -c Release; Remove-Item Env:DOTNET_SYSTEM_GLOBALIZATION_INVARIANT
git diff --check
```

O CI também valida convenções, pacote e workflows reutilizáveis. Não afirme que o CI está funcional apenas com build/testes locais: confirme que as tags chamadas existem e que os `permissions` do chamador incluem o mínimo exigido pelo workflow reutilizável.
