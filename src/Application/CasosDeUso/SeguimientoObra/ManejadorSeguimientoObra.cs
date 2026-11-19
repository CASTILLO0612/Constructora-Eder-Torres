using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Dominio.SeguimientoObra;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.SeguimientoObra;

public sealed partial class ManejadorSeguimientoObra(
    IPuertoSeguimientoObra puertoSeguimiento,
    IAlmacenamientoEvidencias almacenamientoEvidencias,
    IContextoUsuarioActual contextoUsuario,
    IRelojSistema reloj,
    IUnidadTrabajo unidadTrabajo,
    ICalendarioLaboral calendario,
    PoliticaOperacionesObra politicaObra)
{
    public async Task<Resultado<RespuestaActividad>> CrearActividadAsync(
        SolicitudCrearActividad solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.GestionarActividades,
            solicitud.IdObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaActividad>.Fallo(autorizacion.Error!);
        }

        var codigo = solicitud.Codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        var unidad = solicitud.UnidadMedida?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 30
            || string.IsNullOrWhiteSpace(nombre) || nombre.Length > 120
            || string.IsNullOrWhiteSpace(unidad) || unidad.Length > 20)
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.Validacion("datos_actividad_invalidos", "El código, nombre y unidad de medida de la actividad son obligatorios."));
        }

        if (solicitud.CantidadPlanificada < 0m)
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.Validacion("cantidad_planificada_invalida", "La cantidad planificada no puede ser negativa."));
        }

        if (solicitud.FechaFinPlan < solicitud.FechaInicioPlan)
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.Validacion("fechas_actividad_invalidas", "La fecha fin planificada no puede ser anterior a la fecha de inicio."));
        }

        var estadoInicial = string.IsNullOrWhiteSpace(solicitud.EstadoInicial)
            ? EstadosActividad.Planificada
            : solicitud.EstadoInicial.Trim().ToUpperInvariant();
        if (!EstadosActividad.Todos.Contains(estadoInicial))
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.Validacion("estado_actividad_invalido", "El estado de actividad no es válido."));
        }

        if (estadoInicial != EstadosActividad.Planificada)
            return Resultado<RespuestaActividad>.Fallo(ErrorNegocio.Validacion("estado_inicial_actividad_invalido",
                "Una actividad nueva debe comenzar PLANIFICADA."));

        var obraPlanificable = await politicaObra.ExigirPlanificableAsync(solicitud.IdObra, cancelacion);
        if (!obraPlanificable.EsExitoso) return Resultado<RespuestaActividad>.Fallo(obraPlanificable.Error!);

        if (!await puertoSeguimiento.ExisteObraAsync(solicitud.IdObra, cancelacion))
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra indicada no existe."));
        }

        if (await puertoSeguimiento.ExisteCodigoActividadEnObraAsync(solicitud.IdObra, codigo, null, cancelacion))
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.Conflicto("codigo_actividad_duplicado", "Ya existe una actividad con ese código en la obra."));
        }

        // CRQ-0003: Responsable debe pertenecer a la misma obra.
        if (solicitud.IdAsignacionResponsable is int idResp)
        {
            var idObraResp = await puertoSeguimiento.ObtenerIdObraDeAsignacionAsync(idResp, cancelacion);
            if (idObraResp is null)
            {
                return Resultado<RespuestaActividad>.Fallo(
                    ErrorNegocio.NoEncontrado("responsable_no_encontrado", "La asignación del responsable no existe."));
            }

            if (idObraResp.Value != solicitud.IdObra)
            {
                return Resultado<RespuestaActividad>.Fallo(
                    ErrorNegocio.Conflicto("responsable_otra_obra", "El responsable de la actividad debe pertenecer a la misma obra."));
            }
        }

        // CRQ-0003: Participantes deben pertenecer a la misma obra.
        if (solicitud.Participantes is { Count: > 0 })
        {
            foreach (var part in solicitud.Participantes)
            {
                var idObraPart = await puertoSeguimiento.ObtenerIdObraDeAsignacionAsync(part.IdAsignacion, cancelacion);
                if (idObraPart != solicitud.IdObra)
                {
                    return Resultado<RespuestaActividad>.Fallo(
                        ErrorNegocio.Conflicto(
                            "participante_otra_obra",
                            $"La asignación {part.IdAsignacion} no pertenece a la obra de la actividad."));
                }
            }
        }

        var actor = autorizacion.Valor!;
        var creada = await puertoSeguimiento.CrearActividadAsync(
            solicitud with
            {
                Codigo = codigo,
                Nombre = nombre,
                Descripcion = solicitud.Descripcion?.Trim(),
                UnidadMedida = unidad
            },
            estadoInicial,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "actividades",
                $"{solicitud.IdObra}:{codigo}",
                AccionesAuditoria.Crear,
                "estado",
                null,
                estadoInicial,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaActividad>.Exito(creada);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaActividad>>> ListarActividadesPorObraAsync(
        int idObra,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ConsultarObras,
            idObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaActividad>>.Fallo(autorizacion.Error!);
        }

        return Resultado<IReadOnlyList<RespuestaActividad>>.Exito(
            await puertoSeguimiento.ListarActividadesPorObraAsync(idObra, cancelacion));
    }

    public async Task<Resultado<RespuestaActividad>> ActualizarActividadAsync(
        int idActividad,
        SolicitudActualizarActividad solicitud,
        CancellationToken cancelacion = default)
    {
        var existente = await puertoSeguimiento.ObtenerActividadPorIdAsync(idActividad, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.NoEncontrado("actividad_no_encontrada", "La actividad solicitada no existe."));
        }

        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.GestionarActividades,
            existente.IdObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaActividad>.Fallo(autorizacion.Error!);
        }

        var nuevoEstado = solicitud.Estado?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!EstadosActividad.Todos.Contains(nuevoEstado))
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.Validacion("estado_actividad_invalido", "El estado de actividad indicado no es válido."));
        }

        if (!PoliticaEstadosActividad.PermiteTransicion(existente.Estado, nuevoEstado))
            return Resultado<RespuestaActividad>.Fallo(ErrorNegocio.Conflicto("transicion_actividad_invalida",
                $"No se permite cambiar una actividad de {existente.Estado} a {nuevoEstado}."));
        if (nuevoEstado == EstadosActividad.Completa && existente.CantidadEjecutadaAcumulada < solicitud.CantidadPlanificada)
            return Resultado<RespuestaActividad>.Fallo(ErrorNegocio.Conflicto("meta_actividad_no_alcanzada",
                "La actividad no puede completarse mientras su avance registrado sea menor que la meta."));

        // FRQ-0013 / CRQ-0001 (CET-32): Marcar actividad como retrasada exige motivo/justificación.
        if (nuevoEstado == EstadosActividad.Retrasada && string.IsNullOrWhiteSpace(solicitud.MotivoVariacion))
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.Validacion(
                    "justificacion_retraso_requerida",
                    "Debe registrar un motivo o justificación obligatoria al marcar una actividad como RETRASADA."));
        }

        if (solicitud.FechaFinPlan < solicitud.FechaInicioPlan || solicitud.CantidadPlanificada < 0m)
        {
            return Resultado<RespuestaActividad>.Fallo(
                ErrorNegocio.Validacion("datos_actividad_invalidos", "Verifique fechas planificadas y cantidad no negativa."));
        }

        if (solicitud.IdAsignacionResponsable is int idResp)
        {
            var idObraResp = await puertoSeguimiento.ObtenerIdObraDeAsignacionAsync(idResp, cancelacion);
            if (idObraResp != existente.IdObra)
            {
                return Resultado<RespuestaActividad>.Fallo(
                    ErrorNegocio.Conflicto("responsable_otra_obra", "El responsable de la actividad debe pertenecer a la misma obra."));
            }
        }

        var actor = autorizacion.Valor!;
        var actualizada = await puertoSeguimiento.ActualizarActividadAsync(
            idActividad,
            solicitud with
            {
                Nombre = solicitud.Nombre.Trim(),
                Descripcion = solicitud.Descripcion?.Trim(),
                UnidadMedida = solicitud.UnidadMedida.Trim(),
                Estado = nuevoEstado
            },
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "actividades",
                idActividad.ToString(),
                AccionesAuditoria.Modificar,
                "estado",
                existente.Estado,
                nuevoEstado,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaActividad>.Exito(actualizada!);
    }

    public async Task<Resultado<RespuestaAvance>> RegistrarAvanceAsync(
        SolicitudRegistrarAvance solicitud,
        CancellationToken cancelacion = default)
    {
        var validacionPermiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.RegistrarAvances);
        if (!validacionPermiso.EsExitoso)
        {
            return Resultado<RespuestaAvance>.Fallo(validacionPermiso.Error!);
        }

        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        var estado = string.IsNullOrWhiteSpace(solicitud.Estado)
            ? EstadosAvance.Registrado
            : solicitud.Estado.Trim().ToUpperInvariant();
        var existenteUuid = await puertoSeguimiento.ObtenerAvancePorUuidAsync(uuid, cancelacion);
        var confirmarBorrador = existenteUuid?.Estado == EstadosAvance.Borrador && estado == EstadosAvance.Registrado;
        if (existenteUuid is not null)
        {
            var authExistente = AutorizadorOperacion.ExigirPermisoYObra(
                contextoUsuario,
                CodigosPermiso.RegistrarAvances,
                existenteUuid.IdObra);
            if (!authExistente.EsExitoso)
            {
                return Resultado<RespuestaAvance>.Fallo(authExistente.Error!);
            }

            if (existenteUuid.IdActividad != solicitud.IdActividad || existenteUuid.FechaAvance != solicitud.FechaAvance
                || !confirmarBorrador && (existenteUuid.CantidadEjecutada != solicitud.CantidadEjecutada
                    || existenteUuid.Observaciones != solicitud.Observaciones?.Trim()))
                return Resultado<RespuestaAvance>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado", "El UUID ya está asociado a una operación con datos diferentes."));
            if (!confirmarBorrador)
                return Resultado<RespuestaAvance>.Exito(existenteUuid with { FueIdempotente = true });
        }

        if (solicitud.CantidadEjecutada < 0m)
        {
            return Resultado<RespuestaAvance>.Fallo(
                ErrorNegocio.Validacion("cantidad_ejecutada_invalida", "La cantidad ejecutada no puede ser negativa."));
        }

        var actividad = await puertoSeguimiento.ObtenerActividadPorIdAsync(solicitud.IdActividad, cancelacion);
        if (actividad is null)
        {
            return Resultado<RespuestaAvance>.Fallo(
                ErrorNegocio.NoEncontrado("actividad_no_encontrada", "La actividad indicada no existe."));
        }

        var autorizacionObra = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.RegistrarAvances,
            actividad.IdObra);
        if (!autorizacionObra.EsExitoso)
        {
            return Resultado<RespuestaAvance>.Fallo(autorizacionObra.Error!);
        }

        var obraOperativa = await politicaObra.ExigirActivaAsync(actividad.IdObra, cancelacion);
        if (!obraOperativa.EsExitoso) return Resultado<RespuestaAvance>.Fallo(obraOperativa.Error!);
        var observaciones = solicitud.Observaciones?.Trim();

        // FRQ-0013 / CRQ-0001 (CET-32): Si el avance es de actividad incompleta/retrasada, el motivo es obligatorio.
        if ((solicitud.EsIncompletoORetrasado || actividad.Estado == EstadosActividad.Retrasada)
            && string.IsNullOrWhiteSpace(observaciones))
        {
            return Resultado<RespuestaAvance>.Fallo(
                ErrorNegocio.Validacion(
                    "justificacion_obligatoria",
                    "Una actividad incompleta o retrasada exige registrar su motivo o circunstancia antes de finalizar el registro."));
        }

        if (solicitud.IdIncidenciaVinculada is long idIncidencia)
        {
            var idObraIncidencia = await puertoSeguimiento.ObtenerIdObraDeIncidenciaAsync(idIncidencia, cancelacion);
            if (idObraIncidencia is null)
            {
                return Resultado<RespuestaAvance>.Fallo(
                    ErrorNegocio.NoEncontrado("incidencia_no_encontrada", "La incidencia vinculada no existe."));
            }

            if (idObraIncidencia.Value != actividad.IdObra)
            {
                return Resultado<RespuestaAvance>.Fallo(
                    ErrorNegocio.Conflicto("incidencia_otra_obra", "La incidencia vinculada debe pertenecer a la misma obra de la actividad."));
            }
        }

        if (!confirmarBorrador && await puertoSeguimiento.ExisteAvanceActividadFechaAsync(solicitud.IdActividad, solicitud.FechaAvance, cancelacion))
        {
            return Resultado<RespuestaAvance>.Fallo(
                ErrorNegocio.Conflicto("avance_dia_duplicado", "Ya existe un avance registrado para esta actividad en la fecha indicada."));
        }

        if (!EstadosAvance.Todos.Contains(estado))
        {
            return Resultado<RespuestaAvance>.Fallo(
                ErrorNegocio.Validacion("estado_avance_invalido", "El estado del avance debe ser BORRADOR, REGISTRADO o ANULADO."));
        }

        var estadoActividad = PoliticaRegistroAvance.DeterminarEstado(solicitud with { Observaciones = observaciones }, actividad, estado, calendario.Hoy);
        if (!estadoActividad.EsExitoso) return Resultado<RespuestaAvance>.Fallo(estadoActividad.Error!);
        var actor = autorizacionObra.Valor!;
        var ahora = reloj.AhoraUtc;
        var sincronizadoEn = solicitud.EsSincronizacionDiferida ? ahora : (DateTime?)null;

        var avance = await puertoSeguimiento.RegistrarAvanceAsync(
            solicitud with { Observaciones = observaciones, EstadoActividad = estadoActividad.Valor },
            estado,
            actor.IdUsuario,
            uuid,
            ahora,
            sincronizadoEn,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "avances",
                uuid.ToString(),
                confirmarBorrador ? AccionesAuditoria.Modificar : AccionesAuditoria.Crear,
                confirmarBorrador ? "registro" : "cantidad_ejecutada",
                confirmarBorrador ? FormattableString.Invariant(
                    $"estado={existenteUuid!.Estado}; cantidad={existenteUuid.CantidadEjecutada:0.00}; observaciones={existenteUuid.Observaciones}") : null,
                confirmarBorrador ? FormattableString.Invariant(
                    $"estado={estado}; cantidad={solicitud.CantidadEjecutada:0.00}; observaciones={observaciones}")
                    : solicitud.CantidadEjecutada.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                actor.IpOrigen,
                confirmarBorrador ? Guid.NewGuid() : uuid),
            cancelacion,
            idAvanceBorrador: confirmarBorrador ? existenteUuid!.IdAvance : null);

        return Resultado<RespuestaAvance>.Exito(avance with { FueConfirmacionBorrador = confirmarBorrador });
    }

    public async Task<Resultado<RespuestaAvance>> JustificarActividadRetrasadaAsync(
        SolicitudJustificarActividad solicitud,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(solicitud.MotivoObligatorio))
        {
            return Resultado<RespuestaAvance>.Fallo(
                ErrorNegocio.Validacion(
                    "justificacion_obligatoria",
                    "Debe indicar el motivo obligatorio que explica el retraso o estado incompleto de la actividad."));
        }

        return await RegistrarAvanceAsync(
            new SolicitudRegistrarAvance(
                solicitud.IdActividad,
                solicitud.FechaAvance,
                solicitud.CantidadEjecutada,
                solicitud.MotivoObligatorio.Trim(),
                EstadosAvance.Registrado,
                EsIncompletoORetrasado: true,
                IdIncidenciaVinculada: solicitud.IdIncidenciaExistente),
            cancelacion);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaAvance>>> ListarAvancesPorObraAsync(
        int idObra,
        int? idActividad = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ConsultarObras,
            idObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaAvance>>.Fallo(autorizacion.Error!);
        }

        return Resultado<IReadOnlyList<RespuestaAvance>>.Exito(
            await puertoSeguimiento.ListarAvancesPorObraAsync(idObra, idActividad, fechaDesde, fechaHasta, cancelacion));
    }

    public async Task<Resultado<RespuestaEvidencia>> RegistrarEvidenciaAsync(
        SolicitudRegistrarEvidencia solicitud,
        CancellationToken cancelacion = default)
    {
        var validacionPermiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.RegistrarEvidencias);
        if (!validacionPermiso.EsExitoso)
        {
            return Resultado<RespuestaEvidencia>.Fallo(validacionPermiso.Error!);
        }

        var tipo = solicitud.TipoEvidencia?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!TiposEvidencia.Todos.Contains(tipo))
            return Resultado<RespuestaEvidencia>.Fallo(ErrorNegocio.Validacion("tipo_evidencia_invalido",
                "El tipo de evidencia debe ser FOTOGRAFIA, BITACORA o DOCUMENTO."));
        if (tipo == TiposEvidencia.Fotografia && (solicitud.CapturadaEn is null || solicitud.Latitud is null || solicitud.Longitud is null))
            return Resultado<RespuestaEvidencia>.Fallo(ErrorNegocio.Validacion("metadatos_fotografia_obligatorios",
                "La fotografía debe conservar fecha y hora de captura en UTC y coordenadas GPS."));
        if (solicitud.Latitud.HasValue != solicitud.Longitud.HasValue)
            return Resultado<RespuestaEvidencia>.Fallo(ErrorNegocio.Validacion("gps_incompleto", "La latitud y longitud deben enviarse juntas."));
        if (solicitud.Latitud is < -90m or > 90m || solicitud.Longitud is < -180m or > 180m)
            return Resultado<RespuestaEvidencia>.Fallo(ErrorNegocio.Validacion("coordenadas_evidencia_invalidas",
                "Las coordenadas GPS de la evidencia están fuera de rango."));
        solicitud = solicitud with
        {
            Latitud = solicitud.Latitud is { } latitud ? decimal.Round(latitud, 6) : null,
            Longitud = solicitud.Longitud is { } longitud ? decimal.Round(longitud, 6) : null
        };

        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        var existenteUuid = await puertoSeguimiento.ObtenerEvidenciaPorUuidAsync(uuid, cancelacion);
        if (existenteUuid is not null)
        {
            var authExistente = AutorizadorOperacion.ExigirPermisoYObra(
                contextoUsuario,
                CodigosPermiso.RegistrarEvidencias,
                existenteUuid.IdObra);
            if (!authExistente.EsExitoso)
            {
                return Resultado<RespuestaEvidencia>.Fallo(authExistente.Error!);
            }

            if (existenteUuid.IdAvance != solicitud.IdAvance || solicitud.ContenidoArchivo is not { Length: > 0 }
                || existenteUuid.HuellaArchivo != Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(solicitud.ContenidoArchivo))
                || existenteUuid.TipoEvidencia != tipo || existenteUuid.Latitud != solicitud.Latitud || existenteUuid.Longitud != solicitud.Longitud
                || (solicitud.CapturadaEn is { } capturada && existenteUuid.CapturadaEn != capturada))
                return Resultado<RespuestaEvidencia>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado", "El UUID ya está asociado a una operación con datos diferentes."));
            return Resultado<RespuestaEvidencia>.Exito(existenteUuid with { FueIdempotente = true });
        }

        var contextoAvance = await puertoSeguimiento.ObtenerContextoAvanceAsync(solicitud.IdAvance, cancelacion);
        if (contextoAvance is null)
        {
            return Resultado<RespuestaEvidencia>.Fallo(
                ErrorNegocio.NoEncontrado("avance_no_encontrado", "El avance al que se intenta adjuntar la evidencia no existe."));
        }

        var autorizacionObra = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.RegistrarEvidencias,
            contextoAvance.Value.IdObra);
        if (!autorizacionObra.EsExitoso)
        {
            return Resultado<RespuestaEvidencia>.Fallo(autorizacionObra.Error!);
        }

        var obraOperativa = await politicaObra.ExigirActivaAsync(contextoAvance.Value.IdObra, cancelacion);
        if (!obraOperativa.EsExitoso) return Resultado<RespuestaEvidencia>.Fallo(obraOperativa.Error!);

        if (solicitud.ContenidoArchivo is not { Length: > 0 } contenido || contenido.Length > almacenamientoEvidencias.MaximoBytes
            || string.IsNullOrWhiteSpace(solicitud.NombreArchivo))
            return Resultado<RespuestaEvidencia>.Fallo(ErrorNegocio.Validacion(
                "archivo_evidencia_requerido", $"Debe enviar nombre y contenido de archivo, hasta {almacenamientoEvidencias.MaximoBytes} bytes."));
        if (tipo == TiposEvidencia.Fotografia && Path.GetExtension(solicitud.NombreArchivo).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return Resultado<RespuestaEvidencia>.Fallo(ErrorNegocio.Validacion("fotografia_invalida", "Una fotografía requiere un archivo de imagen admitido."));
        var guardado = await almacenamientoEvidencias.GuardarEvidenciaAsync(
            contextoAvance.Value.IdObra, solicitud.IdAvance, solicitud.NombreArchivo, contenido, cancelacion);
        var rutaArchivo = guardado.RutaRelativa;
        var huellaArchivo = guardado.HuellaSha256;
        unidadTrabajo.AlRevertir(token => almacenamientoEvidencias.EliminarAsync(rutaArchivo, token));

        var actor = autorizacionObra.Valor!;
        var ahora = reloj.AhoraUtc;
        var capturadaEn = solicitud.CapturadaEn ?? ahora;
        var sincronizadoEn = solicitud.EsSincronizacionDiferida ? ahora : (DateTime?)null;

        var registrada = await puertoSeguimiento.RegistrarEvidenciaAsync(
            solicitud.IdAvance,
            tipo,
            rutaArchivo,
            huellaArchivo,
            capturadaEn,
            solicitud.Latitud,
            solicitud.Longitud,
            actor.IdUsuario,
            uuid,
            ahora,
            sincronizadoEn,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "evidencias",
                uuid.ToString(),
                AccionesAuditoria.Crear,
                "tipo_evidencia",
                null,
                tipo,
                actor.IpOrigen,
                uuid),
            cancelacion);

        return Resultado<RespuestaEvidencia>.Exito(registrada);
    }

    public async Task<Resultado<DescargaEvidencia>> DescargarEvidenciaAsync(long idEvidencia, CancellationToken cancelacion = default)
    {
        var evidencia = await puertoSeguimiento.ObtenerEvidenciaPorIdAsync(idEvidencia, cancelacion);
        if (evidencia is null)
            return Resultado<DescargaEvidencia>.Fallo(ErrorNegocio.NoEncontrado("evidencia_no_encontrada", "La evidencia no existe."));
        var permiso = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.ConsultarObras, evidencia.IdObra);
        if (!permiso.EsExitoso) return Resultado<DescargaEvidencia>.Fallo(permiso.Error!);
        var contenido = almacenamientoEvidencias.AbrirLectura(evidencia.RutaArchivo);
        return contenido is null
            ? Resultado<DescargaEvidencia>.Fallo(ErrorNegocio.NoEncontrado("archivo_no_encontrado", "El archivo de evidencia no está disponible."))
            : Resultado<DescargaEvidencia>.Exito(new DescargaEvidencia(contenido, Path.GetFileName(evidencia.RutaArchivo)));
    }

    public async Task<Resultado<RespuestaIncidenciaObra>> RegistrarIncidenciaObraAsync(
        SolicitudRegistrarIncidenciaObra solicitud,
        CancellationToken cancelacion = default)
    {
        var validacionPermiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.RegistrarIncidenciasObra);
        if (!validacionPermiso.EsExitoso)
        {
            return Resultado<RespuestaIncidenciaObra>.Fallo(validacionPermiso.Error!);
        }

        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        var existenteUuid = await puertoSeguimiento.ObtenerIncidenciaPorUuidAsync(uuid, cancelacion);
        if (existenteUuid is not null)
        {
            var authExistente = AutorizadorOperacion.ExigirPermisoYObra(
                contextoUsuario,
                CodigosPermiso.RegistrarIncidenciasObra,
                existenteUuid.IdObra);
            if (!authExistente.EsExitoso)
            {
                return Resultado<RespuestaIncidenciaObra>.Fallo(authExistente.Error!);
            }

            if (existenteUuid.IdObra != solicitud.IdObra || existenteUuid.TipoIncidencia != solicitud.TipoIncidencia?.Trim().ToUpperInvariant() || existenteUuid.InicioEn != solicitud.InicioEn || existenteUuid.FinEn != solicitud.FinEn || existenteUuid.Descripcion != solicitud.Descripcion?.Trim() || existenteUuid.HorasPerdidas != solicitud.HorasPerdidas)
                return Resultado<RespuestaIncidenciaObra>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado", "El UUID ya está asociado a una operación con datos diferentes."));
            return Resultado<RespuestaIncidenciaObra>.Exito(existenteUuid with { FueIdempotente = true });
        }

        var autorizacionObra = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.RegistrarIncidenciasObra,
            solicitud.IdObra);
        if (!autorizacionObra.EsExitoso)
        {
            return Resultado<RespuestaIncidenciaObra>.Fallo(autorizacionObra.Error!);
        }

        var tipo = solicitud.TipoIncidencia?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!TiposIncidenciaObra.Todos.Contains(tipo))
        {
            return Resultado<RespuestaIncidenciaObra>.Fallo(
                ErrorNegocio.Validacion("tipo_incidencia_invalido", "El tipo de incidencia debe ser CLIMA, MATERIAL_IMPORTADO, TECNICA, SEGURIDAD u OTRA."));
        }

        var descripcion = solicitud.Descripcion?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Length > 500)
        {
            return Resultado<RespuestaIncidenciaObra>.Fallo(
                ErrorNegocio.Validacion("descripcion_incidencia_invalida", "La descripción de la incidencia es obligatoria y no debe exceder 500 caracteres."));
        }

        if (solicitud.FinEn is DateTime fin && fin < solicitud.InicioEn)
        {
            return Resultado<RespuestaIncidenciaObra>.Fallo(
                ErrorNegocio.Validacion("fechas_incidencia_invalidas", "La fecha/hora de fin de la incidencia no puede ser anterior al inicio."));
        }

        if (solicitud.HorasPerdidas is < 0m)
        {
            return Resultado<RespuestaIncidenciaObra>.Fallo(
                ErrorNegocio.Validacion("horas_perdidas_invalidas", "Las horas perdidas no pueden ser negativas."));
        }

        if (!await puertoSeguimiento.ExisteObraAsync(solicitud.IdObra, cancelacion))
        {
            return Resultado<RespuestaIncidenciaObra>.Fallo(
                ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra indicada no existe."));
        }

        var obraOperativa = await politicaObra.ExigirActivaAsync(solicitud.IdObra, cancelacion);
        if (!obraOperativa.EsExitoso) return Resultado<RespuestaIncidenciaObra>.Fallo(obraOperativa.Error!);

        // CRQ-0003: Todas las actividades afectadas deben pertenecer a la misma obra.
        var actividadesAfectadas = solicitud.IdsActividadesAfectadas?.Distinct().ToArray() ?? [];
        foreach (var idActividad in actividadesAfectadas)
        {
            var idObraActividad = await puertoSeguimiento.ObtenerIdObraDeActividadAsync(idActividad, cancelacion);
            if (idObraActividad != solicitud.IdObra)
            {
                return Resultado<RespuestaIncidenciaObra>.Fallo(
                    ErrorNegocio.Conflicto(
                        "actividad_incidencia_otra_obra",
                        $"La actividad {idActividad} no pertenece a la obra {solicitud.IdObra}."));
            }
        }

        var estado = string.IsNullOrWhiteSpace(solicitud.Estado)
            ? EstadosIncidencia.Abierta
            : solicitud.Estado.Trim().ToUpperInvariant();
        if (!EstadosIncidencia.Todos.Contains(estado))
        {
            return Resultado<RespuestaIncidenciaObra>.Fallo(
                ErrorNegocio.Validacion("estado_incidencia_invalido", "El estado de la incidencia debe ser ABIERTA, RESUELTA o ANULADA."));
        }

        if (estado != EstadosIncidencia.Abierta)
            return Resultado<RespuestaIncidenciaObra>.Fallo(ErrorNegocio.Validacion("estado_inicial_incidencia_invalido",
                "Una incidencia nueva debe comenzar ABIERTA."));

        var actor = autorizacionObra.Valor!;
        var ahora = reloj.AhoraUtc;
        var sincronizadoEn = solicitud.EsSincronizacionDiferida ? ahora : (DateTime?)null;

        var registrada = await puertoSeguimiento.RegistrarIncidenciaObraAsync(
            solicitud with
            {
                TipoIncidencia = tipo,
                Descripcion = descripcion,
                Justificacion = solicitud.Justificacion?.Trim(),
                IdsActividadesAfectadas = actividadesAfectadas
            },
            estado,
            actor.IdUsuario,
            uuid,
            ahora,
            sincronizadoEn,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "incidencias",
                uuid.ToString(),
                AccionesAuditoria.Crear,
                "tipo_incidencia",
                null,
                tipo,
                actor.IpOrigen,
                uuid),
            cancelacion);

        return Resultado<RespuestaIncidenciaObra>.Exito(registrada);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaIncidenciaObra>>> ListarIncidenciasPorObraAsync(
        int idObra,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ConsultarObras,
            idObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaIncidenciaObra>>.Fallo(autorizacion.Error!);
        }

        return Resultado<IReadOnlyList<RespuestaIncidenciaObra>>.Exito(
            await puertoSeguimiento.ListarIncidenciasPorObraAsync(idObra, cancelacion));
    }

    public async Task<Resultado<RespuestaActa>> ElaborarActaSemanalAsync(
        SolicitudElaborarActa solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ElaborarActas,
            solicitud.IdObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaActa>.Fallo(autorizacion.Error!);
        }

        var numero = solicitud.NumeroActa?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(numero) || numero.Length > 30)
        {
            return Resultado<RespuestaActa>.Fallo(
                ErrorNegocio.Validacion("numero_acta_invalido", "El número del acta semanal es obligatorio y no debe exceder 30 caracteres."));
        }

        if (solicitud.PeriodoFin < solicitud.PeriodoInicio)
        {
            return Resultado<RespuestaActa>.Fallo(
                ErrorNegocio.Validacion("periodo_acta_invalido", "La fecha de fin del período del acta no puede ser anterior al inicio."));
        }

        if (!await puertoSeguimiento.ExisteObraAsync(solicitud.IdObra, cancelacion))
        {
            return Resultado<RespuestaActa>.Fallo(
                ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra indicada no existe."));
        }

        if (await puertoSeguimiento.ExisteNumeroActaEnObraAsync(solicitud.IdObra, numero, cancelacion))
        {
            return Resultado<RespuestaActa>.Fallo(
                ErrorNegocio.Conflicto("numero_acta_duplicado", "Ya existe un acta con ese número en la obra."));
        }

        var validacionContenido = await ValidarContenidoActaAsync(solicitud, cancelacion);
        if (!validacionContenido.EsExitoso) return Resultado<RespuestaActa>.Fallo(validacionContenido.Error!);
        var idsAvances = solicitud.IdsAvances?.Distinct().ToArray() ?? [];
        var idsIncidencias = solicitud.IdsIncidencias?.Distinct().ToArray() ?? [];
        var participantes = solicitud.Participantes ?? [];

        var actor = autorizacion.Valor!;
        const string estadoInicial = EstadosActa.Borrador;
        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();

        var acta = await puertoSeguimiento.ElaborarActaAsync(
            solicitud with
            {
                NumeroActa = numero,
                Observaciones = solicitud.Observaciones?.Trim(),
                MejorasPropuestas = solicitud.MejorasPropuestas?.Trim(),
                SolicitudesCambio = solicitud.SolicitudesCambio?.Trim(),
                IdsAvances = idsAvances,
                IdsIncidencias = idsIncidencias,
                Participantes = participantes
            },
            estadoInicial,
            actor.IdUsuario,
            uuid,
            reloj.AhoraUtc,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "actas",
                $"{solicitud.IdObra}:{numero}",
                AccionesAuditoria.Crear,
                "estado",
                null,
                estadoInicial,
                actor.IpOrigen,
                uuid),
            cancelacion);

        return solicitud.EnviarARevision
            ? await EnviarActaARevisionAsync(acta.IdActa, cancelacion)
            : Resultado<RespuestaActa>.Exito(acta);
    }

    public async Task<Resultado<RespuestaActa>> ResolverActaSemanalAsync(
        int idActa,
        SolicitudResolverActa solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.AprobarActas);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaActa>.Fallo(autorizacion.Error!);
        }

        var decision = solicitud.Decision?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!DecisionesActa.Todas.Contains(decision))
        {
            return Resultado<RespuestaActa>.Fallo(
                ErrorNegocio.Validacion("decision_acta_invalida", "La decisión de revisión del acta debe ser APROBADA o RECHAZADA."));
        }

        var existente = await puertoSeguimiento.ObtenerActaPorIdAsync(idActa, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaActa>.Fallo(
                ErrorNegocio.NoEncontrado("acta_no_encontrada", "El acta semanal solicitada no existe."));
        }

        if (!autorizacion.Valor!.TieneAccesoAObra(existente.IdObra))
            return Resultado<RespuestaActa>.Fallo(ErrorNegocio.Prohibido(
                "obra_fuera_de_alcance", "No tiene autorización para resolver actas de esta obra."));

        if (existente.Estado != EstadosActa.Pendiente)
        {
            return Resultado<RespuestaActa>.Fallo(
                ErrorNegocio.Conflicto("acta_no_pendiente", "Solo se pueden aprobar o rechazar actas enviadas a revisión en estado PENDIENTE."));
        }

        var actor = autorizacion.Valor!;
        var accionAuditoria = decision == DecisionesActa.Aprobada ? AccionesAuditoria.Aprobar : AccionesAuditoria.Rechazar;
        var resuelta = await puertoSeguimiento.ResolverActaAsync(
            idActa,
            actor.IdUsuario,
            decision,
            solicitud.Comentario?.Trim(),
            reloj.AhoraUtc,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "actas",
                idActa.ToString(),
                accionAuditoria,
                "estado",
                existente.Estado,
                decision,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaActa>.Exito(resuelta!);
    }

    public async Task<Resultado<IReadOnlyList<ResumenActa>>> ListarActasPorObraAsync(
        int idObra,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ConsultarObras,
            idObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<ResumenActa>>.Fallo(autorizacion.Error!);
        }

        return Resultado<IReadOnlyList<ResumenActa>>.Exito(
            await puertoSeguimiento.ListarActasPorObraAsync(idObra, cancelacion));
    }

    public async Task<Resultado<RespuestaActa>> ObtenerActaPorIdAsync(
        int idActa,
        CancellationToken cancelacion = default)
    {
        var acta = await puertoSeguimiento.ObtenerActaPorIdAsync(idActa, cancelacion);
        if (acta is null)
        {
            return Resultado<RespuestaActa>.Fallo(
                ErrorNegocio.NoEncontrado("acta_no_encontrada", "El acta semanal solicitada no existe."));
        }

        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ConsultarObras,
            acta.IdObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaActa>.Fallo(autorizacion.Error!);
        }

        return Resultado<RespuestaActa>.Exito(acta);
    }
    public async Task<Resultado<IReadOnlyList<RespuestaEvidencia>>> ListarEvidenciasPorAvanceAsync(
        long idAvance, CancellationToken cancelacion = default)
    {
        var avance = await ObtenerAvancePorIdAsync(idAvance, cancelacion);
        if (!avance.EsExitoso) return Resultado<IReadOnlyList<RespuestaEvidencia>>.Fallo(avance.Error!);
        return Resultado<IReadOnlyList<RespuestaEvidencia>>.Exito(
            await puertoSeguimiento.ListarEvidenciasPorAvanceAsync(idAvance, cancelacion));
    }

    public Task<Resultado<RespuestaActividad>> ObtenerActividadPorIdAsync(int idActividad, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarObras],
            () => puertoSeguimiento.ObtenerActividadPorIdAsync(idActividad, cancelacion), r => r.IdObra);

    public Task<Resultado<RespuestaAvance>> ObtenerAvancePorIdAsync(long idAvance, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarObras],
            () => puertoSeguimiento.ObtenerAvancePorIdAsync(idAvance, cancelacion), r => r.IdObra);

    public Task<Resultado<RespuestaEvidencia>> ObtenerEvidenciaPorIdAsync(long idEvidencia, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarObras],
            () => puertoSeguimiento.ObtenerEvidenciaPorIdAsync(idEvidencia, cancelacion), r => r.IdObra);

    public Task<Resultado<RespuestaIncidenciaObra>> ObtenerIncidenciaPorIdAsync(long idIncidencia, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarObras],
            () => puertoSeguimiento.ObtenerIncidenciaPorIdAsync(idIncidencia, cancelacion), r => r.IdObra);
}
