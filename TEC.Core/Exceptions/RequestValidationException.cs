using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Dados de entrada inválidos, com um ou mais erros por campo (HTTP 400).
/// </summary>
/// <remarks>
/// Nome diferente de <c>System.ComponentModel.DataAnnotations.ValidationException</c> (nativa) para evitar conflito.
/// Não repita o valor recebido na mensagem (pode conter dados pessoais ou conteúdo malicioso).
/// </remarks>
/// <example>
/// <code>
/// throw new RequestValidationException(
///     Error.Validation("CPF_INVALIDO", "CPF inválido.", "cpf"),
///     Error.Validation("EMAIL_OBRIGATORIO", "E-mail é obrigatório.", "email"));
/// </code>
/// </example>
public class RequestValidationException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "VALIDACAO";

    /// <summary>Mensagem padrão quando há vários erros.</summary>
    public const string DefaultMessage = TEC.Core.Responses.ApiResponse.DefaultMessages.Validation;

    /// <summary>Cria a exceção para um único campo.</summary>
    /// <param name="field">Nome do campo (ex.: "email").</param>
    /// <param name="message">Mensagem do erro.</param>
    /// <param name="code">Código do erro.</param>
    public RequestValidationException(string field, string message, string code = DefaultCode)
        : base(message, [Error.Validation(code, message, RequireField(field))])
    {
    }

    /// <summary>Cria a exceção com um ou mais erros (todos devem ser do tipo <see cref="ErrorType.Validation"/>).</summary>
    /// <remarks>
    /// O primeiro erro é obrigatório na assinatura: <c>new RequestValidationException()</c> não compila,
    /// em vez de falhar somente em tempo de execução.
    /// </remarks>
    public RequestValidationException(Error error, params IEnumerable<Error> additionalErrors)
        : base(DefaultMessage, [error, .. additionalErrors ?? throw new ArgumentNullException(nameof(additionalErrors))])
    {
        RequireValidationErrors();
    }

    /// <summary>Cria a exceção com a lista de erros (não vazia; todos do tipo <see cref="ErrorType.Validation"/>).</summary>
    public RequestValidationException(IEnumerable<Error> errors)
        : base(DefaultMessage, errors)
    {
        RequireValidationErrors();
    }

    private static string RequireField(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return field;
    }

    // A base já copiou a lista uma única vez e garantiu que não está vazia, sem nulos e com uma só categoria:
    // basta conferir a categoria do primeiro erro
    private void RequireValidationErrors()
    {
        if (ErrorType != ErrorType.Validation)
            throw new ArgumentException("Todos os erros devem ser do tipo Validation.", "errors");
    }
}
