using System.Text.Json.Serialization;
using TEC.Core.Responses.Pagination;

namespace TEC.Core.Responses;

/// <summary>
/// Envelope padrão de resposta paginada.
/// </summary>
/// <example>
/// JSON gerado:
/// <code>
/// { "success": true, "statusCode": 200, "data": [...],
///   "pagination": { "page": 1, "pageSize": 10, "totalItems": 42, "totalPages": 5, "hasPreviousPage": false, "hasNextPage": true },
///   "errors": [], "timestamp": "..." }
/// </code>
/// </example>
/// <typeparam name="T">Tipo dos itens.</typeparam>
public sealed record PagedResponse<T> : ApiResponse<IReadOnlyList<T>>
{
    /// <summary>Metadados de paginação.</summary>
    [JsonPropertyOrder(4)]
    public PaginationInfo? Pagination { get; init; }

    /// <summary>Cria a resposta paginada de sucesso.</summary>
    /// <exception cref="ArgumentException">Itens nulos, parâmetros de paginação inválidos ou inconsistentes com os itens.</exception>
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int page, int pageSize, long totalItems, string? message = null)
    {
        // A validação completa (nulos, faixas e consistência) fica centralizada em PagedResult
        var result = new PagedResult<T>(items, page, pageSize, totalItems);
        result.EnsureConsistent();

        return new()
        {
            Success = true,
            StatusCode = 200,
            Message = message,
            Data = result.Items,
            Pagination = result.Pagination
        };
    }

    /// <summary>Cria a resposta paginada a partir de um <see cref="PagedResult{T}"/>.</summary>
    public static PagedResponse<T> Create(PagedResult<T> result, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Create(result.Items, result.Page, result.PageSize, result.TotalItems, message);
    }
}
