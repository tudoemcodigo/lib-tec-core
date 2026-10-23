using System.Text.Json.Serialization;
using TEC.Core.Common.Results;

namespace TEC.Core.Responses;

/// <summary>
/// Envelope padrão de resposta da API REST (sem dados).
/// </summary>
/// <example>
/// JSON gerado:
/// <code>
/// { "success": false, "statusCode": 404, "message": "Cliente não encontrado", "errors": [...], "timestamp": "2026-09-30T14:00:00+00:00" }
/// </code>
/// </example>
public record ApiResponse
{
    /// <summary>Indica se a operação foi bem-sucedida.</summary>
    [JsonPropertyOrder(0)]
    public bool Success { get; init; }

    /// <summary>Código de status HTTP.</summary>
    [JsonPropertyOrder(1)]
    public int StatusCode
    {
        get;
        init => field = value is >= 100 and <= 599
            ? value
            : throw new ArgumentOutOfRangeException(nameof(StatusCode), "O status HTTP deve estar entre 100 e 599.");
    }

    /// <summary>Mensagem para o consumidor da API.</summary>
    [JsonPropertyOrder(2)]
    public string? Message { get; init; }

    /// <summary>Lista de erros (vazia em caso de sucesso).</summary>
    [JsonPropertyOrder(10)]
    public IReadOnlyList<ApiError> Errors
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(Errors));
    } = [];

    /// <summary>Data/hora (UTC) em que a resposta foi gerada.</summary>
    [JsonPropertyOrder(11)]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Identificador de rastreamento da requisição (opcional). Use <c>response with { TraceId = ... }</c>.</summary>
    [JsonPropertyOrder(12)]
    public string? TraceId { get; init; }

    /// <summary>Sucesso sem dados (HTTP 200).</summary>
    public static ApiResponse Ok(string? message = null) => new() { Success = true, StatusCode = 200, Message = message };

    /// <summary>Falha com status (400 a 599), mensagem e erros informados.</summary>
    public static ApiResponse Fail(int statusCode, string message, params IEnumerable<ApiError> errors) =>
        new() { Success = false, StatusCode = statusCode, Message = message, Errors = ValidateFailure(statusCode, message, errors) };

    /// <summary>Valida os parâmetros de uma resposta de falha e retorna uma cópia imutável dos erros.</summary>
    protected static ApiError[] ValidateFailure(int statusCode, string message, IEnumerable<ApiError> errors)
    {
        if (statusCode is < 400 or > 599)
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Respostas de falha devem ter status HTTP entre 400 e 599.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(errors);

        ApiError[] copy = [.. errors];
        if (copy.Any(e => e is null))
            throw new ArgumentException("A lista de erros não pode conter itens nulos.", nameof(errors));

        return copy;
    }

    /// <summary>Requisição inválida (HTTP 400).</summary>
    public static ApiResponse BadRequest(string message, params IEnumerable<ApiError> errors) => Fail(400, message, errors);

    /// <summary>Erros de validação (HTTP 400).</summary>
    public static ApiResponse ValidationError(params IEnumerable<ApiError> errors) => Fail(400, DefaultMessages.Validation, errors);

    /// <summary>Não autenticado (HTTP 401).</summary>
    public static ApiResponse Unauthorized(string message = DefaultMessages.Unauthorized) => Fail(401, message);

    /// <summary>Sem permissão (HTTP 403).</summary>
    public static ApiResponse Forbidden(string message = DefaultMessages.Forbidden) => Fail(403, message);

    /// <summary>Recurso não encontrado (HTTP 404).</summary>
    public static ApiResponse NotFound(string message = DefaultMessages.NotFound) => Fail(404, message);

    /// <summary>Conflito (HTTP 409).</summary>
    public static ApiResponse Conflict(string message, params IEnumerable<ApiError> errors) => Fail(409, message, errors);

    /// <summary>Erro interno (HTTP 500). Não exponha detalhes da exceção ao cliente.</summary>
    public static ApiResponse InternalError(string message = DefaultMessages.InternalError) => Fail(500, message);

    /// <summary>Converte um <see cref="Result"/> em resposta, mapeando o status HTTP pelo tipo do erro.</summary>
    public static ApiResponse FromResult(Result result, string? successMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
            return Ok(successMessage);

        var (status, message, errors) = MapErrors(result.Errors);
        return Fail(status, message, errors);
    }

    /// <summary>
    /// Mapeia o tipo do primeiro erro para status HTTP, mensagem e erros expostos ao cliente.
    /// Erros internos (<see cref="ErrorType.Failure"/>, HTTP 500) e de integração (<see cref="ErrorType.ExternalService"/>, HTTP 502)
    /// nunca expõem mensagem ou detalhes: registre-os em log e retorne apenas a mensagem genérica.
    /// </summary>
    /// <remarks>
    /// Se <b>qualquer</b> erro da lista for interno (e não apenas o primeiro), a resposta inteira é tratada como interna:
    /// nenhum erro é exposto. Isso evita vazar detalhes de infraestrutura em um <see cref="Result"/> com erros de tipos misturados
    /// (ex.: <c>Result.Failure(Error.Validation(...), Error.Failure("DB", ex.Message))</c>).
    /// </remarks>
    protected static (int StatusCode, string Message, IEnumerable<ApiError> Errors) MapErrors(IReadOnlyList<Error> errors)
    {
        if (errors.Any(e => !e.Type.IsExposedToClient()))
        {
            // Falha interna (500) prevalece sobre falha de integração (502)
            var hiddenType = errors.Any(e => e.Type == ErrorType.Failure) ? ErrorType.Failure : ErrorType.ExternalService;
            return (hiddenType.ToHttpStatusCode(), GenericMessage(hiddenType), []);
        }

        var first = errors[0];
        int status = first.Type.ToHttpStatusCode();
        var message = first.Type == ErrorType.Validation && errors.Count > 1 ? DefaultMessages.Validation : first.Message;
        return (status, message, errors.Select(ApiError.FromError));
    }

    /// <summary>
    /// Converte uma exceção em resposta padronizada. Exceções de <see cref="Exceptions.AppException"/> usam o status e
    /// os erros definidos nelas; qualquer outra exceção vira HTTP 500 com mensagem genérica (sem detalhes internos).
    /// </summary>
    /// <param name="exception">Exceção capturada (ex.: em um middleware global de erros).</param>
    /// <param name="traceId">Identificador de rastreamento opcional para correlacionar com os logs.</param>
    public static ApiResponse FromException(Exception exception, string? traceId = null)
    {
        var (status, message, errors) = MapException(exception);
        return Fail(status, message, errors) with { TraceId = traceId };
    }

    /// <summary>Mapeia a exceção para status, mensagem e erros seguros para o cliente.</summary>
    protected static (int StatusCode, string Message, IEnumerable<ApiError> Errors) MapException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is Exceptions.AppException appException
            ? MapErrors(appException.Errors)
            : (500, DefaultMessages.InternalError, []);
    }

    private static string GenericMessage(ErrorType type) =>
        type == ErrorType.ExternalService ? DefaultMessages.ExternalService : DefaultMessages.InternalError;

    /// <summary>
    /// Mensagens padrão em pt-BR, fonte única dos textos genéricos de erro: as exceções do TEC.Core e os demais
    /// componentes TEC usam estas constantes, para o cliente receber o mesmo texto para o mesmo status.
    /// </summary>
    public static class DefaultMessages
    {
        /// <summary>Erros de validação (HTTP 400).</summary>
        public const string Validation = "Um ou mais erros de validação ocorreram.";

        /// <summary>Não autenticado (HTTP 401).</summary>
        public const string Unauthorized = "Não autenticado.";

        /// <summary>Sem permissão (HTTP 403).</summary>
        public const string Forbidden = "Você não tem permissão para realizar esta operação.";

        /// <summary>Recurso não encontrado (HTTP 404).</summary>
        public const string NotFound = "Recurso não encontrado.";

        /// <summary>Erro interno (HTTP 500), sem detalhes.</summary>
        public const string InternalError = "Ocorreu um erro interno. Tente novamente mais tarde.";

        /// <summary>Falha em serviço externo (HTTP 502), sem detalhes.</summary>
        public const string ExternalService = "Serviço externo indisponível. Tente novamente mais tarde.";
    }
}

