using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;

public interface IAlmacenamientoEvidencias
{
    long MaximoBytes { get; }

    Task<(string RutaRelativa, string HuellaSha256)> GuardarEvidenciaAsync(
        int idObra,
        long idAvance,
        string nombreArchivoOriginal,
        ReadOnlyMemory<byte> contenido,
        CancellationToken cancelacion = default);

    Task EliminarAsync(string rutaRelativa, CancellationToken cancelacion = default);

    Stream? AbrirLectura(string rutaRelativa);
}

public sealed record DescargaEvidencia(Stream Contenido, string NombreArchivo);

public sealed record SolicitudParticipanteActividad(
    int IdAsignacion,
    string? RolParticipacion = null);

public sealed record RespuestaParticipanteActividad(
    int IdActividad,
    int IdAsignacion,
    int IdTrabajador,
    string NombreCompletoTrabajador,
    string? RolParticipacion);

public sealed record SolicitudCrearActividad(
    int IdObra,
    int? IdAsignacionResponsable,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string UnidadMedida,
    decimal CantidadPlanificada,
    DateOnly FechaInicioPlan,
    DateOnly FechaFinPlan,
    string? EstadoInicial = null,
    IReadOnlyList<SolicitudParticipanteActividad>? Participantes = null);

public sealed record SolicitudActualizarActividad(
    int? IdAsignacionResponsable,
    string Nombre,
    string? Descripcion,
    string UnidadMedida,
    decimal CantidadPlanificada,
    DateOnly FechaInicioPlan,
    DateOnly FechaFinPlan,
    string Estado,
    string? MotivoVariacion = null);

public sealed record RespuestaActividad(
    int IdActividad,
    int IdObra,
    int? IdAsignacionResponsable,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string UnidadMedida,
    decimal CantidadPlanificada,
    decimal CantidadEjecutadaAcumulada,
    DateOnly FechaInicioPlan,
    DateOnly FechaFinPlan,
    string Estado,
    IReadOnlyList<RespuestaParticipanteActividad> Participantes);

public sealed record SolicitudRegistrarAvance(
    int IdActividad,
    DateOnly FechaAvance,
    decimal CantidadEjecutada,
    string? Observaciones = null,
    string? Estado = null,
    bool EsIncompletoORetrasado = false,
    long? IdIncidenciaVinculada = null,
    Guid? UuidRegistro = null,
    bool EsSincronizacionDiferida = false,
    string? EstadoActividad = null) : ISolicitudSincronizable;

public sealed record SolicitudJustificarActividad(
    int IdActividad,
    DateOnly FechaAvance,
    decimal CantidadEjecutada,
    string MotivoObligatorio,
    long? IdIncidenciaExistente = null);

public sealed record RespuestaAvance(
    long IdAvance,
    int IdActividad,
    int IdObra,
    string CodigoActividad,
    string NombreActividad,
    DateOnly FechaAvance,
    decimal CantidadEjecutada,
    string? Observaciones,
    string Estado,
    int IdUsuarioRegistro,
    Guid UuidRegistro,
    DateTime CreadoEn,
    DateTime? SincronizadoEn,
    bool FueIdempotente = false,
    [property: System.Text.Json.Serialization.JsonIgnore] bool FueConfirmacionBorrador = false) : IRespuestaIdempotente;

public sealed record SolicitudRegistrarEvidencia(
    long IdAvance,
    string TipoEvidencia,
    string? NombreArchivo = null,
    byte[]? ContenidoArchivo = null,
    DateTime? CapturadaEn = null,
    decimal? Latitud = null,
    decimal? Longitud = null,
    Guid? UuidRegistro = null,
    bool EsSincronizacionDiferida = false) : ISolicitudSincronizable;

public sealed record RespuestaEvidencia(
    long IdEvidencia,
    long IdAvance,
    int IdActividad,
    int IdObra,
    string TipoEvidencia,
    string RutaArchivo,
    string HuellaArchivo,
    DateTime CapturadaEn,
    decimal? Latitud,
    decimal? Longitud,
    int IdUsuarioRegistro,
    Guid UuidRegistro,
    DateTime CreadoEn,
    DateTime? SincronizadoEn,
    bool FueIdempotente = false) : IRespuestaIdempotente;

