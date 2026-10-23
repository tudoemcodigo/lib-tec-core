using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Usuário autenticado, mas sem permissão para a operação (HTTP 403).
/// </summary>
/// <remarks>
/// Não confundir com <see cref="UnauthorizedAccessException"/> do .NET, que se refere a permissões de arquivo/sistema.
/// Não informe na mensagem qual permissão falta (evita mapear o modelo de autorização).
/// </remarks>
public class ForbiddenException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "ACESSO_NEGADO";

    /// <summary>Mensagem padrão.</summary>
    public const string DefaultMessage = TEC.Core.Responses.ApiResponse.DefaultMessages.Forbidden;

    /// <summary>Cria a exceção com código e mensagem padrão.</summary>
    public ForbiddenException()
        : base(DefaultCode, DefaultMessage, ErrorType.Forbidden)
    {
    }

    /// <summary>Cria a exceção com código e mensagem específicos.</summary>
    public ForbiddenException(string code, string message, Exception? innerException = null)
        : base(code, message, ErrorType.Forbidden, innerException)
    {
    }
}
