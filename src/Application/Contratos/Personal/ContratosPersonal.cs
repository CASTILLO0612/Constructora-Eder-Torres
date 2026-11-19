using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.Contratos.Personal;

public sealed record SolicitudCargo(
    string Nombre,
    string? Descripcion);

public sealed record RespuestaCargo(
    int IdCargo,
    string Nombre,
    string? Descripcion,
    bool Activo);

public sealed record SolicitudEspecialidad(
    string Nombre,
    string? Descripcion);

public sealed record RespuestaEspecialidad(
    int IdEspecialidad,
    string Nombre,
    string? Descripcion,
    bool Activo);

public sealed record SolicitudCuadrilla(
    string Codigo,
    string Nombre,
    string? Descripcion);

public sealed record RespuestaCuadrilla(
    int IdCuadrilla,
    string Codigo,
    string Nombre,
    string? Descripcion,
    bool Activo);

public sealed record SolicitudEspecialidadTrabajador(
    int IdEspecialidad,
    string? Nivel = null,
    DateOnly? FechaCertificacion = null,
    DateOnly? FechaVencimiento = null);

public sealed record RespuestaEspecialidadTrabajador(
    int IdEspecialidad,
    string NombreEspecialidad,
    string? Nivel,
    DateOnly? FechaCertificacion,
    DateOnly? FechaVencimiento);

public sealed record SolicitudCrearTrabajador(
    int IdCargo,
    string Cedula,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido,
    string? Telefono,
    string? Correo,
    string? Direccion,
    DateOnly FechaIngreso,
    IReadOnlyList<SolicitudEspecialidadTrabajador>? Especialidades = null);

public sealed record SolicitudActualizarTrabajador(
    int IdCargo,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido,
    string? Telefono,
    string? Correo,
    string? Direccion);

public sealed record SolicitudCambiarEstadoTrabajador(
    string Estado);

public sealed record RespuestaTrabajador(
    int IdTrabajador,
    int IdCargo,
    string NombreCargo,
    string Cedula,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido,
    string? Telefono,
    string? Correo,
    string? Direccion,
    DateOnly FechaIngreso,
    string Estado,
    IReadOnlyList<RespuestaEspecialidadTrabajador> Especialidades);

public sealed record SolicitudAsignarTrabajadorObra(
    int IdTrabajador,
    int IdObra,
    int? IdEspecialidad,
    string TipoContratacion,
    DateOnly FechaInicio,
    DateOnly? FechaFin = null,
    bool EsResponsable = false);

public sealed record SolicitudFinalizarAsignacionObra(
    DateOnly FechaFin,
    string Estado = "FINALIZADA");

public sealed record RespuestaAsignacionTrabajadorObra(
    int IdAsignacion,
    int IdTrabajador,
    string NombreCompletoTrabajador,
    string CedulaTrabajador,
    int IdObra,
    string CodigoObra,
    int? IdEspecialidad,
    string? NombreEspecialidad,
    string TipoContratacion,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    bool EsResponsable,
    string Estado);

public sealed record SolicitudAsignarCuadrillaObra(
    int IdCuadrilla,
    int IdObra,
    DateOnly FechaInicio,
    DateOnly? FechaFin = null);

public sealed record RespuestaCuadrillaObra(
    int IdCuadrillaObra,
    int IdCuadrilla,
    string CodigoCuadrilla,
    string NombreCuadrilla,
    int IdObra,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string Estado);

public sealed record SolicitudAsignarTrabajadorCuadrilla(
    int IdAsignacion,
    int IdCuadrillaObra,
    DateOnly FechaInicio,
    DateOnly? FechaFin = null);

public sealed record RespuestaTrabajadorCuadrilla(
    int IdTrabajadorCuadrilla,
    int IdAsignacion,
    int IdTrabajador,
    string NombreCompletoTrabajador,
    int IdCuadrillaObra,
    int IdCuadrilla,
    string NombreCuadrilla,
    int IdObra,
    DateOnly FechaInicio,
    DateOnly? FechaFin);

public interface IPuertoPersonal
{
    Task<RespuestaTrabajadorCuadrilla?> ObtenerMiembroCuadrillaPorIdAsync(
        int idTrabajadorCuadrilla, CancellationToken cancelacion = default);

