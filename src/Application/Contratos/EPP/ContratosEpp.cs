using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.Contratos.EPP;

public sealed record SolicitudCrearTipoEpp(
    string Nombre,
    string? Descripcion,
    int? VidaUtilDias,
    int ExistenciaMinima = 0);

public sealed record RespuestaTipoEpp(
    int IdTipoEpp,
    string Nombre,
    string? Descripcion,
    int? VidaUtilDias,
    int ExistenciaMinima,
    bool Activo);

public sealed record SolicitudRegistrarEntradaEpp(
    int IdTipoEpp,
    string CodigoInterno,
    string? NumeroSerie,
    DateOnly FechaAdquisicion,
    string Condicion = "NUEVO",
    string? Observacion = null,
    Guid? UuidMovimiento = null);

public sealed record RespuestaEpp(
    int IdEpp,
    int IdTipoEpp,
    string NombreTipoEpp,
    string CodigoInterno,
    string? NumeroSerie,
    DateOnly FechaAdquisicion,
    string Estado,
    string Condicion);

public sealed record ResumenExistenciaTipoEpp(
    int IdTipoEpp,
    string NombreTipoEpp,
    int ExistenciaMinima,
    int TotalDisponibles,
    int TotalEntregados,
    int TotalEnRevision,
    int TotalPerdidos,
    int TotalRetirados,
    bool BajoExistenciaMinima);

public sealed record SolicitudRegistrarEntregaEpp(
    int IdEpp,
    int IdAsignacion,
    DateTime EntregadoEn,
    string? CondicionEntrega = null,
    string? RutaConstancia = null,
    Guid? UuidRegistro = null,
    bool EsSincronizacionDiferida = false) : ISolicitudSincronizable;

public sealed record SolicitudDevolverEpp(
    DateTime DevueltoEn,
    string CondicionDevolucion,
    string? Observacion = null,
    Guid? UuidRegistro = null);

public sealed record ContextoMovimientoEpp(long? IdEntregaEpp, string TipoMovimiento,
    DateTime FechaHora, string? Observacion);

public sealed record RespuestaEntregaEpp(
    long IdEntregaEpp,
    int IdEpp,
    string CodigoInternoEpp,
    int IdTipoEpp,
    string NombreTipoEpp,
    int IdAsignacion,
    int IdTrabajador,
    string NombreCompletoTrabajador,
    int IdObra,
    string CodigoObra,
    int IdUsuarioEntrega,
    DateTime EntregadoEn,
    string CondicionEntrega,
    DateTime? DevueltoEn,
    string? CondicionDevolucion,
    string Estado,
    string? RutaConstancia,
    Guid UuidRegistro,
    DateTime CreadoEn,
    DateTime? SincronizadoEn,
    bool FueIdempotente = false) : IRespuestaIdempotente;

public sealed record SolicitudRegistrarReposicionEpp(
    long IdEntregaOrigen,
    int? IdEppSustituto,
    string Motivo,
    string Descripcion,
    bool EquipoDevuelto,
    DateTime? SolicitadaEn = null,
    Guid? UuidRegistro = null,
    bool EsSincronizacionDiferida = false) : ISolicitudSincronizable;

public sealed record RespuestaReposicionEpp(
    long IdReposicionEpp,
    long IdEntregaOrigen,
    long? IdEntregaNueva,
    int IdTrabajador,
    int IdObra,
    string Motivo,
    string Descripcion,
    bool EquipoDevuelto,
    int IdUsuarioSolicita,
    DateTime SolicitadaEn,
    int? IdUsuarioAprueba,
    DateTime? ResueltaEn,
    string Estado,
    Guid UuidRegistro,
    bool FueIdempotente = false) : IRespuestaIdempotente;

public sealed record EventoHistorialEppTrabajador(
    DateTime FechaHora,
    string TipoEvento,
    long IdReferencia,
    int IdObra,
    string CodigoObra,
    string NombreObra,
    int IdEpp,
    string CodigoInternoEpp,
    string NombreTipoEpp,
    string EstadoEquipo,
    string Condicion,
    string? Detalle);

public sealed record FiltroReporteEpp(
    int? IdObra = null,
    int? IdTrabajador = null,
    int? IdTipoEpp = null,
    DateOnly? FechaDesde = null,
    DateOnly? FechaHasta = null);

