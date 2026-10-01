using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Base de todas as exceções da aplicação. Cada exceção possui um código, uma categoria (<see cref="ErrorType"/>)
/// e a lista de erros, permitindo conversão direta para <c>ApiResponse</c> e <see cref="Result"/>.
/// </summary>
/// <remarks>
/// <para>Use exceções para situações excepcionais; para fluxos esperados (ex.: validação de formulário),
/// prefira retornar <see cref="Result"/>.</para>
/// <para>Segurança: a mensagem destas exceções pode ser exibida ao cliente (exceto em
/// <see cref="IntegrationException"/> e <see cref="InvalidConfigurationException"/>, que são sempre ocultadas).
/// Nunca inclua dados pessoais, senhas, tokens ou detalhes de infraestrutura na mensagem.</para>
/// </remarks>
public abstract class AppException : Exception
{
    /// <summary>Cria a exceção com um único erro.</summary>
    /// <param name="code">Código do erro (ex.: "SALDO_INSUFICIENTE").</param>
    /// <param name="message">Mensagem do erro.</param>
    /// <param name="errorType">Categoria do erro.</param>
    /// <param name="innerException">Exceção original (mantida apenas para log).</param>
    protected AppException(string code, string message, ErrorType errorType, Exception? innerException = null)
        : this(message, [new Error(code, message, errorType)], innerException)
    {
    }

    /// <summary>Cria a exceção com vários erros (todos devem ser da mesma categoria).</summary>
    protected AppException(string message, IEnumerable<Error> errors, Exception? innerException = null)
        : base(RequireMessage(message), innerException)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Error[] copy = [.. errors];

        if (copy.Length == 0)
            throw new ArgumentException("Informe ao menos um erro.", nameof(errors));
        if (copy.Any(e => e is null))
            throw new ArgumentException("A lista de erros não pode conter itens nulos.", nameof(errors));
        if (copy.Any(e => e.Type != copy[0].Type))
            throw new ArgumentException("Todos os erros devem ser da mesma categoria.", nameof(errors));

        Errors = copy;
    }

    /// <summary>Código do (primeiro) erro.</summary>
    public string Code => Errors[0].Code;

    /// <summary>Categoria do erro.</summary>
    public ErrorType ErrorType => Errors[0].Type;

    /// <summary>Status HTTP correspondente.</summary>
    public int StatusCode => ErrorType.ToHttpStatusCode();

    /// <summary>Lista de erros (imutável).</summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>Converte para <see cref="Result"/> de falha.</summary>
    public Result ToResult() => Result.Failure([.. Errors]);

    /// <summary>Converte para <see cref="Result{T}"/> de falha.</summary>
    public Result<T> ToResult<T>() => Result<T>.Failure([.. Errors]);

    private static string RequireMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return message;
    }
}
