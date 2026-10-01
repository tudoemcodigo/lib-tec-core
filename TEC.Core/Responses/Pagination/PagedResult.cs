namespace TEC.Core.Responses.Pagination;

/// <summary>
/// Resultado paginado, para ser retornado por repositórios/serviços e convertido em <see cref="PagedResponse{T}"/>.
/// </summary>
/// <typeparam name="T">Tipo dos itens.</typeparam>
/// <param name="Items">Itens da página (no máximo <paramref name="PageSize"/>).</param>
/// <param name="Page">Página atual (começa em 1).</param>
/// <param name="PageSize">Itens por página (maior que zero).</param>
/// <param name="TotalItems">Total de itens em todas as páginas (não pode ser menor que a quantidade de itens da página).</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems)
{
    // Validação no inicializador (construtor) e no init (expressão "with")

    /// <summary>Itens da página.</summary>
    public IReadOnlyList<T> Items
    {
        get;
        init => field = RequireItems(value);
    } = RequireItems(Items);

    /// <summary>Página atual.</summary>
    public int Page
    {
        get;
        init => field = RequirePage(value);
    } = RequirePage(Page);

    /// <summary>Itens por página.</summary>
    public int PageSize
    {
        get;
        init => field = RequirePageSize(value);
    } = RequirePageSize(PageSize);

    /// <summary>Total de itens.</summary>
    public long TotalItems
    {
        get;
        init => field = RequireTotalItems(value);
    } = RequireTotalItems(TotalItems);

    /// <summary>Metadados de paginação.</summary>
    public PaginationInfo Pagination => PaginationInfo.Create(Page, PageSize, TotalItems);

    /// <summary>Resultado vazio.</summary>
    public static PagedResult<T> Empty(int page = 1, int pageSize = 10) => new([], page, pageSize, 0);

    /// <summary>Transforma os itens (ex.: entidade → DTO) mantendo a paginação.</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return new(Items.Select(mapper).ToList(), Page, PageSize, TotalItems);
    }

    private static IReadOnlyList<T> RequireItems(IReadOnlyList<T> items) =>
        items ?? throw new ArgumentNullException(nameof(Items));

    private static int RequirePage(int page) =>
        page >= 1 ? page : throw new ArgumentOutOfRangeException(nameof(Page), "A página deve ser maior ou igual a 1.");

    private static int RequirePageSize(int pageSize) =>
        pageSize >= 1 ? pageSize : throw new ArgumentOutOfRangeException(nameof(PageSize), "O tamanho da página deve ser maior que zero.");

    private static long RequireTotalItems(long totalItems) =>
        totalItems >= 0 ? totalItems : throw new ArgumentOutOfRangeException(nameof(TotalItems), "O total de itens não pode ser negativo.");

    /// <summary>Verifica a consistência entre os itens e os totais (chamado pelas fábricas de resposta).</summary>
    internal void EnsureConsistent()
    {
        if (Items.Count > PageSize)
            throw new ArgumentException("A página contém mais itens que o tamanho da página.", nameof(Items));
        if (Items.Count > TotalItems)
            throw new ArgumentException("A página contém mais itens que o total informado.", nameof(TotalItems));
    }
}
