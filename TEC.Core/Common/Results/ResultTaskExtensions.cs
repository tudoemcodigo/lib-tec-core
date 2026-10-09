namespace TEC.Core.Common.Results;

/// <summary>
/// Encadeamento sobre <see cref="Task{TResult}"/> de <see cref="Result{T}"/>: compõe operações assíncronas sem repetir
/// <c>if (r.IsFailure) return r.ToFailure&lt;T&gt;();</c> a cada passo.
/// </summary>
/// <example>
/// <code>
/// return await CarregarAsync(id, ct)
///     .BindAsync(pedido => pedido.Aprovar(agora))
///     .MapAsync(pedido => new PedidoAprovadoDto(pedido.Id));
/// </code>
/// </example>
public static class ResultTaskExtensions
{
    /// <summary>Aguarda o resultado e encadeia uma operação síncrona que depende do valor.</summary>
    /// <typeparam name="T">Tipo do valor de entrada.</typeparam>
    /// <typeparam name="TOut">Tipo do valor produzido.</typeparam>
    /// <param name="result">Resultado assíncrono.</param>
    /// <param name="next">Próxima operação.</param>
    /// <returns>O resultado de <paramref name="next"/> ou os erros da falha.</returns>
    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> result, Func<T, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        return (await result.ConfigureAwait(false)).Bind(next);
    }

    /// <summary>Aguarda o resultado e encadeia uma operação assíncrona que depende do valor.</summary>
    /// <typeparam name="T">Tipo do valor de entrada.</typeparam>
    /// <typeparam name="TOut">Tipo do valor produzido.</typeparam>
    /// <param name="result">Resultado assíncrono.</param>
    /// <param name="next">Próxima operação.</param>
    /// <returns>O resultado de <paramref name="next"/> ou os erros da falha.</returns>
    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> result, Func<T, Task<Result<TOut>>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        return await (await result.ConfigureAwait(false)).BindAsync(next).ConfigureAwait(false);
    }

    /// <summary>Aguarda o resultado e encadeia uma operação sem valor que depende do valor.</summary>
    /// <typeparam name="T">Tipo do valor de entrada.</typeparam>
    /// <param name="result">Resultado assíncrono.</param>
    /// <param name="next">Próxima operação.</param>
    /// <returns>O resultado de <paramref name="next"/> ou os erros da falha.</returns>
    public static async Task<Result> BindAsync<T>(this Task<Result<T>> result, Func<T, Result> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        return (await result.ConfigureAwait(false)).Bind(next);
    }

    /// <summary>Aguarda o resultado e transforma o valor em caso de sucesso.</summary>
    /// <typeparam name="T">Tipo do valor de entrada.</typeparam>
    /// <typeparam name="TOut">Tipo do valor produzido.</typeparam>
    /// <param name="result">Resultado assíncrono.</param>
    /// <param name="mapper">Transformação do valor.</param>
    /// <returns>O valor transformado ou os erros da falha.</returns>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> result, Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(result);
        return (await result.ConfigureAwait(false)).Map(mapper);
    }
}
