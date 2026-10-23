using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Violação de regra de negócio (HTTP 422). Os dados são válidos, mas a operação não é permitida.
/// </summary>
/// <example>
/// <code>
/// if (account.Balance &lt; amount)
///     throw new BusinessException("SALDO_INSUFICIENTE", "Saldo insuficiente para realizar a transferência.");
/// </code>
/// </example>
public class BusinessException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "REGRA_DE_NEGOCIO";

    /// <summary>Cria a exceção com o código padrão.</summary>
    public BusinessException(string message)
        : base(DefaultCode, message, ErrorType.BusinessRule)
    {
    }

    /// <summary>Cria a exceção com código específico.</summary>
    public BusinessException(string code, string message, Exception? innerException = null)
        : base(code, message, ErrorType.BusinessRule, innerException)
    {
    }
}
