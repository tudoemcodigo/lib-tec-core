namespace TEC.Core.Common.Results;

/// <summary>
/// Regras centralizadas de cada <see cref="ErrorType"/>: status HTTP e exposição da mensagem ao cliente.
/// </summary>
public static class ErrorTypeExtensions
{
    /// <summary>Status HTTP correspondente ao tipo de erro.</summary>
    public static int ToHttpStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => 400,
        ErrorType.Unauthorized => 401,
        ErrorType.Forbidden => 403,
        ErrorType.NotFound => 404,
        ErrorType.Conflict => 409,
        ErrorType.BusinessRule => 422,
        ErrorType.TooManyRequests => 429,
        ErrorType.ExternalService => 502,
        ErrorType.Failure => 500,
        _ => throw new ArgumentOutOfRangeException(nameof(type), "Tipo de erro inválido.")
    };

    /// <summary>
    /// Indica se a mensagem e os detalhes do erro podem ser enviados ao cliente.
    /// Falhas internas e de integração são sempre ocultadas (podem conter detalhes de infraestrutura).
    /// </summary>
    public static bool IsExposedToClient(this ErrorType type) =>
        type is not (ErrorType.Failure or ErrorType.ExternalService);
}
