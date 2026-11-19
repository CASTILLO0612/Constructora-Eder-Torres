using ConstructoraEderTorres.Aplicacion.Comun;

namespace ConstructoraEderTorres.Presentacion.Intermediarios;

public sealed record OmitirTransaccion;
public sealed record AislamientoTransaccion(AislamientoOperacion Valor);

public sealed class FiltroTransaccionApi(IUnidadTrabajo unidadTrabajo) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var solicitud = contexto.HttpContext.Request;
        if (HttpMethods.IsGet(solicitud.Method) || HttpMethods.IsHead(solicitud.Method)
            || contexto.HttpContext.GetEndpoint()?.Metadata.GetMetadata<OmitirTransaccion>() is not null)
        {
            return await siguiente(contexto);
        }

        return await unidadTrabajo.EjecutarAsync(
            async _ => await siguiente(contexto),
            DebeConfirmar,
            contexto.HttpContext.RequestAborted,
            contexto.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AislamientoTransaccion>()?.Valor
                ?? AislamientoOperacion.Serializable);
    }

    private static bool DebeConfirmar(object? resultado)
    {
        // Results<T...> expone el resultado concreto mediante INestedHttpResult.
        // El estado de su envoltorio no debe ocultar un ProblemHttpResult.
        while (resultado is INestedHttpResult anidado) resultado = anidado.Result;
        return resultado is not IStatusCodeHttpResult { StatusCode: >= 400 };
    }
}
