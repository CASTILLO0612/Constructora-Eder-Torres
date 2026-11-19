using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using System.ComponentModel.DataAnnotations;

namespace ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;

public sealed record SolicitudRegistrarAsistencia(
    int IdAsignacion,
    DateOnly FechaJornada,
    DateTime EntradaEn,
    DateTime? SalidaEn = null,
    [property: Required] decimal? LatitudEntrada = null,
    [property: Required] decimal? LongitudEntrada = null,
    decimal? LatitudSalida = null,
    decimal? LongitudSalida = null,
    Guid? UuidRegistro = null,
    bool EsSincronizacionDiferida = false,
    [property: Required] decimal? PrecisionGpsEntradaMetros = null,
    decimal? PrecisionGpsSalidaMetros = null) : ISolicitudSincronizable;

public sealed record ItemMarcacionCuadrilla(
    int IdAsignacion,
    DateTime EntradaEn,
    DateTime? SalidaEn = null,
    Guid? UuidRegistro = null);

public sealed record SolicitudRegistrarAsistenciaCuadrilla(
    int IdObra,
    DateOnly FechaJornada,
    [property: Required] decimal? LatitudLectura,
    [property: Required] decimal? LongitudLectura,
    IReadOnlyList<ItemMarcacionCuadrilla> Marcaciones,
    bool EsSincronizacionDiferida = false,
    [property: Required] decimal? PrecisionGpsMetros = null);

public sealed record SolicitudRegistrarSalidaAsistencia(
    DateTime SalidaEn,
    [property: Required] decimal? LatitudSalida = null,
    [property: Required] decimal? LongitudSalida = null,
    Guid? UuidRegistro = null,
    bool EsSincronizacionDiferida = false,
    [property: Required] decimal? PrecisionGpsSalidaMetros = null);

public sealed record SolicitudValidarUbicacionAsistencia(int IdAsignacion,
    [property: Required] decimal? Latitud, [property: Required] decimal? Longitud,
    [property: Required] decimal? PrecisionGpsMetros = null);

public sealed record RespuestaValidacionUbicacionAsistencia(
    int IdAsignacion, int IdObra, bool MarcacionHabilitada, decimal DistanciaMetros, decimal RadioAsistenciaMetros,
    decimal PrecisionGpsMetros, decimal DistanciaMaximaEstimadaMetros);

public sealed record RespuestaAsignacionAsistencia(
    int IdAsignacion, int IdObra, string CodigoObra, string NombreObra, string Direccion,
    string EstadoObra, decimal? Latitud, decimal? Longitud, decimal? RadioAsistenciaMetros,
    DateOnly FechaInicio, DateOnly? FechaFin, long? IdAsistenciaAbierta);

public sealed record RespuestaAsistencia(
    long IdAsistencia,
    int IdAsignacion,
    int IdTrabajador,
    string NombreCompletoTrabajador,
    int IdObra,
    DateOnly FechaJornada,
    DateTime EntradaEn,
    DateTime? SalidaEn,
    decimal? LatitudEntrada,
    decimal? LongitudEntrada,
    decimal? LatitudSalida,
    decimal? LongitudSalida,
    string Estado,
    int IdUsuarioRegistro,
    Guid UuidRegistro,
    DateTime CreadoEn,
    DateTime? SincronizadoEn,
    bool FueIdempotente = false,
    decimal? PrecisionGpsEntradaMetros = null,
    decimal? PrecisionGpsSalidaMetros = null) : IRespuestaIdempotente;

public sealed record SolicitudCrearCorreccionAsistencia(
    long IdAsistencia,
    DateTime? EntradaNueva,
    DateTime? SalidaNueva,
    string Motivo);

public sealed record SolicitudResolverCorreccionAsistencia(
    string Decision);

public sealed record RespuestaCorreccionAsistencia(
    long IdCorreccion,
    long IdAsistencia,
    int IdObra,
    int IdUsuarioSolicita,
    int? IdUsuarioAprueba,
    DateTime? EntradaAnterior,
    DateTime? SalidaAnterior,
    DateTime? EntradaNueva,
    DateTime? SalidaNueva,
    string Motivo,
    string Estado,
    DateTime SolicitadoEn,
    DateTime? ResueltoEn);

