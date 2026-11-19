using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Herramientas;
using ConstructoraEderTorres.Dominio.Reglas;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed partial class AdaptadorHerramientasPostgreSql
{
    public Task<bool> TieneIncidenciasPendientesAsync(int idHerramienta, CancellationToken cancelacion = default) =>
        (from i in contexto.IncidenciasHerramienta
         join a in contexto.AsignacionesHerramienta on i.IdAsignacionHerramienta equals a.IdAsignacionHerramienta
         where a.IdHerramienta == idHerramienta
             && (i.Estado == EstadosIncidenciaHerramienta.Abierta || i.Estado == EstadosIncidenciaHerramienta.EnProceso)
         select i).AnyAsync(cancelacion);

    public async Task<RespuestaHerramienta?> ActualizarHerramientaAsync(int idHerramienta,
        SolicitudActualizarHerramienta solicitud, RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default)
    {
        var equipo = await contexto.Herramientas.SingleOrDefaultAsync(h => h.IdHerramienta == idHerramienta, cancelacion);
        if (equipo is null) return null;
        equipo.NumeroSerie = solicitud.NumeroSerie;
        equipo.Marca = solicitud.Marca;
        equipo.Modelo = solicitud.Modelo;
        equipo.FechaAdquisicion = solicitud.FechaAdquisicion;
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
        return await ObtenerHerramientaPorIdAsync(idHerramienta, cancelacion);
    }

    public async Task<RespuestaHerramienta?> RevisarHerramientaAsync(int idHerramienta, string condicion, string estado,
        RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default)
    {
        var equipo = await contexto.Herramientas.SingleOrDefaultAsync(h => h.IdHerramienta == idHerramienta, cancelacion);
        if (equipo is null) return null;
        equipo.Condicion = condicion;
        equipo.Estado = estado;
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
        return await ObtenerHerramientaPorIdAsync(idHerramienta, cancelacion);
    }

    public async Task<RespuestaIncidenciaHerramienta?> AtenderIncidenciaAsync(long idIncidencia, string estado,
        string resolucion, decimal? costo, RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default)
    {
        var incidencia = await contexto.IncidenciasHerramienta.SingleOrDefaultAsync(i => i.IdIncidenciaHerramienta == idIncidencia, cancelacion);
        if (incidencia is null) return null;
        incidencia.Estado = estado;
        incidencia.Resolucion = resolucion;
        incidencia.CostoEstimado = costo ?? incidencia.CostoEstimado;
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
        return await ObtenerIncidenciaPorIdAsync(idIncidencia, cancelacion);
    }
}
