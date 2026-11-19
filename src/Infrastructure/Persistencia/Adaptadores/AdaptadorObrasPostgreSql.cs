using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed class AdaptadorObrasPostgreSql(
    ContextoConstructora contexto,
    IRelojSistema reloj,
    PaginacionConsulta paginacion) : IPuertoObras
{
    public Task<bool> ExisteCodigoObraAsync(
        string codigo,
        int? excluirIdObra = null,
        CancellationToken cancelacion = default) =>
        contexto.Obras.AnyAsync(
            o => o.Codigo.ToLower() == codigo.ToLower()
                 && (excluirIdObra == null || o.IdObra != excluirIdObra.Value),
            cancelacion);

    public async Task<RespuestaObra> CrearObraAsync(
        SolicitudCrearObra solicitud,
        string estadoInicial,
        DateTime creadoEnUtc,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var obra = new Obra
        {
            Codigo = solicitud.Codigo,
            Nombre = solicitud.Nombre,
            Descripcion = solicitud.Descripcion,
            Direccion = solicitud.Direccion,
            Latitud = solicitud.Latitud,
            Longitud = solicitud.Longitud,
            RadioAsistenciaMetros = solicitud.RadioAsistenciaMetros,
            FechaInicioPlan = solicitud.FechaInicioPlan,
            FechaFinPlan = solicitud.FechaFinPlan,
            Estado = estadoInicial,
            CreadoEn = creadoEnUtc
        };

        contexto.Obras.Add(obra);
        await contexto.SaveChangesAsync(cancelacion);

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(
            auditoria with { IdRegistro = obra.IdObra.ToString() },
            creadoEnUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return Mapear(obra);
    }

    public async Task<RespuestaObra?> ObtenerObraPorIdAsync(
        int idObra,
        CancellationToken cancelacion = default)
    {
        var obra = await contexto.Obras.AsNoTracking()
            .FirstOrDefaultAsync(o => o.IdObra == idObra, cancelacion);
        return obra is null ? null : Mapear(obra);
    }

    public async Task<IReadOnlyList<RespuestaObra>> ListarObrasAsync(
        string? estado,
        IReadOnlySet<int>? idsObrasPermitidas,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Obras.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estado))
        {
            consulta = consulta.Where(o => o.Estado == estado);
        }

        if (idsObrasPermitidas is not null)
        {
            var ids = idsObrasPermitidas.ToArray();
            consulta = consulta.Where(o => ids.Contains(o.IdObra));
        }

        var obras = await consulta
            .OrderBy(o => o.IdObra)
            .Paginar(paginacion).ToListAsync(cancelacion);

        return obras.Select(Mapear).ToList();
    }

    public async Task<RespuestaObra?> ActualizarObraAsync(
        int idObra,
        SolicitudActualizarObra solicitud,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var obra = await contexto.Obras.FirstOrDefaultAsync(o => o.IdObra == idObra, cancelacion);
        if (obra is null)
        {
            return null;
        }

        var geocercaAnterior = new { obra.Latitud, obra.Longitud, obra.RadioAsistenciaMetros };
        obra.Nombre = solicitud.Nombre;
        obra.Descripcion = solicitud.Descripcion;
        obra.Direccion = solicitud.Direccion;
        obra.Latitud = solicitud.Latitud;
        obra.Longitud = solicitud.Longitud;
        obra.RadioAsistenciaMetros = solicitud.RadioAsistenciaMetros;

        var geocercaNueva = new { obra.Latitud, obra.Longitud, obra.RadioAsistenciaMetros };
        if (!geocercaAnterior.Equals(geocercaNueva))
        {
            contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria with
            {
                Campo = "geocerca",
                ValorAnterior = JsonSerializer.Serialize(geocercaAnterior),
                ValorNuevo = JsonSerializer.Serialize(geocercaNueva)
            }, reloj.AhoraUtc));
        }
        obra.FechaInicioPlan = solicitud.FechaInicioPlan;
        obra.FechaFinPlan = solicitud.FechaFinPlan;
        obra.FechaInicioReal = solicitud.FechaInicioReal;
        obra.FechaFinReal = solicitud.FechaFinReal;

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return Mapear(obra);
    }

    public async Task<RespuestaObra?> CambiarEstadoObraAsync(
        int idObra,
        string nuevoEstado,
        DateOnly? fechaInicioReal,
        DateOnly? fechaFinReal,
        RegistroEventoAuditoria auditoria,
        CancellationToken cancelacion = default)
    {
        var obra = await contexto.Obras.FirstOrDefaultAsync(o => o.IdObra == idObra, cancelacion);
        if (obra is null)
        {
            return null;
        }

        obra.Estado = nuevoEstado;
        obra.FechaInicioReal = fechaInicioReal;
        obra.FechaFinReal = fechaFinReal;

        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);

        return Mapear(obra);
    }

    private static RespuestaObra Mapear(Obra o) =>
        new(
            o.IdObra,
            o.Codigo,
            o.Nombre,
            o.Descripcion,
            o.Direccion,
            o.FechaInicioPlan,
            o.FechaFinPlan,
            o.FechaInicioReal,
            o.FechaFinReal,
            o.Estado,
            o.CreadoEn,
            o.Latitud,
            o.Longitud,
            o.RadioAsistenciaMetros);
}
