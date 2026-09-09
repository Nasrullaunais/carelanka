using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

/// <summary>
/// One page of a list. The shape is the group-owned <c>PagedResult</c> schema, published
/// byte-identically by all five specs, so nothing here is this component's to change.
/// </summary>
/// <remarks>
/// Built by Patient Management (M4) because <c>GET /api/patients</c> is the first paged
/// endpoint in the API. It lives in DTOs/Common rather than DTOs/Patient precisely because it
/// is not ours — the next component that pages a list imports this one instead of writing a
/// second.
/// </remarks>
public class PagedResult<T>
{
    [Required]
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    [Required]
    public int Page { get; set; }

    [Required]
    public int PageSize { get; set; }

    [Required]
    public int TotalItems { get; set; }

    /// <summary>Zero items is zero pages, not one. An empty list has no page to ask for.</summary>
    [Required]
    public int TotalPages { get; set; }

    public static PagedResult<T> From(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
        => new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
}
