namespace TEC.Core.Cryptography.Abstractions;

/// <summary>
/// Hash de senhas (irreversível, com salt). Nunca armazene senhas com criptografia reversível.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Gera o hash da senha, já contendo algoritmo, iterações e salt.</summary>
    string Hash(string password);

    /// <summary>Verifica se a senha corresponde ao hash armazenado.</summary>
    /// <param name="password">Senha digitada.</param>
    /// <param name="hashedPassword">
    /// Hash armazenado. Passe <c>null</c> quando o usuário não existir: a implementação deve executar uma verificação
    /// fictícia de custo equivalente e retornar <c>false</c>, para que o tempo de resposta não revele quais usuários existem.
    /// </param>
    bool Verify(string password, string? hashedPassword);

    /// <summary>Indica se o hash foi gerado com parâmetros mais fracos que os atuais e deve ser regerado no próximo login.</summary>
    bool NeedsRehash(string hashedPassword);
}
