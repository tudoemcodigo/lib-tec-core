using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Conflito com o estado atual do recurso (HTTP 409). Ex.: e-mail já cadastrado, pedido já faturado.
/// </summary>
public class ConflictException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "CONFLITO";

    /// <summary>Cria a exceção com o código padrão.</summary>
    public ConflictException(string message)
        : base(DefaultCode, message, ErrorType.Conflict)
    {
    }

    /// <summary>Cria a exceção com código específico.</summary>
    public ConflictException(string code, string message, Exception? innerException = null)
        : base(code, message, ErrorType.Conflict, innerException)
    {
    }
}
