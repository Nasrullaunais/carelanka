using CareLanka.Api.Common.Persistence;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CareLanka.Api.Common.ModelBinding;

public sealed class SnakeCaseEnumModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;
        var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;
        if (string.IsNullOrEmpty(value))
        {
            return Task.CompletedTask;
        }

        var enumType = Nullable.GetUnderlyingType(bindingContext.ModelType) ?? bindingContext.ModelType;

        try
        {
            bindingContext.Result = ModelBindingResult.Success(EnumWire.FromWire(enumType, value));
        }
        catch (ArgumentOutOfRangeException)
        {
            bindingContext.ModelState.TryAddModelError(modelName, $"The value '{value}' is not valid.");
        }

        return Task.CompletedTask;
    }
}

public sealed class SnakeCaseEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;

        return modelType.IsEnum ? new SnakeCaseEnumModelBinder() : null;
    }
}
