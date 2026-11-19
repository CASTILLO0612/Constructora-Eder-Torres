using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace ConstructoraEderTorres.Infraestructura.Identidad;

public sealed class AlmacenSesionesPostgreSql(ContextoConstructora contexto, IUnidadTrabajo unidadTrabajo) : IPuertoSesiones
{
    public async Task<Guid> CrearAsync(int idUsuario, DateTime emitidaUtc, DateTime expiraUtc, CancellationToken cancelacion = default)
    {
        var sesion = new SesionUsuario
        {
            IdSesion = Guid.NewGuid(),
            IdUsuario = idUsuario,
            EmitidaEnUtc = emitidaUtc,
            ExpiraEnUtc = expiraUtc
        };
        contexto.SesionesUsuario.Add(sesion);
        await contexto.SaveChangesAsync(cancelacion);
        return sesion.IdSesion;
    }

    public async Task RevocarAsync(Guid idSesion, int idUsuario, DateTime ahoraUtc, CancellationToken cancelacion = default)
    {
        await unidadTrabajo.EjecutarAsync(async token =>
        {
            var actualizadas = await contexto.SesionesUsuario
                .Where(s => s.IdSesion == idSesion && s.IdUsuario == idUsuario && s.RevocadaEnUtc == null)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(s => s.RevocadaEnUtc, ahoraUtc), token);
            if (actualizadas > 0)
            {
                contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(new RegistroEventoAuditoria(idUsuario,
                    "sesiones_usuario", idSesion.ToString(), AccionesAuditoria.Anular, "revocada_en_utc", null,
                    ahoraUtc.ToString("O")), ahoraUtc));
                await contexto.SaveChangesAsync(token);
            }
            return true;
        }, resultado => resultado, cancelacion);
    }
}
