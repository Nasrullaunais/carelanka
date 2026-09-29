using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CareLanka.Api.Hubs.Common;

[Authorize(Policy = Policies.AnyPrincipal)]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var principalType = EnumWire.FromWire<PrincipalType>(RequireClaim(CareLankaClaims.PrincipalType));
        var id = Guid.Parse(RequireClaim(CareLankaClaims.Subject));

        await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.For(principalType, id));
        await base.OnConnectedAsync();
    }

    private string RequireClaim(string type) =>
        Context.User?.FindFirst(type)?.Value
            ?? throw new HubException("The connection's token is missing a required claim.");
}
