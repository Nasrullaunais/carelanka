using System.Text.Json;
using CareLanka.Api.DTOs.Emergency;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace CareLanka.Api.Common.OpenApi;

public sealed class EmergencyQueryBindingMetadataProvider : IBindingMetadataProvider
{
    private static readonly HashSet<Type?> QueryModels =
    [
        typeof(EmergencyCallListRequest),
        typeof(MyEmergencyCallListRequest),
        typeof(MyDispatchHistoryRequest),
        typeof(CancellationRequestListRequest),
        typeof(ListDispatchProposalsRequest),
        typeof(AddressSearchRequest)
    ];

    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        var containerType = context.Key.ContainerType;
        if (!QueryModels.Contains(containerType) || context.Key.Name is null)
        {
            return;
        }

        context.BindingMetadata.BinderModelName = JsonNamingPolicy.CamelCase.ConvertName(context.Key.Name);
    }
}
