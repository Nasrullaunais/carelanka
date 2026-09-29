using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Common;

public class ListMyNotificationsQueryParameters
{
    [FromQuery(Name = "unreadOnly")]
    public bool UnreadOnly { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}
