using ConstructoraEderTorres.Aplicacion.CasosDeUso.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.Obras;

public static class ObrasPuntosAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoObras(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/obras").WithTags("Obras");

        grupo.MapGet("/", async (
            string? estado,
            ManejadorObras manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarObrasAsync(estado, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapGet("/{idObra:int}", async (
            int idObra,
            ManejadorObras manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ObtenerObraPorIdAsync(idObra, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/", async (
            SolicitudCrearObra solicitud,
            ManejadorObras manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearObraAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/obras/{resultado.Valor!.IdObra}" : null);
        });

        grupo.MapPut("/{idObra:int}", async (
            int idObra,
            SolicitudActualizarObra solicitud,
            ManejadorObras manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ActualizarObraAsync(idObra, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPatch("/{idObra:int}/estado", async (
            int idObra,
            SolicitudCambiarEstadoObra solicitud,
            ManejadorObras manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CambiarEstadoObraAsync(idObra, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        return rutas;
    }
}
