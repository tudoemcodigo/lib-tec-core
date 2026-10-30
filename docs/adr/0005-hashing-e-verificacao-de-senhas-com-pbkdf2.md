[🏠 TEC.Core](../../README.md) › [📚 Documentação](../README.md) › [🏛️ ADRs](README.md) › ADR-0005

# ADR-0005: Hashing e Verificação de Senhas com PBKDF2 Nativo, Tempo Constante e Normalização Latin-1

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Cryptography.Hashing`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

O armazenamento e a conferência de senhas em sistemas de autenticação expõem aplicações a riscos críticos:
1. **Ataques de Enumeração de Usuários por Temporização (*Timing Attacks*):** Quando um usuário não existe no banco, APIs tradicionais retornam imediatamente `false` (ou 401), enquanto para um usuário existente o algoritmo de hash executa milhares de iterações. Atacantes medem as diferenças de milissegundos para mapear contas ativas no sistema.
2. **Divergências de Caracteres Acentuados:** Em língua portuguesa, letras com diacríticos (como "é", "ç", "ã") podem ser enviadas em formulários compostos (NFC) ou decompostos (NFD) dependendo do sistema operacional ou teclado do cliente. Usar `string.Normalize()` do .NET é perigoso, pois ele se torna um *no-op* em ambientes com `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (comum em containers Linux mínimos sem ICU), gerando hashes incompatíveis entre ambientes.
3. **Rigidez de Custo Computacional:** Algoritmos que não registram suas iterações no hash inviabilizam o aumento da complexidade à medida que o poder computacional dos atacantes cresce.

## 💡 Decisão

Adotar o [`Pbkdf2PasswordHasher`](../../TEC.Core/Cryptography/Hashing/Pbkdf2PasswordHasher.cs) com o algoritmo **PBKDF2-HMAC-SHA256**:

### Diretrizes de Projeto:
- **Parâmetros e Formato:**
  - Padrão OWASP: **600.000 iterações**, Salt de 16 bytes via CSPRNG e Hash de 32 bytes (256 bits).
  - Formato serializado auto-contido: `PBKDF2-SHA256${iterações}${saltBase64}${hashBase64}`.
- **Normalização Determinística sem ICU ([`LatinDiacritics`](../../TEC.Core/Text/Internal/LatinDiacritics.cs)):**
  - Normalização Latin-1 própria e imutável para a forma composta, garantindo que o mesmo hash seja produzido idêntico em qualquer máquina, com ou sem suporte a ICU no sistema operacional.
- **Verificação Fictícia de Custo Idêntico (*Dummy Derivation*):**
  - Se a senha armazenada for `null`, vazia ou inválida (usuário inexistente), o método `Verify` executa a derivação PBKDF2 real com um salt estático e o número total de iterações configurado. O tempo de resposta para um login de usuário inexistente é indistinguível de um login com senha errada de usuário existente.
- **Equalização de Custo em Hashes Antigos:**
  - Se um hash antigo gravado tiver menos iterações que o atual, a verificação calcula a diferença com derivação fictícia complementar, impedindo que hashes legados vazem a idade da conta por tempo de resposta.
- **Evolução Gradual via `NeedsRehash`:**
  - Permite verificar se o hash armazenado possui iterações inferiores à política atual para que o serviço recalcule a senha de forma transparente durante o login bem-sucedido.
- **Comparação em Tempo Constante:**
  - Uso estrito de `CryptographicOperations.FixedTimeEquals`.

## ⚖️ Consequências

### Positivas
- **Enumeração de logins por tempo de resposta:** usuário inexistente, hash malformado e hash com menos iterações que as configuradas custam as iterações configuradas. Senha vazia ou acima de `MaxPasswordLength` retorna `false` sem derivação, mas isso não revela se o usuário existe.
- **Portabilidade Total:** Funciona com exatidão em containers Alpine/Debian sem bibliotecas ICU instaladas.
- **Independência de Pacotes Nativos:** Não requer bibliotecas C nativas (como Argon2 ou bcrypt externos), operando só com código gerenciado e compatível com Native AOT.

### Negativas / Trade-offs
- PBKDF2 tem custo de memória menor que Argon2id contra ataques acelerados por ASIC/GPU; porém, com 600.000 iterações atinge conformidade plena com NIST SP 800-63B e OWASP.

---

⬅️ [Anterior: ADR-0004](0004-criptografia-simetrica-assimetrica-e-hibrida-versionada.md) · [🏛️ Índice dos ADRs](README.md) · [Próximo: ADR-0006](0006-motor-de-csv-streaming-com-anti-formula-injection.md) ➡️
