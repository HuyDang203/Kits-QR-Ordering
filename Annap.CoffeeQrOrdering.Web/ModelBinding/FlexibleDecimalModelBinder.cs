using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Annap.CoffeeQrOrdering.Web.ModelBinding;

/// <summary>
/// Binds decimals from form posts for vi-VN / en-US / invariant (HTML number inputs often use '.').
/// </summary>
public sealed class FlexibleDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;
        var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        var raw = valueProviderResult.FirstValue?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            bindingContext.ModelState.TryAddModelError(modelName, "Price is required.");
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        raw = raw.Replace('\u00A0', ' ').Trim();

        if (!TryParsePrice(raw, bindingContext.HttpContext?.Features.Get<IRequestCultureFeature>()?.RequestCulture?.Culture,
                out var parsed))
        {
            bindingContext.ModelState.TryAddModelError(modelName, "Enter a valid price.");
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(parsed);
        return Task.CompletedTask;
    }

    private static bool TryParsePrice(string raw, CultureInfo? requestCulture, out decimal value)
    {
        value = 0;
        var styles = NumberStyles.Number | NumberStyles.AllowLeadingSign;

        if (requestCulture is not null && decimal.TryParse(raw, styles, requestCulture, out value))
            return true;

        if (decimal.TryParse(raw, styles, CultureInfo.InvariantCulture, out value))
            return true;

        if (decimal.TryParse(raw, styles, CultureInfo.GetCultureInfo("en-US"), out value))
            return true;

        if (decimal.TryParse(raw, styles, CultureInfo.GetCultureInfo("vi-VN"), out value))
            return true;

        // vi-VN typing "65000,5" in a plain text field, or mixed
        var normalized = raw.Replace(" ", "");
        if (normalized.Contains(',') && !normalized.Contains('.'))
        {
            var withDot = normalized.Replace(',', '.');
            if (decimal.TryParse(withDot, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;
        }

        return false;
    }
}