    Task<bool> TrabajadorPerteneceAObrasAsync(int idTrabajador, IReadOnlySet<int> obras, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaCargo>> ListarCargosAsync(CancellationToken cancelacion = default);

    Task<RespuestaCargo?> ObtenerCargoPorIdAsync(int idCargo, CancellationToken cancelacion = default);

    Task<bool> ExisteCargoPorNombreAsync(string nombre, CancellationToken cancelacion = default);

    Task<RespuestaCargo> CrearCargoAsync(SolicitudCargo solicitud, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaEspecialidad>> ListarEspecialidadesAsync(CancellationToken cancelacion = default);

    Task<RespuestaEspecialidad?> ObtenerEspecialidadPorIdAsync(int idEspecialidad, CancellationToken cancelacion = default);

    Task<bool> ExisteEspecialidadPorNombreAsync(string nombre, CancellationToken cancelacion = default);

    Task<RespuestaEspecialidad> CrearEspecialidadAsync(SolicitudEspecialidad solicitud, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaCuadrilla>> ListarCuadrillasAsync(CancellationToken cancelacion = default);

    Task<RespuestaCuadrilla?> ObtenerCuadrillaPorIdAsync(int idCuadrilla, CancellationToken cancelacion = default);

    Task<bool> ExisteCuadrillaPorCodigoAsync(string codigo, CancellationToken cancelacion = default);

    Task<RespuestaCuadrilla> CrearCuadrillaAsync(SolicitudCuadrilla solicitud, CancellationToken cancelacion = default);

    Task<bool> ExisteTrabajadorPorCedulaAsync(string cedula, int? excluirIdTrabajador = null, CancellationToken cancelacion = default);

    Task<bool> ExisteTrabajadorPorCorreoAsync(string correo, int? excluirIdTrabajador = null, CancellationToken cancelacion = default);

    Task<RespuestaTrabajador> CrearTrabajadorAsync(
        SolicitudCrearTrabajador solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaTrabajador?> ObtenerTrabajadorPorIdAsync(int idTrabajador, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaTrabajador>> ListarTrabajadoresAsync(
        string? estado,
        bool soloAsignables,
        IReadOnlySet<int>? idsObrasAlcance,
        CancellationToken cancelacion = default);

    Task<RespuestaTrabajador?> ActualizarTrabajadorAsync(
        int idTrabajador,
        SolicitudActualizarTrabajador solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaTrabajador?> CambiarEstadoTrabajadorAsync(
        int idTrabajador,
        string nuevoEstado,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaEspecialidadTrabajador> RegistrarEspecialidadTrabajadorAsync(
        int idTrabajador,
        SolicitudEspecialidadTrabajador solicitud,
        CancellationToken cancelacion = default);

    Task<bool> TrabajadorTieneEspecialidadAsync(
        int idTrabajador,
        int idEspecialidad,
        CancellationToken cancelacion = default);

    Task<bool> ExisteObraAsync(int idObra, CancellationToken cancelacion = default);

    Task<bool> ExisteAsignacionActivaTrabajadorObraAsync(
        int idTrabajador,
        int idObra,
        CancellationToken cancelacion = default);

    Task<RespuestaAsignacionTrabajadorObra> AsignarTrabajadorAObraAsync(
        SolicitudAsignarTrabajadorObra solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaAsignacionTrabajadorObra?> ObtenerAsignacionPorIdAsync(
        int idAsignacion,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaAsignacionTrabajadorObra>> ListarAsignacionesObraAsync(
        int idObra,
        bool soloVigentes,
        CancellationToken cancelacion = default);

    Task<RespuestaAsignacionTrabajadorObra?> FinalizarAsignacionObraAsync(
        int idAsignacion,
        DateOnly fechaFin,
        string nuevoEstado,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<bool> ExisteCuadrillaObraActivaAsync(
        int idCuadrilla,
        int idObra,
        CancellationToken cancelacion = default);

    Task<RespuestaCuadrillaObra> AsignarCuadrillaAObraAsync(
        SolicitudAsignarCuadrillaObra solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaCuadrillaObra?> ObtenerCuadrillaObraPorIdAsync(
        int idCuadrillaObra,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaCuadrillaObra>> ListarCuadrillasDeObraAsync(
        int idObra,
        CancellationToken cancelacion = default);

    Task<bool> ExisteTrabajadorCuadrillaActivaAsync(
        int idAsignacion,
        CancellationToken cancelacion = default);

    Task<RespuestaTrabajadorCuadrilla> AsignarTrabajadorACuadrillaAsync(
        SolicitudAsignarTrabajadorCuadrilla solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaTrabajadorCuadrilla>> ListarMiembrosCuadrillaObraAsync(
        int idObra,
        int? idCuadrillaObra = null,
        CancellationToken cancelacion = default);
}
