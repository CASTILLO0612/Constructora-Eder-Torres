using ConstructoraEderTorres.Aplicacion.CasosDeUso.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Presentacion.Configuracion;
using ConstructoraEderTorres.Presentacion.Identidad;
using ConstructoraEderTorres.Presentacion.Intermediarios;
using Microsoft.AspNetCore.Antiforgery;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.Seguridad;

public static class SeguridadPuntosAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoSeguridad(this IEndpointRouteBuilder rutas)
    {
        var sesion = rutas.MapGroup("/api/sesion").WithTags("Seguridad - Sesión")
            .WithMetadata(new OmitirTransaccion());

        sesion.MapGet("/antifalsificacion", (IAntiforgery proteccion, HttpContext contexto) =>
        {
            var tokens = proteccion.GetAndStoreTokens(contexto);
            return TypedResults.Ok(new RespuestaAntifalsificacion(tokens.RequestToken!, InyeccionDependencias.CabeceraAntifalsificacion));
        }).AllowAnonymous();

        sesion.MapPost("/iniciar", async (
            SolicitudIniciarSesion solicitud,
            ManejadorSeguridad manejador,
            OpcionesSesion opcionesSesion,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.AutenticarCredencialesAsync(solicitud, cancelacion);
            if (!resultado.EsExitoso)
            {
                return resultado.AResultadoHttp(contextoHttp);
            }

            await ContextoUsuarioHttp.EmitirCookieSesionAsync(contextoHttp, resultado.Valor!, opcionesSesion);
            return Results.Ok(resultado.Valor!.Perfil);
        }).AllowAnonymous().RequireRateLimiting(InyeccionDependencias.PoliticaInicioSesion)
            .Produces<RespuestaSesionActiva>();

        sesion.MapPost("/cerrar", async (HttpContext contextoHttp) =>
        {
            await ContextoUsuarioHttp.CerrarSesionPersistidaAsync(contextoHttp);
            return TypedResults.NoContent();
        });

        sesion.MapGet("/actual", (
            ManejadorSeguridad manejador,
            HttpContext contextoHttp) =>
        {
            var sesionActual = manejador.ObtenerPerfilSesionActual();
            return sesionActual.AResultadoHttp(contextoHttp);
        });

        var usuarios = rutas.MapGroup("/api/usuarios").WithTags("Seguridad - Usuarios");

        usuarios.MapGet("/", async (
            string? estado,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarUsuariosAsync(estado, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        usuarios.MapGet("/{idUsuario:int}", async (
            int idUsuario,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ObtenerUsuarioPorIdAsync(idUsuario, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        usuarios.MapPost("/", async (
            SolicitudCrearUsuario solicitud,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearUsuarioAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/usuarios/{resultado.Valor!.IdUsuario}" : null);
        });

        usuarios.MapPut("/{idUsuario:int}", async (
            int idUsuario,
            SolicitudActualizarUsuario solicitud,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ActualizarUsuarioAsync(idUsuario, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        usuarios.MapPatch("/{idUsuario:int}/estado", async (
            int idUsuario,
            SolicitudCambiarEstadoUsuario solicitud,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CambiarEstadoUsuarioAsync(idUsuario, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        usuarios.MapPost("/{idUsuario:int}/roles", async (
            int idUsuario,
            SolicitudAsignarRolUsuario solicitud,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.AsignarRolAUsuarioAsync(idUsuario, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        var roles = rutas.MapGroup("/api/roles").WithTags("Seguridad - Roles y Permisos");
        roles.MapGet("/{idRol:int}", async (int idRol, ManejadorSeguridad manejador, HttpContext contextoHttp, CancellationToken cancelacion) =>
            (await manejador.ObtenerRolPorIdAsync(idRol, cancelacion)).AResultadoHttp(contextoHttp));


        roles.MapGet("/", async (
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarRolesAsync(cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        roles.MapPost("/", async (
            SolicitudCrearRol solicitud,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.CrearRolAsync(solicitud, cancelacion);
            return resultado.AResultadoHttp(
                contextoHttp,
                resultado.EsExitoso ? $"/api/roles/{resultado.Valor!.IdRol}" : null);
        });

        roles.MapPut("/{idRol:int}/permisos", async (
            int idRol,
            SolicitudAsignarPermisosRol solicitud,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.AsignarPermisosARolAsync(idRol, solicitud, cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        });

        rutas.MapGet("/api/permisos", async (
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ListarPermisosAsync(cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        }).WithTags("Seguridad - Roles y Permisos");

        rutas.MapGet("/api/auditorias", async (
            string? entidad,
            string? idRegistro,
            string? accion,
            int? idUsuario,
            DateTime? desdeUtc,
            DateTime? hastaUtc,
            int? limite,
            ManejadorSeguridad manejador,
            HttpContext contextoHttp,
            CancellationToken cancelacion) =>
        {
            var resultado = await manejador.ConsultarAuditoriasAsync(
                new FiltroAuditoria(Entidad: entidad, IdRegistro: idRegistro, Accion: accion, IdUsuario: idUsuario,
                    DesdeUtc: desdeUtc, HastaUtc: hastaUtc, Limite: limite ?? 100),
                cancelacion);
            return resultado.AResultadoHttp(contextoHttp);
        }).WithTags("Seguridad - Auditoría");

        return rutas;
    }
}
