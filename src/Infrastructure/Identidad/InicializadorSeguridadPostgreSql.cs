using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Identidad;

public sealed class InicializadorSeguridadPostgreSql(
    ContextoConstructora contexto,
    IPuertoSeguridad seguridad,
    IServicioHashContrasena contrasenas,
    IUnidadTrabajo unidadTrabajo,
    IRelojSistema reloj,
    ICalendarioLaboral calendario) : IInicializadorSeguridad
{
    public Task<Resultado<RespuestaUsuario>> CrearAdministradorInicialAsync(
        string nombreUsuario, string contrasena, CancellationToken cancelacion = default)
    {
        var nombre = nombreUsuario?.Trim() ?? string.Empty;
        if (nombre.Length is < 1 or > 60 || string.IsNullOrWhiteSpace(contrasena) || contrasena.Length is < 8 or > 256)
            return Task.FromResult(Resultado<RespuestaUsuario>.Fallo(ErrorNegocio.Validacion(
                "administrador_invalido", "Indique un usuario de hasta 60 caracteres y una contraseña de 8 a 256 caracteres.")));

        return unidadTrabajo.EjecutarAsync(async token =>
        {
            // Serializa la inicialización entre procesos sin introducir IDs numéricos de roles.
            await contexto.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext('constructora:inicializacion-seguridad'))", token);
            if (await contexto.Usuarios.AnyAsync(token))
                return Resultado<RespuestaUsuario>.Fallo(ErrorNegocio.Conflicto(
                    "seguridad_ya_inicializada", "Ya existen usuarios. Use la administración autenticada de cuentas."));

            await seguridad.InicializarCatalogoSeguridadAsync(token);
            var rol = await contexto.Roles.SingleAsync(r => r.Nombre == NombresRol.GerenteGeneral, token);
            var usuario = await seguridad.CrearUsuarioAsync(nombre, contrasenas.GenerarHash(contrasena), null,
                reloj.AhoraUtc, calendario.Hoy, [rol.IdRol],
                new RegistroEventoAuditoria(null, "usuarios", nombre, AccionesAuditoria.Crear,
                    "estado", null, EstadosUsuario.Activo), token);
            return Resultado<RespuestaUsuario>.Exito(usuario);
        }, resultado => resultado.EsExitoso, cancelacion);
    }
}