public sealed record ItemReporteEpp(
    int IdObra,
    string CodigoObra,
    string NombreObra,
    int IdTrabajador,
    string CedulaTrabajador,
    string NombreCompletoTrabajador,
    int IdTipoEpp,
    string NombreTipoEpp,
    int CantidadEntregada,
    int CantidadActiva,
    int CantidadReemplazada,
    int CantidadPerdida);

public sealed record SolicitudRevisarEpp(string Condicion, bool Retirar, string Motivo);

public interface IPuertoEpp
{
    Task<RespuestaEpp?> RevisarEppAsync(int idEpp, string condicion, string estado, int idUsuario, string motivo,
        RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default);
    Task<ContextoMovimientoEpp?> ObtenerMovimientoPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaEntregaEpp>> ListarEntregasAsync(int? idObra, int? idTrabajador,
        string? estado, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaReposicionEpp>> ListarReposicionesAsync(int? idObra,
        string? estado, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default);

    Task<RespuestaReposicionEpp?> ObtenerReposicionPorIdAsync(long idReposicion, CancellationToken cancelacion = default);

    Task<bool> ExisteTipoEppPorNombreAsync(string nombre, CancellationToken cancelacion = default);

    Task<RespuestaTipoEpp?> ObtenerTipoEppPorIdAsync(int idTipoEpp, CancellationToken cancelacion = default);

    Task<RespuestaTipoEpp> CrearTipoEppAsync(SolicitudCrearTipoEpp solicitud, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaTipoEpp>> ListarTiposEppAsync(CancellationToken cancelacion = default);

    Task<bool> ExisteEppPorCodigoAsync(string codigoInterno, CancellationToken cancelacion = default);

    Task<bool> ExisteEppPorSerieAsync(string numeroSerie, CancellationToken cancelacion = default);

    Task<RespuestaEpp?> ObtenerEppPorIdAsync(int idEpp, CancellationToken cancelacion = default);

    Task<RespuestaEpp> RegistrarEntradaEppAsync(
        SolicitudRegistrarEntradaEpp solicitud,
        int idUsuario,
        DateTime fechaHoraUtc,
        Guid uuidMovimiento,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaEpp>> ConsultarExistenciasEppAsync(
        int? idTipoEpp,
        string? estado,
        string? condicion,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<ResumenExistenciaTipoEpp>> ConsultarResumenExistenciasAsync(
        int? idTipoEpp = null,
        CancellationToken cancelacion = default);

    Task<ContextoAsignacionObra?> ObtenerContextoAsignacionAsync(
        int idAsignacion,
        CancellationToken cancelacion = default);

    Task<RespuestaEntregaEpp?> ObtenerEntregaPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default);

    Task<RespuestaEntregaEpp?> ObtenerEntregaPorIdAsync(long idEntregaEpp, CancellationToken cancelacion = default);

    Task<RespuestaEntregaEpp> RegistrarEntregaEppAsync(
        SolicitudRegistrarEntregaEpp solicitud,
        string condicionEntrega,
        int idUsuarioEntrega,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaEntregaEpp?> RegistrarDevolucionEppAsync(
        long idEntregaEpp,
        SolicitudDevolverEpp solicitud,
        int idUsuario,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaReposicionEpp?> ObtenerReposicionPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default);

    Task<RespuestaReposicionEpp> RegistrarReposicionEppAsync(
        SolicitudRegistrarReposicionEpp solicitud,
        int idUsuario,
        DateTime ahoraUtc,
        Guid uuidRegistro,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        long? idReposicionExistente,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<EventoHistorialEppTrabajador>> ConsultarHistorialPorTrabajadorAsync(
        int idTrabajador,
        IReadOnlySet<int>? idsObrasPermitidas,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<ItemReporteEpp>> GenerarReporteEppAsync(
        FiltroReporteEpp filtro,
        IReadOnlySet<int>? idsObrasPermitidas,
        CancellationToken cancelacion = default);
}


public sealed record SolicitudEjecutarReposicionEpp(int IdEppSustituto, bool EquipoDevuelto, DateTime? EjecutadaEn = null);
