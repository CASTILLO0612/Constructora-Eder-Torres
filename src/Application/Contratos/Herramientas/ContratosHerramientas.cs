using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;

public sealed record SolicitudCrearCategoriaHerramienta(
    string Nombre,
    string? Descripcion);

public sealed record RespuestaCategoriaHerramienta(
    int IdCategoria,
    string Nombre,
    string? Descripcion,
    bool Activo);

public sealed record SolicitudCrearTipoHerramienta(
    int IdCategoria,
    string Nombre,
    string? Descripcion);

public sealed record RespuestaTipoHerramienta(
    int IdTipoHerramienta,
    int IdCategoria,
    string NombreCategoria,
    string Nombre,
    string? Descripcion,
    bool Activo);

public sealed record SolicitudRegistrarHerramienta(
    int IdTipoHerramienta,
    string CodigoInterno,
    string? NumeroSerie,
    string? Marca,
    string? Modelo,
    DateOnly FechaAdquisicion,
    string Condicion = "NUEVA");

public sealed record RespuestaHerramienta(
    int IdHerramienta,
    int IdTipoHerramienta,
    string NombreTipoHerramienta,
    int IdCategoria,
    string NombreCategoria,
    string CodigoInterno,
    string? NumeroSerie,
    string? Marca,
    string? Modelo,
    DateOnly FechaAdquisicion,
    string Estado,
    string Condicion);

public sealed record SolicitudAsignarHerramienta(
    int IdHerramienta,
    int IdAsignacionTrabajadorObra,
    DateTime EntregadaEn,
    string? CondicionEntrega = null,
    Guid? UuidRegistro = null,
    bool EsSincronizacionDiferida = false) : ISolicitudSincronizable;

public sealed record RespuestaAsignacionHerramienta(
    long IdAsignacionHerramienta,
    int IdHerramienta,
    string CodigoInternoHerramienta,
    string NombreTipoHerramienta,
    int IdAsignacionTrabajadorObra,
    int IdTrabajador,
    string NombreCompletoTrabajador,
    int IdObra,
    string CodigoObra,
    int IdUsuarioEntrega,
    DateTime EntregadaEn,
    string CondicionEntrega,
    DateTime? DevueltaEn,
    string? CondicionDevolucion,
    string Estado,
    Guid UuidRegistro,
    DateTime CreadoEn,
    DateTime? SincronizadoEn,
    bool FueIdempotente = false) : IRespuestaIdempotente;

public sealed record SolicitudRegistrarEventoHerramienta(
    long IdAsignacionHerramienta,
    string TipoIncidencia,
    DateTime FechaHora,
    string Descripcion,
    string? CondicionDevolucion = null,
    string? Resolucion = null,
    decimal? CostoEstimado = null,
    bool CerrarAsignacion = true,
    bool RetirarHerramienta = false,
    Guid? UuidRegistro = null) : ISolicitudSincronizable;

public sealed record RespuestaIncidenciaHerramienta(
    long IdIncidenciaHerramienta,
    long IdAsignacionHerramienta,
    int IdHerramienta,
    string CodigoInternoHerramienta,
    int IdTrabajador,
    int IdObra,
    string TipoIncidencia,
    DateTime FechaHora,
    string Descripcion,
    string? Resolucion,
    decimal? CostoEstimado,
    int IdUsuarioRegistro,
    Guid UuidRegistro,
    string Estado,
    string EstadoAsignacionResultante,
    string EstadoHerramientaResultante,
    bool FueIdempotente = false) : IRespuestaIdempotente;

public sealed record SolicitudActualizarHerramienta(string? NumeroSerie, string? Marca, string? Modelo,
    DateOnly FechaAdquisicion);

public sealed record SolicitudRevisarHerramienta(string Condicion, bool Retirar, string Motivo);

public sealed record SolicitudAtenderIncidenciaHerramienta(string Estado, string Resolucion,
    decimal? CostoEstimado = null);