public sealed record FiltroConsolidadoAsistencia(
    int? IdObra = null,
    int? IdTrabajador = null,
    int? IdCuadrilla = null,
    DateOnly? FechaDesde = null,
    DateOnly? FechaHasta = null,
    string? Estado = null);

public sealed record SolicitudRegistrarSalidasCuadrilla(int IdObra, DateTime SalidaEn,
    [property: Required] decimal? LatitudLectura, [property: Required] decimal? LongitudLectura,
    [property: Required] decimal? PrecisionGpsMetros, IReadOnlyList<long> IdsAsistencias,
    bool EsSincronizacionDiferida = false);

public sealed record ItemConsolidadoAsistencia(
    int IdObra,
    string CodigoObra,
    string NombreObra,
    int IdTrabajador,
    string CedulaTrabajador,
    string NombreCompletoTrabajador,
    int? IdCuadrilla,
    string? NombreCuadrilla,
    int TotalJornadasRegistradas,
    int JornadasCompletas,
    int JornadasAbiertas,
    int JornadasCorregidas,
    decimal HorasTrabajadasTotales);

public interface IPuertoAsistencia
{
    /// <summary>Serializa las escrituras de cada trabajador durante la transacción actual.</summary>
    Task BloquearMarcacionesAsync(IReadOnlyList<int> idsAsignaciones, CancellationToken cancelacion = default);
    Task BloquearAsistenciaAsync(long idAsistencia, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaAsignacionAsistencia>> ListarAsignacionesPropiasAsync(
        int idTrabajador, DateOnly fecha, CancellationToken cancelacion = default);

    Task<bool> ExisteSolapamientoAsync(int idTrabajador, DateTime entradaEn, DateTime? salidaEn,
        long? excluirIdAsistencia = null, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaCorreccionAsistencia>> ListarCorreccionesAsync(
        int? idObra, string? estado, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaAsistencia>> ListarRegistrosAsync(FiltroConsolidadoAsistencia filtro,
        IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default);

    Task<RespuestaAsistencia?> ObtenerPorUuidAsync(
        Guid uuidRegistro,
        CancellationToken cancelacion = default);

    Task<RespuestaAsistencia?> ObtenerPorIdAsync(
        long idAsistencia,
        CancellationToken cancelacion = default);

    Task<ContextoAsignacionObra?> ObtenerContextoAsignacionAsync(
        int idAsignacion,
        CancellationToken cancelacion = default);

    Task<bool> ExisteMarcacionMismaEntradaAsync(
        int idAsignacion,
        DateTime entradaEn,
        CancellationToken cancelacion = default);

    Task<RespuestaAsistencia> RegistrarAsistenciaAsync(
        SolicitudRegistrarAsistencia solicitud,
        string estado,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaAsistencia?> RegistrarSalidaAsistenciaAsync(
        long idAsistencia,
        DateTime salidaEn,
        decimal? latitudSalida,
        decimal? longitudSalida,
        decimal? precisionGpsSalidaMetros,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaCorreccionAsistencia> CrearCorreccionAsistenciaAsync(
        long idAsistencia,
        int idUsuarioSolicita,
        DateTime? entradaAnterior,
        DateTime? salidaAnterior,
        DateTime? entradaNueva,
        DateTime? salidaNueva,
        string motivo,
        DateTime solicitadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaCorreccionAsistencia?> ObtenerCorreccionPorIdAsync(
        long idCorreccion,
        CancellationToken cancelacion = default);

    Task<RespuestaCorreccionAsistencia?> ResolverCorreccionAsistenciaAsync(
        long idCorreccion,
        int idUsuarioAprueba,
        string nuevoEstado,
        DateTime resueltoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<ItemConsolidadoAsistencia>> ConsultarConsolidadoAsync(
        FiltroConsolidadoAsistencia filtro,
        IReadOnlySet<int>? idsObrasPermitidas,
        CancellationToken cancelacion = default);
}
