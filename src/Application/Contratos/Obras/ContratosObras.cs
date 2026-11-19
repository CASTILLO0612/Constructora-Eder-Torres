using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using System.ComponentModel.DataAnnotations;

namespace ConstructoraEderTorres.Aplicacion.Contratos.Obras;

public sealed record SolicitudCrearObra(
    string Codigo,
    string Nombre,
    string? Descripcion,
    string Direccion,
    DateOnly FechaInicioPlan,
    DateOnly FechaFinPlan,
    string? EstadoInicial = null,
    [property: Required] decimal? Latitud = null,
    [property: Required] decimal? Longitud = null,
    [property: Required] decimal? RadioAsistenciaMetros = null);

public sealed record SolicitudActualizarObra(
    string Nombre,
    string? Descripcion,
    string Direccion,
    DateOnly FechaInicioPlan,
    DateOnly FechaFinPlan,
    DateOnly? FechaInicioReal,
    DateOnly? FechaFinReal,
    [property: Required] decimal? Latitud = null,
    [property: Required] decimal? Longitud = null,
    [property: Required] decimal? RadioAsistenciaMetros = null);

public sealed record SolicitudCambiarEstadoObra(
    string Estado,
    DateOnly? FechaInicioReal = null,
    DateOnly? FechaFinReal = null);

public sealed record RespuestaObra(
    int IdObra,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string Direccion,
    DateOnly FechaInicioPlan,
    DateOnly FechaFinPlan,
    DateOnly? FechaInicioReal,
    DateOnly? FechaFinReal,
    string Estado,
    DateTime CreadoEn,
    decimal? Latitud,
    decimal? Longitud,
    decimal? RadioAsistenciaMetros);

public interface IPuertoObras
{
    Task<bool> ExisteCodigoObraAsync(
        string codigo,
        int? excluirIdObra = null,
        CancellationToken cancelacion = default);

    Task<RespuestaObra> CrearObraAsync(
        SolicitudCrearObra solicitud,
        string estadoInicial,
        DateTime creadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaObra?> ObtenerObraPorIdAsync(
        int idObra,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<RespuestaObra>> ListarObrasAsync(
        string? estado,
        IReadOnlySet<int>? idsObrasPermitidas,
        CancellationToken cancelacion = default);

    Task<RespuestaObra?> ActualizarObraAsync(
        int idObra,
        SolicitudActualizarObra solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);

    Task<RespuestaObra?> CambiarEstadoObraAsync(
        int idObra,
        string nuevoEstado,
        DateOnly? fechaInicioReal,
        DateOnly? fechaFinReal,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default);
}
