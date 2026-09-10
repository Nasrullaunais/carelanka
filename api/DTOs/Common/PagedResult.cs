using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

/// <summary>One page of a list endpoint. Group-owned: the shape is the same in all five specs.</summary>
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

    /// <summary>Always at least 1, so an empty list does not render as "page 1 of 0".</summary>
    [Required]
    public int TotalPages { get; set; }

    public static PagedResult<T> From(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
        => new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
}
