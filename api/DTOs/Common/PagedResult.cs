using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

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
