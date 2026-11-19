using ConstructoraEderTorres.Aplicacion.CasosDeUso.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.Herramientas;

public static class HerramientasPuntosAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoHerramientas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/herramientas").WithTags("Herramientas");
        grupo.MapPut("/{idHerramienta:int}", async (int idHerramienta, SolicitudActualizarHerramienta solicitud,
            ManejadorHerramientas manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ActualizarHerramientaAsync(idHerramienta, solicitud, cancelacion)).AResultadoHttp(contextoHttp));
        grupo.MapPost("/{idHerramienta:int}/revision", async (int idHerramienta, SolicitudRevisarHerramienta solicitud,
            ManejadorHerramientas manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.RevisarHerramientaAsync(idHerramienta, solicitud, cancelacion)).AResultadoHttp(contextoHttp));
        grupo.MapPatch("/eventos/{idIncidencia:long}/atencion", async (long idIncidencia, SolicitudAtenderIncidenciaHerramienta solicitud,
            ManejadorHerramientas manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.AtenderIncidenciaAsync(idIncidencia, solicitud, cancelacion)).AResultadoHttp(contextoHttp));
        grupo.MapGet("/eventos", async (int? idObra, long? idAsignacionHerramienta, int? idHerramienta, ManejadorHerramientas manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ListarIncidenciasAsync(idObra, idAsignacionHerramienta, cancelacion, idHerramienta)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/categorias/{idCategoria:int}", async (int idCategoria, ManejadorHerramientas manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerCategoriaPorIdAsync(idCategoria, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/tipos/{idTipo:int}", async (int idTipo, ManejadorHerramientas manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerTipoPorIdAsync(idTipo, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/{idHerramienta:int}", async (int idHerramienta, ManejadorHerramientas manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerHerramientaPorIdAsync(idHerramienta, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/asignaciones/{idAsignacion:long}", async (long idAsignacion, ManejadorHerramientas manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerAsignacionPorIdAsync(idAsignacion, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/eventos/{idIncidencia:long}", async (long idIncidencia, ManejadorHerramientas manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerIncidenciaPorIdAsync(idIncidencia, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/categorias", async (
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarCategoriasAsync(cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/categorias", async (
            SolicitudCrearCategoriaHerramienta solicitud,
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearCategoriaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/herramientas/categorias/{resultado.Valor!.IdCategoria}" : null);
        });

        grupo.MapGet("/tipos", async (
            int? idCategoria,
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarTiposAsync(idCategoria, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/tipos", async (
            SolicitudCrearTipoHerramienta solicitud,
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearTipoAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/herramientas/tipos/{resultado.Valor!.IdTipoHerramienta}" : null);
        });

        grupo.MapGet("/", async (
            int? idTipoHerramienta,
            string? estado,
            string? condicion,
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ConsultarHerramientasAsync(idTipoHerramienta, estado, condicion, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/", async (
            SolicitudRegistrarHerramienta solicitud,
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarHerramientaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/herramientas/{resultado.Valor!.IdHerramienta}" : null);
        });

        grupo.MapGet("/asignaciones", async (
            int? idObra,
            bool? soloActivas,
            string? estado,
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarAsignacionesAsync(idObra, soloActivas ?? false, cancelacion, estado);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/asignaciones", async (
            SolicitudAsignarHerramienta solicitud,
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.AsignarHerramientaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso && !resultado.Valor!.FueIdempotente
                    ? $"/api/herramientas/asignaciones/{resultado.Valor.IdAsignacionHerramienta}"
                    : null);
        });

        grupo.MapPost("/eventos", async (
            SolicitudRegistrarEventoHerramienta solicitud,
            ManejadorHerramientas manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.RegistrarDevolucionOIncidenciaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso && !resultado.Valor!.FueIdempotente
                    ? $"/api/herramientas/eventos/{resultado.Valor.IdIncidenciaHerramienta}"
                    : null);
        });

        return rutas;
    }
}
