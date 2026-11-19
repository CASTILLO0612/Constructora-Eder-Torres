using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.SeguimientoObra;

public sealed partial class ManejadorSeguimientoObra
{
    public async Task<Resultado<RespuestaActa>> ActualizarActaSemanalAsync(
        int idActa, SolicitudActualizarActa solicitud, CancellationToken cancelacion = default)
    {
        var consulta = await AutorizarEdicionActaAsync(idActa, cancelacion);
        if (!consulta.EsExitoso) return consulta;
        var existente = consulta.Valor!;
        if (existente.Estado is not (EstadosActa.Borrador or EstadosActa.Rechazada))
            return Resultado<RespuestaActa>.Fallo(ErrorNegocio.Conflicto("acta_no_editable",
                "Solo puede corregir actas en borrador o rechazadas. Las actas enviadas o aprobadas conservan su contenido."));
        var contenido = new SolicitudElaborarActa(existente.IdObra, existente.NumeroActa,
            solicitud.PeriodoInicio, solicitud.PeriodoFin, solicitud.Observaciones, solicitud.MejorasPropuestas,
            solicitud.SolicitudesCambio, IdsAvances: solicitud.IdsAvances,
            IdsIncidencias: solicitud.IdsIncidencias, Participantes: solicitud.Participantes);
        var validacion = await ValidarContenidoActaAsync(contenido, cancelacion);
        if (!validacion.EsExitoso) return Resultado<RespuestaActa>.Fallo(validacion.Error!);
        var usuario = contextoUsuario.ObtenerUsuario()!;
        var actualizada = await puertoSeguimiento.ActualizarActaAsync(idActa, solicitud,
            new RegistroEventoAuditoria(usuario.IdUsuario, "actas", idActa.ToString(),
                AccionesAuditoria.Modificar, "estado", existente.Estado, EstadosActa.Borrador, usuario.IpOrigen),
            cancelacion);
        return Resultado<RespuestaActa>.Exito(actualizada!);
    }

    public async Task<Resultado<RespuestaActa>> EnviarActaARevisionAsync(
        int idActa, CancellationToken cancelacion = default)
    {
        var consulta = await AutorizarEdicionActaAsync(idActa, cancelacion);
        if (!consulta.EsExitoso) return consulta;
        var acta = consulta.Valor!;
        if (acta.Estado != EstadosActa.Borrador)
            return Resultado<RespuestaActa>.Fallo(ErrorNegocio.Conflicto("acta_no_borrador",
                "El acta debe estar en borrador antes de enviarla a revisión."));
        if (!acta.AvancesConsolidados.Any(a => a.Estado == EstadosAvance.Registrado))
            return Resultado<RespuestaActa>.Fallo(ErrorNegocio.Conflicto("acta_sin_avances",
                "El período del acta debe contener al menos un avance registrado."));
        var usuario = contextoUsuario.ObtenerUsuario()!;
        var enviada = await puertoSeguimiento.EnviarActaARevisionAsync(idActa,
            new RegistroEventoAuditoria(usuario.IdUsuario, "actas", idActa.ToString(),
                AccionesAuditoria.Modificar, "estado", EstadosActa.Borrador, EstadosActa.Pendiente, usuario.IpOrigen),
            cancelacion);
        return Resultado<RespuestaActa>.Exito(enviada!);
    }

    private async Task<Resultado<RespuestaActa>> AutorizarEdicionActaAsync(int idActa, CancellationToken cancelacion)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ElaborarActas);
        if (!permiso.EsExitoso) return Resultado<RespuestaActa>.Fallo(permiso.Error!);
        var acta = await puertoSeguimiento.ObtenerActaPorIdAsync(idActa, cancelacion);
        if (acta is null) return Resultado<RespuestaActa>.Fallo(
            ErrorNegocio.NoEncontrado("acta_no_encontrada", "El acta semanal no existe."));
        if (!permiso.Valor!.TieneAccesoAObra(acta.IdObra)) return Resultado<RespuestaActa>.Fallo(
            ErrorNegocio.Prohibido("obra_fuera_de_alcance", "No tiene autorización para elaborar actas de esta obra."));
        return Resultado<RespuestaActa>.Exito(acta);
    }

    private async Task<Resultado> ValidarContenidoActaAsync(
        SolicitudElaborarActa solicitud, CancellationToken cancelacion)
    {
        if (solicitud.PeriodoFin < solicitud.PeriodoInicio)
            return Resultado.Fallo(ErrorNegocio.Validacion("periodo_acta_invalido",
                "La fecha de fin del período no puede ser anterior al inicio."));
        foreach (var id in solicitud.IdsAvances?.Distinct() ?? [])
        {
            var avance = await puertoSeguimiento.ObtenerAvancePorIdAsync(id, cancelacion);
            if (avance is null || avance.IdObra != solicitud.IdObra)
                return Resultado.Fallo(ErrorNegocio.Conflicto("avance_acta_otra_obra",
                    "Todos los avances deben pertenecer a la obra del acta."));
            if (avance.FechaAvance < solicitud.PeriodoInicio || avance.FechaAvance > solicitud.PeriodoFin
                || avance.Estado != EstadosAvance.Registrado)
                return Resultado.Fallo(ErrorNegocio.Conflicto("avance_fuera_periodo_acta",
                    "El acta solo puede consolidar avances registrados dentro del período seleccionado."));
        }
        foreach (var id in solicitud.IdsIncidencias?.Distinct() ?? [])
        {
            var incidencia = await puertoSeguimiento.ObtenerIncidenciaPorIdAsync(id, cancelacion);
            if (incidencia is null || incidencia.IdObra != solicitud.IdObra)
                return Resultado.Fallo(ErrorNegocio.Conflicto("incidencia_acta_otra_obra",
                    "Todas las incidencias deben pertenecer a la obra del acta."));
        }
        foreach (var participante in solicitud.Participantes ?? [])
        {
            if (string.IsNullOrWhiteSpace(participante.TipoParticipacion) || participante.TipoParticipacion.Length > 30)
                return Resultado.Fallo(ErrorNegocio.Validacion("participacion_invalida",
                    "Indique el tipo de participación, con un máximo de 30 caracteres."));
            if (await puertoSeguimiento.ObtenerIdObraDeAsignacionAsync(participante.IdAsignacion, cancelacion) != solicitud.IdObra)
                return Resultado.Fallo(ErrorNegocio.Conflicto("participante_acta_otra_obra",
                    "Todos los participantes deben pertenecer a la obra del acta."));
        }
        return Resultado.Exito();
    }
}
