namespace TEC.Core.Responses.Pagination;

/// <summary>
/// Extensões para paginar consultas (<see cref="IQueryable{T}"/>) e coleções em memória.
/// </summary>
/// <remarks>
/// Página e tamanho normalmente vêm da query string (entrada não confiável): são validados e o
/// tamanho da página é limitado a <see cref="DefaultMaxPageSize"/>, evitando consultas que retornem a tabela inteira.
/// </remarks>
public static class PaginationExtensions
{
    /// <summary>Tamanho máximo padrão de página.</summary>
    public const int DefaultMaxPageSize = 1000;

    /// <summary>Aplica Skip/Take conforme a página (começando em 1).</summary>
    public static IQueryable<T> Paginate<T>(this IQueryable<T> query, int page, int pageSize, int maxPageSize = DefaultMaxPageSize)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Skip(CalculateSkip(page, pageSize, maxPageSize)).Take(pageSize);
    }

    /// <summary>
    /// Pagina uma consulta (ex.: Entity Framework) e retorna o <see cref="PagedResult{T}"/>, executando no banco
    /// apenas um <c>COUNT</c> e a busca da página (<c>Skip</c>/<c>Take</c>), sem materializar a tabela inteira.
    /// </summary>
    /// <remarks>
    /// Ordene a consulta antes (<c>OrderBy</c>): sem ordenação, o banco pode devolver páginas inconsistentes.
    /// Esta sobrecarga é síncrona; com EF Core assíncrono, use <see cref="Paginate{T}"/> com <c>CountAsync</c>/<c>ToListAsync</c>.
    /// Se a página pedida estiver além do total, devolve a página vazia sem consultar os itens.
    /// </remarks>
    public static PagedResult<T> ToPagedResult<T>(this IQueryable<T> query, int page, int pageSize, int maxPageSize = DefaultMaxPageSize)
    {
        ArgumentNullException.ThrowIfNull(query);
        int skip = CalculateSkip(page, pageSize, maxPageSize);

        long totalItems = query.LongCount();
        List<T> items = totalItems > skip ? [.. query.Skip(skip).Take(pageSize)] : [];
        return new PagedResult<T>(items, page, pageSize, totalItems);
    }

    /// <summary>Pagina uma coleção em memória e retorna o <see cref="PagedResult{T}"/>.</summary>
    /// <remarks>Para <see cref="IQueryable{T}"/> é usada a sobrecarga específica, que não materializa a consulta inteira.</remarks>
    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int page, int pageSize, int maxPageSize = DefaultMaxPageSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        int skip = CalculateSkip(page, pageSize, maxPageSize);

        var list = source as IReadOnlyCollection<T> ?? source.ToList();
        var items = list.Skip(skip).Take(pageSize).ToList();
        return new PagedResult<T>(items, page, pageSize, list.Count);
    }

    /// <summary>Valida os parâmetros e calcula quantos itens pular, sem risco de estouro aritmético.</summary>
    public static int CalculateSkip(int page, int pageSize, int maxPageSize = DefaultMaxPageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, maxPageSize);

        long skip = (long)(page - 1) * pageSize;
        if (skip > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page), "Página fora do intervalo permitido.");

        return (int)skip;
    }
}
