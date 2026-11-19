using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Comun;

namespace ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;

public static class EstadosAcuseSincronizacion
{
    public const string PendienteReintento = "PENDIENTE_REINTENTO";
    public const string Sincronizado = "SINCRONIZADO";
    public const string IdempotentePrevio = "IDEMPOTENTE_PREVIO";
    public const string RechazadoValidacion = "RECHAZADO_VALIDACION";
    public const string RechazadoAutorizacion = "RECHAZADO_AUTORIZACION";
    public const string RechazadoConflicto = "RECHAZADO_CONFLICTO";
}

public static class TiposOperacionSincronizacion
{
    public const string Asistencia = "ASISTENCIA";
    public const string SalidaAsistencia = "SALIDA_ASISTENCIA";
    public const string DevolucionEpp = "DEVOLUCION_EPP";
    public const string Avance = "AVANCE";
    public const string Evidencia = "EVIDENCIA";
    public const string IncidenciaObra = "INCIDENCIA_OBRA";
    public const string EntregaEpp = "ENTREGA_EPP";
    public const string ReposicionEpp = "REPOSICION_EPP";
    public const string AsignacionHerramienta = "ASIGNACION_HERRAMIENTA";
    public const string EventoHerramienta = "EVENTO_HERRAMIENTA";
}

public sealed record SolicitudSincronizacionLote(
    IReadOnlyList<SolicitudRegistrarAsistencia>? Asistencias = null,
    IReadOnlyList<SolicitudRegistrarAvance>? Avances = null,
    IReadOnlyList<SolicitudRegistrarEvidencia>? Evidencias = null,
    IReadOnlyList<SolicitudRegistrarIncidenciaObra>? IncidenciasObra = null,
    IReadOnlyList<SolicitudRegistrarEntregaEpp>? EntregasEpp = null,
    IReadOnlyList<SolicitudRegistrarReposicionEpp>? ReposicionesEpp = null,
    IReadOnlyList<SolicitudAsignarHerramienta>? AsignacionesHerramienta = null,
    IReadOnlyList<SolicitudRegistrarEventoHerramienta>? EventosHerramienta = null,
    IReadOnlyList<SolicitudSincronizarSalidaAsistencia>? SalidasAsistencia = null,
    IReadOnlyList<SolicitudSincronizarDevolucionEpp>? DevolucionesEpp = null);

public sealed record SolicitudSincronizarSalidaAsistencia(long? IdAsistencia, DateTime SalidaEn,
    decimal? LatitudSalida, decimal? LongitudSalida, Guid? UuidRegistro,
    decimal? PrecisionGpsSalidaMetros = null) : ISolicitudSincronizable;

public sealed record SolicitudSincronizarDevolucionEpp(long IdEntregaEpp, DateTime DevueltoEn,
    string CondicionDevolucion, string? Observacion, Guid? UuidRegistro) : ISolicitudSincronizable;

public sealed record AcuseOperacionSincronizacion(
    Guid UuidRegistro,
    string TipoOperacion,
    string EstadoAcuse,
    string? IdRegistroServidor,
    DateTime? SincronizadoEnUtc,
    string? CodigoError = null,
    string? MensajeError = null);

public sealed record RespuestaSincronizacionLote(
    DateTime ProcesadoEnUtc,
    int TotalRecibidas,
    int TotalConfirmadas,
    int TotalIdempotentes,
    int TotalRechazadas,
    IReadOnlyList<AcuseOperacionSincronizacion> Acuses);
