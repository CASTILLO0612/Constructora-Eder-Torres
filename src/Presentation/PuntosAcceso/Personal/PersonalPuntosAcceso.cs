using ConstructoraEderTorres.Aplicacion.CasosDeUso.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.Personal;

public static class PersonalPuntosAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoPersonal(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/personal").WithTags("Personal y Cuadrillas");
        grupo.MapGet("/trabajadores-cuadrilla/{idTrabajadorCuadrilla:int}", async (
            int idTrabajadorCuadrilla, ManejadorPersonal manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerMiembroCuadrillaPorIdAsync(idTrabajadorCuadrilla, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/obras/{idObra:int}/cuadrillas", async (
            int idObra, ManejadorPersonal manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ListarCuadrillasDeObraAsync(idObra, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapPatch("/asignaciones-obra/{idAsignacion:int}/finalizacion", async (
            int idAsignacion, SolicitudFinalizarAsignacionObra solicitud, ManejadorPersonal manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.FinalizarAsignacionObraAsync(idAsignacion, solicitud, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/trabajadores/{idTrabajador:int}", async (int idTrabajador, ManejadorPersonal manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerTrabajadorPorIdAsync(idTrabajador, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/cargos/{idCargo:int}", async (int idCargo, ManejadorPersonal manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerCargoPorIdAsync(idCargo, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/especialidades/{idEspecialidad:int}", async (int idEspecialidad, ManejadorPersonal manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerEspecialidadPorIdAsync(idEspecialidad, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/cuadrillas/{idCuadrilla:int}", async (int idCuadrilla, ManejadorPersonal manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerCuadrillaPorIdAsync(idCuadrilla, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/asignaciones-obra/{idAsignacion:int}", async (int idAsignacion, ManejadorPersonal manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerAsignacionPorIdAsync(idAsignacion, cancelacion)).AResultadoHttp(contextoHttp));

        grupo.MapGet("/cuadrillas-obra/{idCuadrillaObra:int}", async (int idCuadrillaObra, ManejadorPersonal manejador,
            HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerCuadrillaObraPorIdAsync(idCuadrillaObra, cancelacion)).AResultadoHttp(contextoHttp));


        grupo.MapGet("/cargos", async (
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarCargosAsync(cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/cargos", async (
            SolicitudCargo solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearCargoAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/personal/cargos/{resultado.Valor!.IdCargo}" : null);
        });

        grupo.MapGet("/especialidades", async (
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarEspecialidadesAsync(cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/especialidades", async (
            SolicitudEspecialidad solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearEspecialidadAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/personal/especialidades/{resultado.Valor!.IdEspecialidad}" : null);
        });

        grupo.MapGet("/cuadrillas", async (
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarCuadrillasAsync(cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/cuadrillas", async (
            SolicitudCuadrilla solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearCuadrillaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/personal/cuadrillas/{resultado.Valor!.IdCuadrilla}" : null);
        });

        grupo.MapGet("/trabajadores", async (
            string? estado,
            bool? soloAsignables,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarTrabajadoresAsync(
                estado,
                soloAsignables ?? false,
                cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/trabajadores", async (
            SolicitudCrearTrabajador solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearTrabajadorAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/personal/trabajadores/{resultado.Valor!.IdTrabajador}" : null);
        });

        grupo.MapPut("/trabajadores/{idTrabajador:int}", async (
            int idTrabajador,
            SolicitudActualizarTrabajador solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ActualizarTrabajadorAsync(idTrabajador, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPatch("/trabajadores/{idTrabajador:int}/estado", async (
            int idTrabajador,
            SolicitudCambiarEstadoTrabajador solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CambiarEstadoTrabajadorAsync(idTrabajador, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/asignaciones-obra", async (
            SolicitudAsignarTrabajadorObra solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.AsignarTrabajadorAObraAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/personal/asignaciones-obra/{resultado.Valor!.IdAsignacion}" : null);
        });

        grupo.MapGet("/obras/{idObra:int}/asignaciones", async (
            int idObra,
            bool? soloVigentes,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarAsignacionesDeObraAsync(
                idObra,
                soloVigentes ?? true,
                cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        grupo.MapPost("/cuadrillas-obra", async (
            SolicitudAsignarCuadrillaObra solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.AsignarCuadrillaAObraAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/personal/cuadrillas-obra/{resultado.Valor!.IdCuadrillaObra}" : null);
        });

        grupo.MapPost("/trabajadores-cuadrilla", async (
            SolicitudAsignarTrabajadorCuadrilla solicitud,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.AsignarTrabajadorACuadrillaAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/personal/trabajadores-cuadrilla/{resultado.Valor!.IdTrabajadorCuadrilla}" : null);
        });

        grupo.MapGet("/obras/{idObra:int}/miembros-cuadrilla", async (
            int idObra,
            int? idCuadrillaObra,
            ManejadorPersonal manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarMiembrosCuadrillaObraAsync(idObra, idCuadrillaObra, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        return rutas;
    }
}