public sealed record SolicitudRegistrarIncidenciaObra(
    int IdObra,
    string TipoIncidencia,
    DateTime InicioEn,
    DateTime? FinEn,
    decimal? HorasPerdidas,
    string Descripcion,
    string? Justificacion,
    string? Estado = null,
    IReadOnlyList<int>? IdsActividadesAfectadas = null,
    Guid? UuidRegistro = null,
    bool EsSincronizacionDiferida = false) : ISolicitudSincronizable;

public sealed record RespuestaIncidenciaObra(
    long IdIncidencia,
    int IdObra,
    string TipoIncidencia,
    DateTime InicioEn,
    DateTime? FinEn,
    decimal? HorasPerdidas,
    string Descripcion,
    string? Justificacion,
    string Estado,
    int IdUsuarioRegistro,
    Guid UuidRegistro,
    DateTime CreadoEn,
    DateTime? SincronizadoEn,
    IReadOnlyList<int> IdsActividadesAfectadas,
    bool FueIdempotente = false) : IRespuestaIdempotente;

public sealed record SolicitudParticipanteActa(
    int IdAsignacion,
    string TipoParticipacion,
    string? RutaFirma = null,
    DateTime? FirmadoEn = null);

public sealed record RespuestaParticipanteActa(
    int IdActa,
    int IdAsignacion,
    int IdTrabajador,
    string NombreCompletoTrabajador,
    string TipoParticipacion,
    string? RutaFirma,
    DateTime? FirmadoEn);

public sealed record RespuestaAprobacionActa(
    int IdAprobacion,
    int IdActa,
    int IdUsuario,
    string NombreUsuario,
    string Decision,
    string? Comentario,
    DateTime FechaHora);

public sealed record SolicitudElaborarActa(
    int IdObra,
    string NumeroActa,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    string? Observaciones = null,
    string? MejorasPropuestas = null,
    string? SolicitudesCambio = null,
    bool EnviarARevision = false,
    IReadOnlyList<long>? IdsAvances = null,
    IReadOnlyList<long>? IdsIncidencias = null,
    IReadOnlyList<SolicitudParticipanteActa>? Participantes = null,
    Guid? UuidRegistro = null);

public sealed record SolicitudResolverActa(
    string Decision,
    string? Comentario = null);

public sealed record SolicitudEnviarActa;

public sealed record SolicitudActualizarActa(
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    string? Observaciones = null,
    string? MejorasPropuestas = null,
    string? SolicitudesCambio = null,
    IReadOnlyList<long>? IdsAvances = null,
    IReadOnlyList<long>? IdsIncidencias = null,
    IReadOnlyList<SolicitudParticipanteActa>? Participantes = null);

public sealed record RespuestaActa(
    int IdActa,
    int IdObra,
    string CodigoObra,
    string NombreObra,
    string NumeroActa,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    string? Observaciones,
    string? MejorasPropuestas,
    string? SolicitudesCambio,
    string Estado,
    int IdUsuarioCreador,
    Guid UuidRegistro,
    DateTime CreadoEn,
    IReadOnlyList<RespuestaAvance> AvancesConsolidados,
    IReadOnlyList<RespuestaEvidencia> EvidenciasConsolidadas,
    IReadOnlyList<RespuestaIncidenciaObra> IncidenciasConsolidadas,
    IReadOnlyList<RespuestaParticipanteActa> Participantes,
    IReadOnlyList<RespuestaAprobacionActa> Aprobaciones);

public interface IPuertoSeguimientoObra
{
    Task<IReadOnlyList<RespuestaEvidencia>> ListarEvidenciasPorAvanceAsync(
        long idAvance, CancellationToken cancelacion = default);

    Task<RespuestaIncidenciaObra?> ObtenerIncidenciaPorIdAsync(long idIncidencia, CancellationToken cancelacion = default);

    Task<RespuestaAvance?> ObtenerAvancePorIdAsync(long idAvance, CancellationToken cancelacion = default);

