using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Adaptadores;

internal static class FabricaAuditoria
{
    public static Dominio.Seguridad.Auditoria CrearEntidad(
        RegistroEventoAuditoria evento,
        DateTime fechaHoraUtc) =>
        new()
        {
            IdUsuario = evento.IdUsuario,
            Entidad = evento.Entidad,
            IdRegistro = evento.IdRegistro.Length > 50 ? evento.IdRegistro[..50] : evento.IdRegistro,
            Accion = evento.Accion,
            Campo = evento.Campo,
            ValorAnterior = Recortar(evento.ValorAnterior, 1000),
            ValorNuevo = Recortar(evento.ValorNuevo, 1000),
            FechaHora = fechaHoraUtc,
            IpOrigen = evento.IpOrigen,
            UuidOperacion = evento.UuidOperacion is { } g && g != Guid.Empty ? g : Guid.NewGuid()
        };

    private static string? Recortar(string? valor, int longitudMaxima) =>
        valor is null || valor.Length <= longitudMaxima
            ? valor
            : valor[..longitudMaxima];
}

public sealed class ServicioAuditoriaPostgreSql(
    ContextoConstructora contexto,
    IRelojSistema reloj) : IServicioAuditoria
{
    public async Task RegistrarEventoAsync(
        RegistroEventoAuditoria evento,
        CancellationToken cancelacion = default)
    {
        contexto.Auditorias.Add(FabricaAuditoria.CrearEntidad(evento, reloj.AhoraUtc));
        await contexto.SaveChangesAsync(cancelacion);
    }
}
