using ConstructoraEderTorres.Aplicacion.CasosDeUso.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Presentacion.Configuracion;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.SeguimientoObra;

public static class SeguimientoObraPuntosAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoSeguimientoObra(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/seguimiento").WithTags("Seguimiento de Obra");
        grupo.MapGet("/avances/{idAvance:long}/evidencias", async (
            long idAvance, ManejadorSeguimientoObra manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ListarEvidenciasPorAvanceAsync(idAvance, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/actividades/{idActividad:int}", async (int idActividad, ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerActividadPorIdAsync(idActividad, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/avances/{idAvance:long}", async (long idAvance, ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerAvancePorIdAsync(idAvance, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/evidencias/{idEvidencia:long}", async (long idEvidencia, ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerEvidenciaPorIdAsync(idEvidencia, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/incidencias/{idIncidencia:long}", async (long idIncidencia, ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerIncidenciaPorIdAsync(idIncidencia, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/evidencias/{idEvidencia:long}/archivo", async (
            long idEvidencia, ManejadorSeguimientoObra manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
        {
            var resultado = await manejador.DescargarEvidenciaAsync(idEvidencia, cancelacion);
            return resultado.EsExitoso
                ? Results.Stream(resultado.Valor!.Contenido, "application/octet-stream", resultado.Valor.NombreArchivo)
                : resultado.AResultadoHttp(contextoHttp);
        }).Produces<Stream>(StatusCodes.Status200OK, contentType: "application/octet-stream");

        grupo.MapGet("/obras/{idObra:int}/actividades", async (
            int idObra,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarActividadesPorObraAsync(idObra, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/actividades", async (
            SolicitudCrearActividad solicitud,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearActividadAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/seguimiento/actividades/{resultado.Valor!.IdActividad}" : null);
        });

        grupo.MapPut("/actividades/{idActividad:int}", async (
            int idActividad,
            SolicitudActualizarActividad solicitud,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ActualizarActividadAsync(idActividad, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapGet("/obras/{idObra:int}/avances", async (
            int idObra,
            int? idActividad,
            DateOnly? fechaDesde,
            DateOnly? fechaHasta,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarAvancesPorObraAsync(
                idObra,
                idActividad,
                fechaDesde,
                fechaHasta,
                cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/avances", async (
            SolicitudRegistrarAvance solicitud,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarAvanceAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso && !resultado.Valor!.FueIdempotente && !resultado.Valor.FueConfirmacionBorrador
                    ? $"/api/seguimiento/avances/{resultado.Valor.IdAvance}"
                    : null);
        });

        grupo.MapPost("/avances/justificar-retraso", async (
            SolicitudJustificarActividad solicitud,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.JustificarActividadRetrasadaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/seguimiento/avances/{resultado.Valor!.IdAvance}" : null);
        });

        grupo.MapPost("/evidencias", async (
            SolicitudRegistrarEvidencia solicitud,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarEvidenciaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso && !resultado.Valor!.FueIdempotente
                    ? $"/api/seguimiento/evidencias/{resultado.Valor.IdEvidencia}"
                    : null);
        }).WithMetadata(new LimiteOperacionApi(PerfilLimiteApi.Evidencia));

        grupo.MapGet("/obras/{idObra:int}/incidencias", async (
            int idObra,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarIncidenciasPorObraAsync(idObra, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/incidencias", async (
            SolicitudRegistrarIncidenciaObra solicitud,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarIncidenciaObraAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso && !resultado.Valor!.FueIdempotente
                    ? $"/api/seguimiento/incidencias/{resultado.Valor.IdIncidencia}"
                    : null);
        });

        grupo.MapGet("/obras/{idObra:int}/actas", async (
            int idObra,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarActasPorObraAsync(idObra, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapGet("/actas/{idActa:int}", async (
            int idActa,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ObtenerActaPorIdAsync(idActa, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/actas", async (
            SolicitudElaborarActa solicitud,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ElaborarActaSemanalAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/seguimiento/actas/{resultado.Valor!.IdActa}" : null);
        });

        grupo.MapPut("/actas/{idActa:int}", async (int idActa, SolicitudActualizarActa solicitud,
            ManejadorSeguimientoObra manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ActualizarActaSemanalAsync(idActa, solicitud, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapPost("/actas/{idActa:int}/enviar-revision", async (int idActa, SolicitudEnviarActa solicitud,
            ManejadorSeguimientoObra manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.EnviarActaARevisionAsync(idActa, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapPatch("/actas/{idActa:int}/resolucion", async (
            int idActa,
            SolicitudResolverActa solicitud,
            ManejadorSeguimientoObra manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ResolverActaSemanalAsync(idActa, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        return rutas;
    }
}
