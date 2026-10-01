namespace TEC.Core.Common.Results;

/// <summary>
/// Representa um erro de negócio ou de validação.
/// </summary>
/// <param name="Code">Código do erro (ex.: "CLIENTE_NAO_ENCONTRADO"). Obrigatório.</param>
/// <param name="Message">Mensagem descritiva. Obrigatória.</param>
/// <param name="Type">Categoria do erro.</param>
/// <param name="Field">Campo relacionado ao erro (opcional, usado em validações).</param>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure, string? Field = null)
{
    // A validação fica no inicializador (construtor) e também no init (expressão "with"),
    // para que "erro with { Code = "" }" não produza um erro inválido

    /// <summary>Código do erro.</summary>
    public string Code
    {
        get;
        init => field = RequireText(value, nameof(Code));
    } = RequireText(Code, nameof(Code));

    /// <summary>Mensagem descritiva.</summary>
    public string Message
    {
        get;
        init => field = RequireText(value, nameof(Message));
    } = RequireText(Message, nameof(Message));

    /// <summary>Categoria do erro.</summary>
    public ErrorType Type
    {
        get;
        init => field = RequireDefined(value);
    } = RequireDefined(Type);

    /// <summary>Cria um erro genérico.</summary>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    /// <summary>Cria um erro de validação.</summary>
    public static Error Validation(string code, string message, string? field = null) => new(code, message, ErrorType.Validation, field);

    /// <summary>Cria um erro de recurso não encontrado.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Cria um erro de conflito.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Cria um erro de não autenticado.</summary>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>Cria um erro de acesso negado.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Cria um erro de regra de negócio.</summary>
    public static Error BusinessRule(string code, string message) => new(code, message, ErrorType.BusinessRule);

    /// <summary>Cria um erro de limite de requisições excedido.</summary>
    public static Error TooManyRequests(string code, string message) => new(code, message, ErrorType.TooManyRequests);

    /// <summary>Cria um erro de falha em serviço externo (mensagem não é exposta ao cliente).</summary>
    public static Error ExternalService(string code, string message) => new(code, message, ErrorType.ExternalService);

    private static string RequireText(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    private static ErrorType RequireDefined(ErrorType type) =>
        Enum.IsDefined(type) ? type : throw new ArgumentOutOfRangeException(nameof(Type), "Tipo de erro inválido.");
}
