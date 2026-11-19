using ConstructoraEderTorres.Aplicacion.CasosDeUso.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.EPP;

public static class EppPuntosAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoEpp(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/epp").WithTags("Equipos de Protección Personal (EPP)");
        grupo.MapPost("/existencias/{idEpp:int}/revision", async (int idEpp, SolicitudRevisarEpp solicitud,
            ManejadorEpp manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.RevisarEppAsync(idEpp, solicitud, cancelacion)).AResultadoHttp(contextoHttp));
        grupo.MapGet("/entregas", async (int? idObra, int? idTrabajador, string? estado, ManejadorEpp manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ListarEntregasAsync(idObra, idTrabajador, estado, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/reposiciones", async (int? idObra, string? estado, ManejadorEpp manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ListarReposicionesAsync(idObra, estado, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/tipos/{idTipoEpp:int}", async (int idTipoEpp, ManejadorEpp manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerTipoEppPorIdAsync(idTipoEpp, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/existencias/{idEpp:int}", async (int idEpp, ManejadorEpp manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerEppPorIdAsync(idEpp, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/entregas/{idEntrega:long}", async (long idEntrega, ManejadorEpp manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerEntregaPorIdAsync(idEntrega, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/reposiciones/{idReposicion:long}", async (long idReposicion, ManejadorEpp manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerReposicionPorIdAsync(idReposicion, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapPost("/reposiciones/{idReposicion:long}/ejecutar", async (long idReposicion,
            SolicitudEjecutarReposicionEpp solicitud, ManejadorEpp manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
        {
            var resultado = await manejador.EjecutarReposicionAsync(idReposicion, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapGet("/tipos", async (
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarTiposEppAsync(cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/tipos", async (
            SolicitudCrearTipoEpp solicitud,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearTipoEppAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/epp/tipos/{resultado.Valor!.IdTipoEpp}" : null);
        });

        grupo.MapGet("/existencias", async (
            int? idTipoEpp,
            string? estado,
            string? condicion,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ConsultarExistenciasAsync(idTipoEpp, estado, condicion, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapGet("/existencias/resumen", async (
            int? idTipoEpp,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ConsultarResumenExistenciasAsync(idTipoEpp, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/entradas", async (
            SolicitudRegistrarEntradaEpp solicitud,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarEntradaAutorizadaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/epp/existencias/{resultado.Valor!.IdEpp}" : null);
        });

        grupo.MapPost("/entregas", async (
            SolicitudRegistrarEntregaEpp solicitud,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarEntregaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso && !resultado.Valor!.FueIdempotente
                    ? $"/api/epp/entregas/{resultado.Valor.IdEntregaEpp}"
                    : null);
        });

        grupo.MapPatch("/entregas/{idEntregaEpp:long}/devolucion", async (
            long idEntregaEpp,
            SolicitudDevolverEpp solicitud,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.DevolverEppAsync(idEntregaEpp, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/reposiciones", async (
            SolicitudRegistrarReposicionEpp solicitud,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarReposicionAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso && !resultado.Valor!.FueIdempotente
                    ? $"/api/epp/reposiciones/{resultado.Valor.IdReposicionEpp}"
                    : null);
        });

        grupo.MapGet("/trabajadores/{idTrabajador:int}/historial", async (
            int idTrabajador,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ConsultarHistorialTrabajadorAsync(idTrabajador, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapGet("/reportes", async (
            int? idObra,
            int? idTrabajador,
            int? idTipoEpp,
            DateOnly? fechaDesde,
            DateOnly? fechaHasta,
            ManejadorEpp manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.GenerarReporteAsync(
                new FiltroReporteEpp(idObra, idTrabajador, idTipoEpp, fechaDesde, fechaHasta),
                cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        return rutas;
    }
}
