using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.EPP;
using ConstructoraEderTorres.Dominio.Reglas;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

public sealed partial class AdaptadorEppPostgreSql
{
    public async Task<RespuestaEpp?> RevisarEppAsync(int idEpp, string condicion, string estado, int idUsuario,
        string motivo, RegistroEventoAuditoria auditoria, CancellationToken cancelacion = default)
    {
        var equipo = await contexto.EquiposProteccionPersonal.SingleOrDefaultAsync(e => e.IdEpp == idEpp, cancelacion);
        if (equipo is null) return null;
        equipo.Condicion = condicion;
        equipo.Estado = estado;
        contexto.MovimientosExistenciasEpp.Add(new MovimientoExistenciasEpp
        {
            IdEpp = idEpp, TipoMovimiento = estado == EstadosEpp.Retirado ? TiposMovimientoEpp.Retiro : TiposMovimientoEpp.Ajuste,
            FechaHora = reloj.AhoraUtc, IdUsuario = idUsuario, Observacion = motivo, UuidRegistro = Guid.NewGuid()
        });
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(auditoria, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
        return await ObtenerEppPorIdAsync(idEpp, cancelacion);
    }
}
