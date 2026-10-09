namespace TEC.Core.Common.Results;

/// <summary>
/// Resultado de uma operação sem valor de retorno. Permite indicar sucesso ou falha sem lançar exceções.
/// </summary>
public class Result
{
    private static readonly Result SuccessInstance = new(true, []);

    // private protected: só Result<T> (neste assembly) deriva de Result; consumidores não criam subclasses com estado inválido
    private protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        // Cópia defensiva: alterações posteriores no array do chamador não afetam o resultado (imutável)
        errors = [.. errors];
        if (errors.Any(e => e is null))
            throw new ArgumentException("A lista de erros não pode conter itens nulos.", nameof(errors));

        if (isSuccess && errors.Count > 0)
            throw new InvalidOperationException("Um resultado de sucesso não pode conter erros.");
        if (!isSuccess && errors.Count == 0)
            throw new InvalidOperationException("Um resultado de falha deve conter ao menos um erro.");

        IsSuccess = isSuccess;
        Errors = errors;
    }

    /// <summary>Indica se a operação foi bem-sucedida.</summary>
    public bool IsSuccess { get; }

    /// <summary>Indica se a operação falhou.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Lista de erros (vazia em caso de sucesso).</summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>Primeiro erro da lista, ou <c>null</c> em caso de sucesso.</summary>
    public Error? Error => Errors.Count > 0 ? Errors[0] : null;

    /// <summary>Cria um resultado de sucesso.</summary>
    public static Result Success() => SuccessInstance;

    /// <summary>Cria um resultado de falha com um ou mais erros.</summary>
    public static Result Failure(params Error[] errors) => new(false, errors);

    /// <summary>Cria um resultado de sucesso com valor.</summary>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>Cria um resultado de falha tipado.</summary>
    public static Result<T> Failure<T>(params Error[] errors) => Result<T>.Failure(errors);

    /// <summary>Propaga os erros desta falha para um resultado sem valor (descarta o tipo do valor).</summary>
    /// <exception cref="InvalidOperationException">O resultado é de sucesso.</exception>
    public Result ToFailure() => Failure([.. FailureErrors()]);

    /// <summary>Propaga os erros desta falha para um resultado de outro tipo de valor.</summary>
    /// <exception cref="InvalidOperationException">O resultado é de sucesso.</exception>
    public Result<TOut> ToFailure<TOut>() => Result<TOut>.Failure([.. FailureErrors()]);

    /// <summary>Encadeia outra operação, executada só se esta teve sucesso.</summary>
    /// <param name="next">Próxima operação.</param>
    /// <returns>O resultado de <paramref name="next"/> ou esta falha.</returns>
    public Result Bind(Func<Result> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? next() : this;
    }

    /// <summary>Encadeia uma operação que produz um valor, executada só se esta teve sucesso.</summary>
    /// <typeparam name="TOut">Tipo do valor produzido.</typeparam>
    /// <param name="next">Próxima operação.</param>
    /// <returns>O resultado de <paramref name="next"/> ou os erros desta falha.</returns>
    public Result<TOut> Bind<TOut>(Func<Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? next() : ToFailure<TOut>();
    }

    /// <summary>Encadeia uma operação assíncrona, executada só se esta teve sucesso.</summary>
    /// <param name="next">Próxima operação.</param>
    /// <returns>O resultado de <paramref name="next"/> ou esta falha.</returns>
    public async Task<Result> BindAsync(Func<Task<Result>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? await next().ConfigureAwait(false) : this;
    }

    private IReadOnlyList<Error> FailureErrors() => IsFailure
        ? Errors
        : throw new InvalidOperationException("Não é possível propagar os erros de um resultado de sucesso.");

    /// <summary>Converte um erro em resultado de falha (permite <c>return Error.NotFound(...);</c>).</summary>
    /// <param name="error">Erro da falha.</param>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Resultado de uma operação com valor de retorno.
/// </summary>
/// <typeparam name="T">Tipo do valor retornado.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, bool isSuccess, IReadOnlyList<Error> errors) : base(isSuccess, errors)
    {
        _value = value;
    }

    /// <summary>Valor retornado. Lança exceção se acessado em caso de falha.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado com falha.");

    /// <summary>Cria um resultado de sucesso com valor.</summary>
    public static Result<T> Success(T value) => new(value, true, []);

    /// <summary>Cria um resultado de falha.</summary>
    public static new Result<T> Failure(params Error[] errors) => new(default, false, errors);

    /// <summary>Transforma o valor em caso de sucesso, propagando os erros em caso de falha.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess ? Result<TOut>.Success(mapper(_value!)) : ToFailure<TOut>();
    }

    /// <summary>Encadeia uma operação que depende do valor, executada só em caso de sucesso.</summary>
    /// <typeparam name="TOut">Tipo do valor produzido.</typeparam>
    /// <param name="next">Próxima operação, que recebe o valor.</param>
    /// <returns>O resultado de <paramref name="next"/> ou os erros desta falha.</returns>
    /// <example><code>return Pedido.Criar(dados).Bind(p => estoque.Reservar(p)).Map(p => p.Id);</code></example>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? next(_value!) : ToFailure<TOut>();
    }

    /// <summary>Encadeia uma operação sem valor que depende do valor, executada só em caso de sucesso.</summary>
    /// <param name="next">Próxima operação, que recebe o valor.</param>
    /// <returns>O resultado de <paramref name="next"/> ou os erros desta falha.</returns>
    public Result Bind(Func<T, Result> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? next(_value!) : ToFailure();
    }

    /// <summary>Encadeia uma operação assíncrona que depende do valor, executada só em caso de sucesso.</summary>
    /// <typeparam name="TOut">Tipo do valor produzido.</typeparam>
    /// <param name="next">Próxima operação, que recebe o valor.</param>
    /// <returns>O resultado de <paramref name="next"/> ou os erros desta falha.</returns>
    public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? await next(_value!).ConfigureAwait(false) : ToFailure<TOut>();
    }

    /// <summary>Encadeia uma operação assíncrona sem valor que depende do valor, executada só em caso de sucesso.</summary>
    /// <param name="next">Próxima operação, que recebe o valor.</param>
    /// <returns>O resultado de <paramref name="next"/> ou os erros desta falha.</returns>
    public async Task<Result> BindAsync(Func<T, Task<Result>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? await next(_value!).ConfigureAwait(false) : ToFailure();
    }

    /// <summary>Mantém o sucesso só se o valor atender à condição; caso contrário, falha com <paramref name="error"/>.</summary>
    /// <param name="predicate">Condição sobre o valor.</param>
    /// <param name="error">Erro quando a condição não é atendida.</param>
    /// <returns>Este resultado, ou a falha com <paramref name="error"/>.</returns>
    public Result<T> Ensure(Func<T, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        return IsFailure || predicate(_value!) ? this : Failure(error);
    }

    /// <summary>Executa uma das funções conforme o resultado.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IReadOnlyList<Error>, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(Errors);
    }

    /// <summary>Converte um valor em resultado de sucesso (permite <c>return valor;</c>).</summary>
    /// <param name="value">Valor do sucesso.</param>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Converte um erro em resultado de falha tipado.</summary>
    /// <param name="error">Erro da falha.</param>
    public static implicit operator Result<T>(Error error) => Failure(error);
}
