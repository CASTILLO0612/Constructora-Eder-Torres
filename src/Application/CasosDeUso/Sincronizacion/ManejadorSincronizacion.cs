using ConstructoraEderTorres.Aplicacion.CasosDeUso.Asistencia;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.EPP;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Herramientas;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.Extensions.Logging;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Sincronizacion;

public sealed class ManejadorSincronizacion(
    ManejadorAsistencia asistencia,
    ManejadorSeguimientoObra seguimiento,
    ManejadorEpp epp,
    ManejadorHerramientas herramientas,
    IContextoUsuarioActual contextoUsuario,
    IUnidadTrabajo unidadTrabajo,
    OpcionesSincronizacion opciones,
    IRelojSistema reloj,
    ILogger<ManejadorSincronizacion> registro)
{
    public async Task<Resultado<RespuestaSincronizacionLote>> ProcesarLoteOfflineAsync(
        SolicitudSincronizacionLote solicitud, CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.OperarSincronizacion);
        if (!permiso.EsExitoso) return Resultado<RespuestaSincronizacionLote>.Fallo(permiso.Error!);
        var total = (solicitud.Asistencias?.Count ?? 0) + (solicitud.Avances?.Count ?? 0)
            + (solicitud.Evidencias?.Count ?? 0) + (solicitud.IncidenciasObra?.Count ?? 0)
            + (solicitud.EntregasEpp?.Count ?? 0) + (solicitud.ReposicionesEpp?.Count ?? 0)
            + (solicitud.AsignacionesHerramienta?.Count ?? 0) + (solicitud.EventosHerramienta?.Count ?? 0)
            + (solicitud.SalidasAsistencia?.Count ?? 0) + (solicitud.DevolucionesEpp?.Count ?? 0);
        if (total == 0 || total > opciones.MaximoOperaciones)
            return Resultado<RespuestaSincronizacionLote>.Fallo(ErrorNegocio.Validacion(
                "tamano_lote_invalido", $"El lote admite entre 1 y {opciones.MaximoOperaciones} operaciones."));

        var acuses = new List<AcuseOperacionSincronizacion>(total);
        await ProcesarGrupoAsync(solicitud.Asistencias, TiposOperacionSincronizacion.Asistencia,
            (s, ct) => asistencia.RegistrarAsistenciaAsync(s with { EsSincronizacionDiferida = true }, ct),
            r => r.IdAsistencia.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.SalidasAsistencia, TiposOperacionSincronizacion.SalidaAsistencia,
            asistencia.SincronizarSalidaAsync,
            r => r.IdAsistencia.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.Avances, TiposOperacionSincronizacion.Avance,
            (s, ct) => seguimiento.RegistrarAvanceAsync(s with { EsSincronizacionDiferida = true }, ct),
            r => r.IdAvance.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.Evidencias, TiposOperacionSincronizacion.Evidencia,
            (s, ct) => seguimiento.RegistrarEvidenciaAsync(s with { EsSincronizacionDiferida = true }, ct),
            r => r.IdEvidencia.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.IncidenciasObra, TiposOperacionSincronizacion.IncidenciaObra,
            (s, ct) => seguimiento.RegistrarIncidenciaObraAsync(s with { EsSincronizacionDiferida = true }, ct),
            r => r.IdIncidencia.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.EntregasEpp, TiposOperacionSincronizacion.EntregaEpp,
            (s, ct) => epp.RegistrarEntregaAsync(s with { EsSincronizacionDiferida = true }, ct),
            r => r.IdEntregaEpp.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.DevolucionesEpp, TiposOperacionSincronizacion.DevolucionEpp,
            (s, ct) => epp.DevolverEppAsync(s.IdEntregaEpp,
                new(s.DevueltoEn, s.CondicionDevolucion, s.Observacion, s.UuidRegistro), ct),
            r => r.IdEntregaEpp.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.ReposicionesEpp, TiposOperacionSincronizacion.ReposicionEpp,
            (s, ct) => epp.RegistrarReposicionAsync(s with { EsSincronizacionDiferida = true }, ct),
            r => r.IdReposicionEpp.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.AsignacionesHerramienta, TiposOperacionSincronizacion.AsignacionHerramienta,
            (s, ct) => herramientas.AsignarHerramientaAsync(s with { EsSincronizacionDiferida = true }, ct),
            r => r.IdAsignacionHerramienta.ToString(), acuses, cancelacion);
        await ProcesarGrupoAsync(solicitud.EventosHerramienta, TiposOperacionSincronizacion.EventoHerramienta,
            herramientas.RegistrarDevolucionOIncidenciaAsync,
            r => r.IdIncidenciaHerramienta.ToString(), acuses, cancelacion);

        var confirmadas = acuses.Count(a => a.EstadoAcuse == EstadosAcuseSincronizacion.Sincronizado);
        foreach (var acuse in acuses)
            DiagnosticoAplicacion.OperacionesSincronizacion.Add(1,
                new KeyValuePair<string, object?>("tipo", acuse.TipoOperacion),
                new KeyValuePair<string, object?>("resultado", acuse.EstadoAcuse));
        var idempotentes = acuses.Count(a => a.EstadoAcuse == EstadosAcuseSincronizacion.IdempotentePrevio);
        return Resultado<RespuestaSincronizacionLote>.Exito(new(reloj.AhoraUtc, total,
            confirmadas, idempotentes, total - confirmadas - idempotentes, acuses));
    }

    private async Task ProcesarGrupoAsync<TSolicitud, TRespuesta>(
        IReadOnlyList<TSolicitud>? solicitudes, string tipo,
        Func<TSolicitud, CancellationToken, Task<Resultado<TRespuesta>>> procesar,
        Func<TRespuesta, string> obtenerId, List<AcuseOperacionSincronizacion> acuses, CancellationToken cancelacion)
        where TSolicitud : ISolicitudSincronizable
        where TRespuesta : IRespuestaIdempotente
    {
        foreach (var solicitud in solicitudes ?? [])
        {
            cancelacion.ThrowIfCancellationRequested();
            var uuid = solicitud?.UuidRegistro ?? Guid.Empty;
            if (uuid == Guid.Empty)
            {
                acuses.Add(CrearError(uuid, tipo, ErrorNegocio.Validacion("uuid_registro_obligatorio",
                    "Toda operación offline requiere un UUID generado antes de su captura.")));
                continue;
            }
            var errores = ValidadorContratoSolicitud.Validar(solicitud!, opciones.MaximoOperaciones);
            if (errores.Count > 0)
            {
                acuses.Add(CrearError(uuid, tipo, ErrorNegocio.Validacion("contrato_operacion_invalido",
                    string.Join(" ", errores.Select(e => e.Key + ": " + string.Join(" ", e.Value))))));
                continue;
            }
            try
            {
                var resultado = await unidadTrabajo.EjecutarAsync(ct => procesar(solicitud!, ct),
                    r => r.EsExitoso, cancelacion,
                    tipo is TiposOperacionSincronizacion.Asistencia or TiposOperacionSincronizacion.SalidaAsistencia
                        ? AislamientoOperacion.LecturaConfirmada : AislamientoOperacion.Serializable);
                acuses.Add(resultado.EsExitoso
                    ? new(uuid, tipo, resultado.Valor!.FueIdempotente
                        ? EstadosAcuseSincronizacion.IdempotentePrevio : EstadosAcuseSincronizacion.Sincronizado,
                        obtenerId(resultado.Valor), reloj.AhoraUtc)
                    : CrearError(uuid, tipo, resultado.Error!));
            }
            catch (ExcepcionPersistencia error)
            {
                acuses.Add(CrearError(uuid, tipo, error.Error));
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                registro.LogError(error, "Falló la sincronización {Tipo} con UUID {Uuid}.", tipo, uuid);
                acuses.Add(CrearError(uuid, tipo, new("operacion_no_disponible",
                    "No se pudo confirmar esta operación. Consérvela en la cola y reintente.", TipoError.NoDisponible)));
            }
        }
    }

    private static AcuseOperacionSincronizacion CrearError(Guid uuid, string tipo, ErrorNegocio error) =>
        new(uuid, tipo, error.Tipo switch
        {
            TipoError.Prohibido or TipoError.NoAutenticado => EstadosAcuseSincronizacion.RechazadoAutorizacion,
            TipoError.Conflicto => EstadosAcuseSincronizacion.RechazadoConflicto,
            TipoError.NoDisponible => EstadosAcuseSincronizacion.PendienteReintento,
            _ => EstadosAcuseSincronizacion.RechazadoValidacion
        }, null, null, error.Codigo, error.Mensaje);
}
