using ConstructoraEderTorres.Aplicacion.CasosDeUso.Sincronizacion;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Presentacion.Intermediarios;
using ConstructoraEderTorres.Presentacion.Configuracion;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.Sincronizacion;

public static class SincronizacionPuntosAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoSincronizacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/sincronizacion").WithTags("Sincronización Offline")
            .WithMetadata(new OmitirTransaccion(), new LimiteOperacionApi(PerfilLimiteApi.Sincronizacion));

        grupo.MapPost("/lote", async (
            SolicitudSincronizacionLote solicitud,
            ManejadorSincronizacion manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ProcesarLoteOfflineAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        return rutas;
    }
}
