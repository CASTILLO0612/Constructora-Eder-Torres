using ConstructoraEderTorres.Aplicacion.CasosDeUso.Asistencia;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Presentacion.Intermediarios;
using ConstructoraEderTorres.Presentacion.Configuracion;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.Asistencia;

public static class AsistenciaPuntosAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoAsistencia(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/asistencias").WithTags("Asistencia")
            .WithMetadata(new AislamientoTransaccion(AislamientoOperacion.LecturaConfirmada),
                new LimiteOperacionApi(PerfilLimiteApi.Asistencia));
        grupo.MapGet("/mis-asignaciones", async (ManejadorAsistencia manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ListarAsignacionesPropiasAsync(cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapPost("/validar-ubicacion", async (SolicitudValidarUbicacionAsistencia solicitud,
            ManejadorAsistencia manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ValidarUbicacionAsync(solicitud, cancelacion)).AResultadoHttp(contextoHttp))
            .WithMetadata(new LimiteOperacionApi(PerfilLimiteApi.Ubicacion), new OmitirTransaccion());
        grupo.MapGet("/correcciones", async (int? idObra, string? estado, ManejadorAsistencia manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ListarCorreccionesAsync(idObra, estado, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/{idAsistencia:long}", async (long idAsistencia, ManejadorAsistencia manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerAsistenciaPorIdAsync(idAsistencia, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/correcciones/{idCorreccion:long}", async (long idCorreccion, ManejadorAsistencia manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerCorreccionPorIdAsync(idCorreccion, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/", async (int? idObra, int? idTrabajador, int? idCuadrilla,
            DateOnly? fechaDesde, DateOnly? fechaHasta, string? estado, ManejadorAsistencia manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarRegistrosAsync(new FiltroConsolidadoAsistencia(
                idObra, idTrabajador, idCuadrilla, fechaDesde, fechaHasta, estado), cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/", async (
            SolicitudRegistrarAsistencia solicitud,
            ManejadorAsistencia manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarAsistenciaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso && !resultado.Valor!.FueIdempotente
                    ? $"/api/asistencias/{resultado.Valor.IdAsistencia}"
                    : null);
        });

        grupo.MapPost("/cuadrilla", async (
            SolicitudRegistrarAsistenciaCuadrilla solicitud,
            ManejadorAsistencia manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarAsistenciaCuadrillaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/cuadrilla/salidas", async (SolicitudRegistrarSalidasCuadrilla solicitud,
            ManejadorAsistencia manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.RegistrarSalidasCuadrillaAsync(solicitud, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapPatch("/{idAsistencia:long}/salida", async (
            long idAsistencia,
            SolicitudRegistrarSalidaAsistencia solicitud,
            ManejadorAsistencia manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarSalidaAsync(idAsistencia, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/correcciones", async (
            SolicitudCrearCorreccionAsistencia solicitud,
            ManejadorAsistencia manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.SolicitarCorreccionAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/asistencias/correcciones/{resultado.Valor!.IdCorreccion}" : null);
        });

        grupo.MapPatch("/correcciones/{idCorreccion:long}/resolucion", async (
            long idCorreccion,
            SolicitudResolverCorreccionAsistencia solicitud,
            ManejadorAsistencia manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ResolverCorreccionAsync(idCorreccion, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapGet("/consolidado", async (
            int? idObra,
            int? idTrabajador,
            int? idCuadrilla,
            DateOnly? fechaDesde,
            DateOnly? fechaHasta,
            ManejadorAsistencia manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ConsultarConsolidadoAsync(
                new FiltroConsolidadoAsistencia(idObra, idTrabajador, idCuadrilla, fechaDesde, fechaHasta),
                cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        return rutas;
    }
}
