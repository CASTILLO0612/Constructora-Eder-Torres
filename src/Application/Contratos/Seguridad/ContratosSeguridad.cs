using System.Net;
using ConstructoraEderTorres.Aplicacion.Comun;

namespace ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;

public interface IServicioHashContrasena
{
    string GenerarHash(string contrasenaPlana);

    bool VerificarHash(string contrasenaPlana, string hashAlmacenado);
}

public sealed record RegistroEventoAuditoria(
    int? IdUsuario,
    string Entidad,
    string IdRegistro,
    string Accion,
    string? Campo = null,
    string? ValorAnterior = null,
    string? ValorNuevo = null,
    IPAddress? IpOrigen = null,
    Guid? UuidOperacion = null);

public interface IServicioAuditoria
{
    Task RegistrarEventoAsync(
        RegistroEventoAuditoria evento,
        CancellationToken cancelacion = default);
}

public sealed record SolicitudCrearUsuario(
    string NombreUsuario,
    string Contrasena,
    int? IdTrabajador,
    IReadOnlyList<int>? IdsRolesIniciales = null);

public sealed record SolicitudActualizarUsuario(
    string NombreUsuario,
    int? IdTrabajador,
    string? NuevaContrasena = null);

public sealed record SolicitudCambiarEstadoUsuario(
    string Estado);

public sealed record RolAsignadoRespuesta(
    int IdUsuarioRol,
    int IdRol,
    string NombreRol,
    DateOnly FechaInicio,
    DateOnly? FechaFin);

public sealed record RespuestaUsuario(
    int IdUsuario,
    int? IdTrabajador,
    string NombreUsuario,
    string Estado,
    DateTime CreadoEn,
    DateTime? UltimoAcceso,
    IReadOnlyList<RolAsignadoRespuesta> Roles);

public sealed record SolicitudCrearRol(
    string Nombre,
    string? Descripcion,
    IReadOnlyList<string>? CodigosPermiso = null);

public sealed record SolicitudAsignarPermisosRol(
    IReadOnlyList<string> CodigosPermiso);

public sealed record SolicitudAsignarRolUsuario(
    int IdRol,
    DateOnly FechaInicio,
    DateOnly? FechaFin = null);

public sealed record RespuestaPermiso(
    int IdPermiso,
    string Codigo,
    string Nombre,
    string? Descripcion,
    bool Activo);

public sealed record RespuestaRol(
    int IdRol,
    string Nombre,
    string? Descripcion,
    bool Activo,
    IReadOnlyList<string> CodigosPermiso);

public sealed record SolicitudIniciarSesion(
    string NombreUsuario,
    string Contrasena);

public sealed record RespuestaSesionActiva(
    int IdUsuario,
    string NombreUsuario,
    int? IdTrabajador,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permisos,
    IReadOnlyList<int> ObrasAsignadas,
    bool TieneAlcanceGlobal,
    DateTime AutenticadoEnUtc);

public sealed record ResultadoAutenticacion(RespuestaSesionActiva Perfil, string SelloCredenciales);

public sealed record RespuestaAntifalsificacion(string Token, string Cabecera);

public interface IInicializadorSeguridad
{
    Task<Resultado<RespuestaUsuario>> CrearAdministradorInicialAsync(
        string nombreUsuario, string contrasena, CancellationToken cancelacion = default);
}

public sealed record FiltroAuditoria(
    string? Entidad = null,
    string? IdRegistro = null,
    int? IdUsuario = null,
    string? Accion = null,
    DateTime? DesdeUtc = null,
    DateTime? HastaUtc = null,
    int Limite = 100);

public sealed record RespuestaAuditoria(
    long IdAuditoria,
    int? IdUsuario,
    string? NombreUsuario,
    string Entidad,
    string IdRegistro,
    string Accion,
    string? Campo,
    string? ValorAnterior,
    string? ValorNuevo,
    DateTime FechaHora,
    string? IpOrigen,
    Guid UuidOperacion);

public interface IPuertoSeguridad
{
    Task InicializarCatalogoSeguridadAsync(CancellationToken cancelacion = default);

    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, int? excluirIdUsuario = null, CancellationToken cancelacion = default);

    Task<bool> TrabajadorTieneUsuarioAsync(int idTrabajador, int? excluirIdUsuario = null, CancellationToken cancelacion = default);

    Task<bool> ExisteTrabajadorActivoAsync(int idTrabajador, CancellationToken cancelacion = default);

    Task<RespuestaUsuario> CrearUsuarioAsync(
        string nombreUsuario,
        string contrasenaHash,
        int? idTrabajador,
        DateTime creadoEnUtc,
        DateOnly fechaRoles,
        IReadOnlyList<int> idsRoles,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaUsuario?> ObtenerUsuarioPorIdAsync(int idUsuario, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaUsuario>> ListarUsuariosAsync(string? estado = null, CancellationToken cancelacion = default);

    Task<RespuestaUsuario?> ActualizarUsuarioAsync(
        int idUsuario,
        string nombreUsuario,
        int? idTrabajador,
        string? nuevaContrasenaHash,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaUsuario?> CambiarEstadoUsuarioAsync(
        int idUsuario,
        string nuevoEstado,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaPermiso>> ListarPermisosAsync(CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaRol>> ListarRolesAsync(CancellationToken cancelacion = default);

    Task<RespuestaRol?> ObtenerRolPorIdAsync(int idRol, CancellationToken cancelacion = default);

    Task<bool> ExisteRolPorNombreAsync(string nombre, int? excluirIdRol = null, CancellationToken cancelacion = default);

    Task<RespuestaRol> CrearRolAsync(
        string nombre,
        string? descripcion,
        IReadOnlyList<string> codigosPermiso,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaRol?> ActualizarPermisosDeRolAsync(
        int idRol,
        IReadOnlyList<string> codigosPermiso,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RolAsignadoRespuesta?> AsignarRolAUsuarioAsync(
        int idUsuario,
        int idRol,
        DateOnly fechaInicio,
        DateOnly? fechaFin,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<CredencialesUsuarioAlmacenadas?> ObtenerCredencialesPorNombreUsuarioAsync(
        string nombreUsuario,
        CancellationToken cancelacion = default);

    Task RegistrarUltimoAccesoAsync(
        int idUsuario,
        DateTime instanteUtc,
        CancellationToken cancelacion = default);

    Task<UsuarioAutenticado?> ConstruirContextoUsuarioActivoAsync(
        int idUsuario,
        DateOnly fechaVigencia,
        IPAddress? ipOrigen = null,
        CancellationToken cancelacion = default,
        Guid? idSesion = null,
        DateTime? ahoraUtc = null);

    Task<IReadOnlyList<RespuestaAuditoria>> ConsultarAuditoriasAsync(
        FiltroAuditoria filtro,
        CancellationToken cancelacion = default);
}

public sealed record CredencialesUsuarioAlmacenadas(
    int IdUsuario,
    string NombreUsuario,
    string ContrasenaHash,
    string Estado);
