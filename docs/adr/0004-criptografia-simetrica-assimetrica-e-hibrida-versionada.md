# ADR-0004: Arquitetura Criptográfica Versionada (AES-GCM, RSA e Híbrida) com ZeroMemory

- **Status:** Aceito
- **Data:** 2026-10-08
- **Área:** `TEC.Core.Cryptography`
- **Decisores:** Roberto Oliveira / Time TEC

---

## 🎯 Contexto

Aplicações corporativas necessitam de criptografia robusta para repouso e trânsito de dados confidenciais. Práticas inseguras comuns na indústria incluem:
- Cifras não autenticadas (como AES-CBC) suscetíveis a ataques de oráculo de padding (Padding Oracle Attacks / Vaudenay).
- Reutilização de IV/Nonce em AES-GCM, o que compromete totalmente a chave.
- Ausência de versionamento de formato binário, inviabilizando migrações graduais de algoritmos.
- Sobrecarga no processamento de chaves PEM em fluxos de alta frequência.
- Permanência de chaves e textos planos descriptografados na memória gerenciada da aplicação (lixo do GC acessível via dumps de memória).

## 💡 Decisão

Implementar componentes nativos de criptografia de padrão militar com autenticação de integridade (AEAD) e garantia de versionamento:

### 1. Criptografia Simétrica ([`AesGcmCryptography`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Cryptography/Symmetric/AesGcmCryptography.cs))
- **Em Memória:** Formato `[versão 1 byte = 0x01][nonce 12 bytes][tag 16 bytes][dados cifrados]`. O byte de versão entra nos dados associados autenticados (AAD), impedindo adulterações externas no cabeçalho.
- **Em Streaming e Arquivos:** Divisão em blocos de 64 KB com derivação de subchave exclusiva por arquivo via **HKDF-SHA256** com salt de 32 bytes e contexto `TEC.Core.AesGcm.Stream.v1`. Cada bloco possui flag de bloco final, tamanho e contador sequencial no AAD, tornando matematicamente impossível reordenar, truncar ou injetar blocos adicionais.
- **Operações Atômicas de Arquivos:** Métodos dedicados `EncryptFileAsync`, `DecryptFileAsync`, `EncryptToFileAsync` e `DecryptFromFileAsync` com chave binária ou Base64, gravando em arquivo temporário `.tmp` e substituindo o destino de forma atômica no encerramento, garantindo que falhas parciais nunca deixem arquivos truncados ou corrompidos.

### 2. Criptografia Assimétrica ([`RsaCryptography`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Cryptography/Asymmetric/RsaCryptography.cs))
- Cifragem exclusivamente com **RSA-OAEP-SHA256**.
- Assinatura com **SHA-256 + PSS** (padrão) e suporte a PKCS#1 v1.5 para compatibilidade com legados.
- Piso mínimo de segurança de 2048 bits para geração e teto de importação de 16384 bits para mitigar ataques de negação de serviço (DoS) por processamento exponencial de módulos criptográficos.
- Introdução de [`RsaKey`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Cryptography/Asymmetric/RsaKey.cs) para importação única de chaves PEM em caminhos de alta performance.

### 3. Criptografia Híbrida ([`HybridCryptography`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Cryptography/Hybrid/HybridCryptography.cs))
- Combina RSA (para troca segura da chave simétrica aleatória efêmera de 256 bits) com AES-GCM (para cifrar o payload sem restrição de tamanho).
- O cabeçalho completo com o prefixo da chave cifrada é autenticado no AAD do AES-GCM.
- Suporte nativo a [`RsaKey`](file:///D:/Projetos/Componentes/TEC.Core/TEC.Core/Cryptography/Asymmetric/RsaKey.cs) em todas as sobrecargas de memória, texto Base64 e arquivos (`EncryptFileAsync`, `DecryptFileAsync`, `EncryptToFileAsync`, `DecryptFromFileAsync`), viabilizando alta vazão de arquivos híbridos sem reinterpretar PEMs a cada operação.

### 4. Higienização de Memória (*ZeroMemory*)
- Todos os buffers temporários de texto plano, chaves decodificadas e senhas intermediárias são sistematicamente zerados em blocos `finally` via [`CryptographicOperations.ZeroMemory`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory).

## ⚖️ Consequências

### Positivas
- **Segurança Criptográfica Extrema:** Proteção comprovada contra ataques de oráculo de padding, colisões de nonce e espionagem de memória pós-GC.
- **Transparência e Evolução:** Formatos binários com byte de versão permitem a inclusão futura de cifras pós-quânticas sem quebrar decifragem de registros antigos.
- **Zero Dependências:** Utiliza as APIs criptográficas nativas da BCL do .NET (OpenSSL no Linux, CNG no Windows), 100% compatível com Native AOT.

### Negativas / Trade-offs
- O tamanho dos dados cifrados é acrescido dos bytes de cabeçalho, nonce e tag de autenticação.
