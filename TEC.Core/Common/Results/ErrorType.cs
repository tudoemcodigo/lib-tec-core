namespace TEC.Core.Common.Results;

/// <summary>
/// Categoria do erro. Define o status HTTP e se a mensagem pode ser exibida ao cliente
/// (veja <see cref="ErrorTypeExtensions"/>).
/// </summary>
public enum ErrorType
{
    /// <summary>Falha interna (HTTP 500). A mensagem nunca é exposta ao cliente.</summary>
    Failure = 0,

    /// <summary>Erro de validação dos dados de entrada (HTTP 400).</summary>
    Validation = 1,

    /// <summary>Recurso não encontrado (HTTP 404).</summary>
    NotFound = 2,

    /// <summary>Conflito com o estado atual do recurso (HTTP 409).</summary>
    Conflict = 3,

    /// <summary>Não autenticado (HTTP 401).</summary>
    Unauthorized = 4,

    /// <summary>Sem permissão (HTTP 403).</summary>
    Forbidden = 5,

    /// <summary>Regra de negócio violada (HTTP 422).</summary>
    BusinessRule = 6,

    /// <summary>Limite de requisições excedido (HTTP 429).</summary>
    TooManyRequests = 7,

    /// <summary>Falha em serviço externo/integração (HTTP 502). A mensagem nunca é exposta ao cliente.</summary>
    ExternalService = 8
}
