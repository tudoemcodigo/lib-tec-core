using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Usuário não autenticado ou credenciais/token inválidos (HTTP 401).
/// </summary>
/// <remarks>
/// Use sempre a mesma mensagem para usuário inexistente e senha incorreta: mensagens diferentes
/// permitem descobrir quais usuários existem (enumeração de contas).
/// </remarks>
public class UnauthenticatedException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "NAO_AUTENTICADO";

    /// <summary>Mensagem padrão (genérica de propósito).</summary>
    public const string DefaultMessage = "Credenciais inválidas ou sessão expirada.";

    /// <summary>Cria a exceção com código e mensagem padrão.</summary>
    public UnauthenticatedException()
        : base(DefaultCode, DefaultMessage, ErrorType.Unauthorized)
    {
    }

    /// <summary>Cria a exceção com código e mensagem específicos.</summary>
    public UnauthenticatedException(string code, string message, Exception? innerException = null)
        : base(code, message, ErrorType.Unauthorized, innerException)
    {
    }
}
