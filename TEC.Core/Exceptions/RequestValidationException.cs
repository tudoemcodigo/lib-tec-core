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
    public const string DefaultMessage = "Um ou mais erros de validação ocorreram.";

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
    /// O primeiro erro é obrigatório na assinatura: <c>new RequestValidationException()</c> não compila
    /// (antes compilava e falhava somente em tempo de execução).
    /// </remarks>
    public RequestValidationException(Error error, params IEnumerable<Error> additionalErrors)
        : base(DefaultMessage, RequireValidationErrors([error, .. additionalErrors ?? throw new ArgumentNullException(nameof(additionalErrors))]))
    {
    }

    /// <summary>Cria a exceção com a lista de erros (não vazia; todos do tipo <see cref="ErrorType.Validation"/>).</summary>
    public RequestValidationException(IEnumerable<Error> errors)
        : base(DefaultMessage, RequireValidationErrors(errors))
    {
    }

    private static string RequireField(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return field;
    }

    private static Error[] RequireValidationErrors(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Error[] copy = [.. errors];
        if (copy.Any(e => e is not null && e.Type != ErrorType.Validation))
            throw new ArgumentException("Todos os erros devem ser do tipo Validation.", nameof(errors));

        return copy;
    }
}