    Task<RespuestaEvidencia?> ObtenerEvidenciaPorIdAsync(long idEvidencia, CancellationToken cancelacion = default);
    Task<bool> ExisteObraAsync(int idObra, CancellationToken cancelacion = default);

    Task<int?> ObtenerIdObraDeAsignacionAsync(int idAsignacion, CancellationToken cancelacion = default);

    Task<int?> ObtenerIdObraDeActividadAsync(int idActividad, CancellationToken cancelacion = default);

    Task<(int IdActividad, int IdObra)?> ObtenerContextoAvanceAsync(long idAvance, CancellationToken cancelacion = default);

    Task<int?> ObtenerIdObraDeIncidenciaAsync(long idIncidencia, CancellationToken cancelacion = default);

    Task<bool> ExisteCodigoActividadEnObraAsync(int idObra, string codigo, int? excluirIdActividad = null, CancellationToken cancelacion = default);

    Task<RespuestaActividad> CrearActividadAsync(
        SolicitudCrearActividad solicitud,
        string estadoInicial,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaActividad?> ObtenerActividadPorIdAsync(int idActividad, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaActividad>> ListarActividadesPorObraAsync(int idObra, CancellationToken cancelacion = default);

    Task<RespuestaActividad?> ActualizarActividadAsync(
        int idActividad,
        SolicitudActualizarActividad solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaParticipanteActividad> AgregarParticipanteActividadAsync(
        int idActividad,
        SolicitudParticipanteActividad solicitud,
        CancellationToken cancelacion = default);

    Task<RespuestaAvance?> ObtenerAvancePorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default);

    Task<bool> ExisteAvanceActividadFechaAsync(int idActividad, DateOnly fechaAvance, CancellationToken cancelacion = default);

    Task<RespuestaAvance> RegistrarAvanceAsync(
        SolicitudRegistrarAvance solicitud,
        string estado,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default,
        long? idAvanceBorrador = null);

    Task<IReadOnlyList<RespuestaAvance>> ListarAvancesPorObraAsync(
        int idObra,
        int? idActividad = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken cancelacion = default);

    Task<RespuestaEvidencia?> ObtenerEvidenciaPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default);

    Task<RespuestaEvidencia> RegistrarEvidenciaAsync(
        long idAvance,
        string tipoEvidencia,
        string rutaArchivo,
        string huellaArchivo,
        DateTime capturadaEnUtc,
        decimal? latitud,
        decimal? longitud,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaEvidencia>> ListarEvidenciasPorObraAsync(
        int idObra,
        long? idAvance = null,
        CancellationToken cancelacion = default);

    Task<RespuestaIncidenciaObra?> ObtenerIncidenciaPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default);

    Task<RespuestaIncidenciaObra> RegistrarIncidenciaObraAsync(
        SolicitudRegistrarIncidenciaObra solicitud,
        string estado,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaIncidenciaObra>> ListarIncidenciasPorObraAsync(
        int idObra,
        CancellationToken cancelacion = default);

    Task<bool> ExisteNumeroActaEnObraAsync(int idObra, string numeroActa, CancellationToken cancelacion = default);

    Task<RespuestaActa> ElaborarActaAsync(
        SolicitudElaborarActa solicitud,
        string estadoInicial,
        int idUsuarioCreador,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaActa?> ObtenerActaPorIdAsync(int idActa, CancellationToken cancelacion = default);

    Task<IReadOnlyList<ResumenActa>> ListarActasPorObraAsync(int idObra, CancellationToken cancelacion = default);

    Task<RespuestaActa?> EnviarActaARevisionAsync(
        int idActa,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaActa?> ActualizarActaAsync(int idActa, SolicitudActualizarActa solicitud,
        RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default);

    Task<RespuestaActa?> ResolverActaAsync(
        int idActa,
        int idUsuarioRevisor,
        string decision,
        string? comentario,
        DateTime fechaHoraUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);
}


public sealed record ResumenActa(int IdActa, int IdObra, string NumeroActa,
    DateOnly PeriodoInicio, DateOnly PeriodoFin, string Estado, DateTime CreadoEn);
