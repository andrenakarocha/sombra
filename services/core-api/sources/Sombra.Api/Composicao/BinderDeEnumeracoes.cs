using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Sombra.Api.Composicao;

/// <summary>
/// Aceita os enums da query string no formato do contrato (<c>CAUTELA_EXTREMA</c>), o mesmo do JSON, além do nome
/// do C# (<c>CautelaExtrema</c>).
/// </summary>
public sealed class BinderDeEnumeracoes : IModelBinder
{
    /// <inheritdoc/>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);
        ValueProviderResult valor = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valor == ValueProviderResult.None || string.IsNullOrWhiteSpace(valor.FirstValue))
        {
            return Task.CompletedTask;
        }

        Type tipo = bindingContext.ModelMetadata.UnderlyingOrModelType;
        string texto = valor.FirstValue.Replace("_", string.Empty, StringComparison.Ordinal);
        if (!int.TryParse(texto, out _) && Enum.TryParse(tipo, texto, ignoreCase: true, out object? lido) && Enum.IsDefined(tipo, lido))
        {
            bindingContext.Result = ModelBindingResult.Success(lido);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Valor desconhecido.");
        }

        return Task.CompletedTask;
    }
}

/// <summary>Usa o <see cref="BinderDeEnumeracoes"/> para todo enum (e enum anulável).</summary>
public sealed class ProvedorDoBinderDeEnumeracoes : IModelBinderProvider
{
    /// <inheritdoc/>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Metadata.UnderlyingOrModelType.IsEnum ? new BinderDeEnumeracoes() : null;
    }
}