public interface IPuertoHerramientas
{
    Task<RespuestaHerramienta?> ActualizarHerramientaAsync(int idHerramienta, SolicitudActualizarHerramienta solicitud,
        RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default);
    Task<bool> TieneIncidenciasPendientesAsync(int idHerramienta, CancellationToken cancelacion = default);
    Task<RespuestaHerramienta?> RevisarHerramientaAsync(int idHerramienta, string condicion, string estado,
        RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default);
    Task<RespuestaIncidenciaHerramienta?> AtenderIncidenciaAsync(long idIncidencia, string estado, string resolucion,
        decimal? costo, RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default);
    Task<IReadOnlyList<RespuestaIncidenciaHerramienta>> ListarIncidenciasAsync(int? idObra,
        long? idAsignacionHerramienta, IReadOnlySet<int>? idsObrasPermitidas, CancellationToken cancelacion = default,
        int? idHerramienta = null);

    Task<RespuestaIncidenciaHerramienta?> ObtenerIncidenciaPorIdAsync(long idIncidencia, CancellationToken cancelacion = default);

    Task<bool> ExisteCategoriaPorNombreAsync(string nombre, CancellationToken cancelacion = default);

    Task<RespuestaCategoriaHerramienta?> ObtenerCategoriaPorIdAsync(int idCategoria, CancellationToken cancelacion = default);

    Task<RespuestaCategoriaHerramienta> CrearCategoriaAsync(SolicitudCrearCategoriaHerramienta solicitud, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaCategoriaHerramienta>> ListarCategoriasAsync(CancellationToken cancelacion = default);

    Task<bool> ExisteTipoPorNombreEnCategoriaAsync(int idCategoria, string nombre, CancellationToken cancelacion = default);

    Task<RespuestaTipoHerramienta?> ObtenerTipoPorIdAsync(int idTipoHerramienta, CancellationToken cancelacion = default);

    Task<RespuestaTipoHerramienta> CrearTipoAsync(SolicitudCrearTipoHerramienta solicitud, CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaTipoHerramienta>> ListarTiposAsync(int? idCategoria = null, CancellationToken cancelacion = default);

    Task<bool> ExisteHerramientaPorCodigoAsync(string codigoInterno, CancellationToken cancelacion = default);

    Task<bool> ExisteHerramientaPorSerieAsync(string numeroSerie, CancellationToken cancelacion = default);

    Task<RespuestaHerramienta?> ObtenerHerramientaPorIdAsync(int idHerramienta, CancellationToken cancelacion = default);

    Task<RespuestaHerramienta> RegistrarHerramientaAsync(
        SolicitudRegistrarHerramienta solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaHerramienta>> ListarHerramientasAsync(
        int? idTipoHerramienta = null,
        string? estado = null,
        string? condicion = null,
        CancellationToken cancelacion = default);

    Task<ContextoAsignacionObra?> ObtenerContextoAsignacionTrabajadorAsync(
        int idAsignacionTrabajadorObra,
        CancellationToken cancelacion = default);

    Task<bool> TieneAsignacionHerramientaActivaAsync(int idHerramienta, CancellationToken cancelacion = default);

    Task<RespuestaAsignacionHerramienta?> ObtenerAsignacionPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default);

    Task<RespuestaAsignacionHerramienta?> ObtenerAsignacionPorIdAsync(long idAsignacionHerramienta, CancellationToken cancelacion = default);

    Task<RespuestaAsignacionHerramienta> AsignarHerramientaAsync(
        SolicitudAsignarHerramienta solicitud,
        string condicionEntrega,
        int idUsuarioEntrega,
        Guid uuidRegistro,
        DateTime creadoEnUtc,
        DateTime? sincronizadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaAsignacionHerramienta>> ListarAsignacionesHerramientaAsync(
        int? idObra = null,
        bool soloActivas = false,
        IReadOnlySet<int>? idsObrasPermitidas = null,
        CancellationToken cancelacion = default,
        string? estado = null);

    Task<RespuestaIncidenciaHerramienta?> ObtenerIncidenciaPorUuidAsync(Guid uuidRegistro, CancellationToken cancelacion = default);

    Task<RespuestaIncidenciaHerramienta> RegistrarEventoHerramientaAsync(
        SolicitudRegistrarEventoHerramienta solicitud,
        string nuevoEstadoAsignacion,
        string? condicionDevolucion,
        string nuevoEstadoHerramienta,
        string nuevaCondicionHerramienta,
        string estadoIncidencia,
        int idUsuarioRegistro,
        Guid uuidRegistro,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);
}
