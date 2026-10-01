namespace TEC.Core.Responses.Pagination;

/// <summary>
/// Metadados de paginação retornados pela API.
/// </summary>
public sealed record PaginationInfo
{
    /// <summary>Página atual (começa em 1).</summary>
    public int Page { get; init; }

    /// <summary>Itens por página.</summary>
    public int PageSize { get; init; }

    /// <summary>Total de itens em todas as páginas.</summary>
    public long TotalItems { get; init; }

    /// <summary>Total de páginas.</summary>
    public int TotalPages { get; init; }

    /// <summary>Indica se existe página anterior.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>Indica se existe próxima página.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Cria os metadados calculando o total de páginas.</summary>
    public static PaginationInfo Create(int page, int pageSize, long totalItems)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalItems);

        return new PaginationInfo
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            // Divisão com arredondamento para cima sem somar antes (totalItems + pageSize estouraria perto de long.MaxValue)
            TotalPages = (int)Math.Min(int.MaxValue, totalItems / pageSize + (totalItems % pageSize == 0 ? 0 : 1))
        };
    }
}