/// <summary>
/// Envelope padrão de resposta da API REST com dados.
/// </summary>
/// <typeparam name="T">Tipo dos dados retornados.</typeparam>
public record ApiResponse<T> : ApiResponse
{
    /// <summary>Dados retornados.</summary>
    [JsonPropertyOrder(3)]
    public T? Data { get; init; }

    /// <summary>Sucesso com dados (HTTP 200).</summary>
    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, StatusCode = 200, Message = message, Data = data };

    /// <summary>Recurso criado (HTTP 201).</summary>
    public static ApiResponse<T> Created(T data, string? message = null) =>
        new() { Success = true, StatusCode = 201, Message = message, Data = data };

    /// <summary>Falha com status e erros informados.</summary>
    public static new ApiResponse<T> Fail(int statusCode, string message, params IEnumerable<ApiError> errors) =>
        new() { Success = false, StatusCode = statusCode, Message = message, Errors = ValidateFailure(statusCode, message, errors) };

    /// <inheritdoc cref="ApiResponse.BadRequest"/>
    public static new ApiResponse<T> BadRequest(string message, params IEnumerable<ApiError> errors) => Fail(400, message, errors);

    /// <inheritdoc cref="ApiResponse.ValidationError"/>
    public static new ApiResponse<T> ValidationError(params IEnumerable<ApiError> errors) => Fail(400, DefaultMessages.Validation, errors);

    /// <inheritdoc cref="ApiResponse.Unauthorized"/>
    public static new ApiResponse<T> Unauthorized(string message = DefaultMessages.Unauthorized) => Fail(401, message);

    /// <inheritdoc cref="ApiResponse.Forbidden"/>
    public static new ApiResponse<T> Forbidden(string message = DefaultMessages.Forbidden) => Fail(403, message);

    /// <inheritdoc cref="ApiResponse.NotFound"/>
    public static new ApiResponse<T> NotFound(string message = DefaultMessages.NotFound) => Fail(404, message);

    /// <inheritdoc cref="ApiResponse.Conflict"/>
    public static new ApiResponse<T> Conflict(string message, params IEnumerable<ApiError> errors) => Fail(409, message, errors);

    /// <inheritdoc cref="ApiResponse.InternalError"/>
    public static new ApiResponse<T> InternalError(string message = DefaultMessages.InternalError) => Fail(500, message);

    /// <summary>Converte um <see cref="Result{T}"/> em resposta, mapeando o status HTTP pelo tipo do erro.</summary>
    public static ApiResponse<T> FromResult(Result<T> result, string? successMessage = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
            return Ok(result.Value, successMessage);

        var (status, message, errors) = MapErrors(result.Errors);
        return Fail(status, message, errors);
    }

    /// <inheritdoc cref="ApiResponse.FromException"/>
    public static new ApiResponse<T> FromException(Exception exception, string? traceId = null)
    {
        var (status, message, errors) = MapException(exception);
        return Fail(status, message, errors) with { TraceId = traceId };
    }
}
