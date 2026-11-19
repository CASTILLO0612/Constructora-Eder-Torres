using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed partial class AdaptadorSeguimientoObraPostgreSql
{
    public async Task<RespuestaActa?> ActualizarActaAsync(int idActa, SolicitudActualizarActa solicitud,
        RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default)
    {
        var acta = await contexto.Actas.SingleOrDefaultAsync(a => a.IdActa == idActa, cancelacion);
        if (acta is null) return null;
        acta.PeriodoInicio = solicitud.PeriodoInicio;
        acta.PeriodoFin = solicitud.PeriodoFin;
        acta.Observaciones = solicitud.Observaciones?.Trim();
        acta.MejorasPropuestas = solicitud.MejorasPropuestas?.Trim();
        acta.SolicitudesCambio = solicitud.SolicitudesCambio?.Trim();
        acta.Estado = EstadosActa.Borrador;

        // Las relaciones se reemplazan dentro de la transacción de la operación; las decisiones anteriores se conservan.
        contexto.ActasAvance.RemoveRange(await contexto.ActasAvance.Where(x => x.IdActa == idActa).ToListAsync(cancelacion));
        contexto.ActasIncidencia.RemoveRange(await contexto.ActasIncidencia.Where(x => x.IdActa == idActa).ToListAsync(cancelacion));
        contexto.TrabajadoresActa.RemoveRange(await contexto.TrabajadoresActa.Where(x => x.IdActa == idActa).ToListAsync(cancelacion));
        await contexto.SaveChangesAsync(cancelacion);

        var avances = solicitud.IdsAvances is { Count: > 0 } ? solicitud.IdsAvances.Distinct().ToArray()
            : await (from av in contexto.Avances.AsNoTracking()
                     join actividad in contexto.Actividades.AsNoTracking() on av.IdActividad equals actividad.IdActividad
                     where actividad.IdObra == acta.IdObra && av.Estado == EstadosAvance.Registrado
                         && av.FechaAvance >= acta.PeriodoInicio && av.FechaAvance <= acta.PeriodoFin
                     select av.IdAvance).ToArrayAsync(cancelacion);
        var incidencias = solicitud.IdsIncidencias is { Count: > 0 } ? solicitud.IdsIncidencias.Distinct().ToArray()
            : await contexto.Incidencias.AsNoTracking().Where(i => i.IdObra == acta.IdObra
                && DateOnly.FromDateTime(i.InicioEn) >= acta.PeriodoInicio
                && DateOnly.FromDateTime(i.InicioEn) <= acta.PeriodoFin
                && i.Estado != EstadosIncidencia.Anulada).Select(i => i.IdIncidencia).ToArrayAsync(cancelacion);
        contexto.ActasAvance.AddRange(avances.Select(id => new ActaAvance { IdActa = idActa, IdAvance = id }));
        contexto.ActasIncidencia.AddRange(incidencias.Select(id => new ActaIncidencia { IdActa = idActa, IdIncidencia = id }));
        contexto.TrabajadoresActa.AddRange((solicitud.Participantes ?? []).DistinctBy(p => p.IdAsignacion)
            .Select(p => new TrabajadorActa { IdActa = idActa, IdAsignacion = p.IdAsignacion,
                TipoParticipacion = p.TipoParticipacion.Trim().ToUpperInvariant(), RutaFirma = p.RutaFirma, FirmadoEn = p.FirmadoEn }));
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
        return await ObtenerActaPorIdAsync(idActa, cancelacion);
    }
}
