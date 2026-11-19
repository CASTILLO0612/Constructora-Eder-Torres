using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Presentacion.Configuracion;

namespace ConstructoraEderTorres.Presentacion.Intermediarios;

public sealed class FiltroContratoSolicitud(OpcionesApi opciones) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        foreach (var argumento in contexto.Arguments)
        {
            if (argumento?.GetType().Namespace?.StartsWith("ConstructoraEderTorres.Aplicacion.Contratos", StringComparison.Ordinal) != true)
                continue;
            var errores = ValidadorContratoSolicitud.Validar(argumento, opciones.MaximoElementosSolicitud,
                validarAnidados: argumento is not SolicitudSincronizacionLote);
            if (errores.Count > 0)
                return ValueTask.FromResult<object?>(Results.ValidationProblem(errores.ToDictionary()));
        }
        return siguiente(contexto);
    }
}
