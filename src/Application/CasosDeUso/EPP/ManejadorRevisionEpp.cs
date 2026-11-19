using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.EPP;

public sealed partial class ManejadorEpp
{
    public async Task<Resultado<RespuestaEpp>> RevisarEppAsync(int idEpp, SolicitudRevisarEpp solicitud,
        CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioEpp);
        if (!permiso.EsExitoso) return Resultado<RespuestaEpp>.Fallo(permiso.Error!);
        var equipo = await puertoEpp.ObtenerEppPorIdAsync(idEpp, cancelacion);
        if (equipo is null) return Resultado<RespuestaEpp>.Fallo(ErrorNegocio.NoEncontrado("epp_no_encontrado", "El EPP no existe."));
        if (equipo.Estado != EstadosEpp.EnRevision) return Resultado<RespuestaEpp>.Fallo(
            ErrorNegocio.Conflicto("epp_no_revision", "Solo se puede dictaminar un EPP en revisión."));
        var condicion = solicitud.Condicion?.Trim().ToUpperInvariant() ?? "";
        if (!CondicionesEpp.Todas.Contains(condicion) || (!solicitud.Retirar && condicion == CondicionesEpp.Danado)
            || string.IsNullOrWhiteSpace(solicitud.Motivo) || solicitud.Motivo.Length > 500)
            return Resultado<RespuestaEpp>.Fallo(ErrorNegocio.Validacion("revision_epp_invalida",
                "Indique condición y motivo (hasta 500 caracteres). Un equipo dañado no puede volver a disponible."));
        var estado = solicitud.Retirar ? EstadosEpp.Retirado : EstadosEpp.Disponible;
        var actor = permiso.Valor!;
        var revisado = await puertoEpp.RevisarEppAsync(idEpp, condicion, estado, actor.IdUsuario, solicitud.Motivo.Trim(),
            new RegistroEventoAuditoria(actor.IdUsuario, "epps", idEpp.ToString(), AccionesAuditoria.Modificar,
                "revision", equipo.Estado, estado, actor.IpOrigen), cancelacion);
        return Resultado<RespuestaEpp>.Exito(revisado!);
    }
}
