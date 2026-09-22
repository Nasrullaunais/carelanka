using System.Text.Json;
using CareLanka.Api.DTOs.Emergency;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace CareLanka.Api.Common.OpenApi;

public sealed class EmergencyQueryBindingMetadataProvider : IBindingMetadataProvider
{
    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        var containerType = context.Key.ContainerType;
        if ((containerType != typeof(EmergencyCallListRequest)
                && containerType != typeof(MyEmergencyCallListRequest)
                && containerType != typeof(MyDispatchHistoryRequest))
            || context.Key.Name is null)
        {
            return;
        }

        context.BindingMetadata.BinderModelName = JsonNamingPolicy.CamelCase.ConvertName(context.Key.Name);
    }
}
