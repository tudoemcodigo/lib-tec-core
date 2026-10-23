using System.Security.Cryptography;

namespace TEC.Core.Cryptography.Asymmetric;

/// <summary>
/// Chave RSA já importada de PEM, para reutilizar em muitas operações de <see cref="RsaCryptography"/> sem interpretar o
/// PEM e importar a chave a cada chamada (ex.: verificar a assinatura de cada webhook, decifrar cada mensagem).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A importação aplica as mesmas regras das sobrecargas com PEM: tamanho máximo do PEM, mínimo de
/// <see cref="RsaCryptography.MinimumKeySize"/> e máximo de <see cref="RsaCryptography.MaximumImportKeySize"/> bits.</item>
/// <item>Pode ser compartilhada entre threads (ex.: singleton): as operações não alteram a chave. Descarte-a quando
/// ninguém mais for usá-la; depois do descarte, as operações lançam <see cref="ObjectDisposedException"/>.</item>
/// <item>A chave privada fica em memória enquanto a instância existir: guarde-a com o mesmo cuidado do PEM.</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Na inicialização (singleton)
/// var publicKey = RsaKey.FromPublicKeyPem(pemDoParceiro);
///
/// // Por requisição
/// bool valid = rsa.VerifyData(body, signature, publicKey);
/// </code>
/// </example>
public sealed class RsaKey : IDisposable
{
    private readonly RSA _rsa;

    private RsaKey(RSA rsa, bool hasPrivateKey)
    {
        _rsa = rsa;
        HasPrivateKey = hasPrivateKey;
    }

    /// <summary>Tamanho da chave em bits.</summary>
    public int KeySize => _rsa.KeySize;

    /// <summary>Indica se a chave importada contém a parte privada (permite decifrar e assinar).</summary>
    public bool HasPrivateKey { get; }

    /// <summary>Instância do .NET usada nas operações.</summary>
    internal RSA Rsa => _rsa;

    /// <summary>Importa uma chave pública (ou privada, da qual só a parte pública será usada para cifrar e verificar).</summary>
    /// <param name="publicKeyPem">Chave em PEM (SubjectPublicKeyInfo, PKCS#1 ou PKCS#8).</param>
    /// <exception cref="ArgumentException">PEM vazio, inválido, grande demais ou chave fora dos tamanhos aceitos.</exception>
    public static RsaKey FromPublicKeyPem(string publicKeyPem) =>
        new(RsaCryptography.ImportKey(publicKeyPem, requirePrivate: false), RsaCryptography.IsPrivateKeyPem(publicKeyPem));

    /// <summary>Importa uma chave privada (necessária para decifrar e assinar).</summary>
    /// <param name="privateKeyPem">Chave privada em PEM (rótulo "PRIVATE KEY").</param>
    /// <exception cref="ArgumentException">PEM sem chave privada, vazio, inválido, grande demais ou fora dos tamanhos aceitos.</exception>
    public static RsaKey FromPrivateKeyPem(string privateKeyPem) =>
        new(RsaCryptography.ImportKey(privateKeyPem, requirePrivate: true), hasPrivateKey: true);

    /// <summary>Libera a chave importada.</summary>
    public void Dispose() => _rsa.Dispose();
}
