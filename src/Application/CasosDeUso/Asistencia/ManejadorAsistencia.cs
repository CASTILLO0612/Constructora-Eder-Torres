using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Asistencia;

public sealed partial class ManejadorAsistencia(
    IPuertoAsistencia puertoAsistencia,
    IPuertoObras puertoObras,
    IContextoUsuarioActual contextoUsuario,
    IRelojSistema reloj,
    ICalendarioLaboral calendario,
    PoliticaCapturaAsistencia politicaCaptura)
{
    public Task<Resultado<IReadOnlyList<RespuestaCorreccionAsistencia>>> ListarCorreccionesAsync(
        int? idObra, string? estado, CancellationToken cancelacion = default)
    {
        var normalizado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (normalizado is not null && !EstadosCorreccionAsistencia.Todos.Contains(normalizado))
            return Task.FromResult(Resultado<IReadOnlyList<RespuestaCorreccionAsistencia>>.Fallo(
                ErrorNegocio.Validacion("estado_correccion_invalido", "El estado de corrección no es válido.")));
        return ConsultaAutorizada.ListarAsync(contextoUsuario,
            [CodigosPermiso.CorregirAsistencia, CodigosPermiso.AprobarCorreccionAsistencia], idObra,
            obras => puertoAsistencia.ListarCorreccionesAsync(idObra, normalizado, obras, cancelacion));
    }

    public async Task<Resultado<RespuestaAsistencia>> RegistrarAsistenciaAsync(
        SolicitudRegistrarAsistencia solicitud,
        CancellationToken cancelacion = default)
    {
        var validacionPermiso = ExigirPermisoMarcacion();
        if (!validacionPermiso.EsExitoso)
        {
            return Resultado<RespuestaAsistencia>.Fallo(validacionPermiso.Error!);
        }

        var validacionGps = ValidarCoordenadas(solicitud.LatitudEntrada, solicitud.LongitudEntrada,
            solicitud.LatitudSalida, solicitud.LongitudSalida, exigirSalida: solicitud.SalidaEn.HasValue);
        if (!validacionGps.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(validacionGps.Error!);
        if (solicitud.SalidaEn is null && (solicitud.LatitudSalida.HasValue || solicitud.PrecisionGpsSalidaMetros.HasValue))
            return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.Validacion(
                "gps_salida_sin_marcacion", "Las coordenadas de salida requieren una hora de salida."));

        foreach (var precision in new[] { solicitud.PrecisionGpsEntradaMetros, solicitud.PrecisionGpsSalidaMetros })
            if (precision.HasValue && PoliticaCapturaAsistencia.ValidarPrecision(precision) is { EsExitoso: false } errorPrecision)
                return Resultado<RespuestaAsistencia>.Fallo(errorPrecision.Error!);

        var lecturaOriginal = solicitud;
        solicitud = solicitud with
        {
            LatitudEntrada = solicitud.LatitudEntrada is { } lat ? decimal.Round(lat, 6) : null,
            LongitudEntrada = solicitud.LongitudEntrada is { } lon ? decimal.Round(lon, 6) : null,
            LatitudSalida = solicitud.LatitudSalida is { } latS ? decimal.Round(latS, 6) : null,
            LongitudSalida = solicitud.LongitudSalida is { } lonS ? decimal.Round(lonS, 6) : null,
            PrecisionGpsEntradaMetros = solicitud.PrecisionGpsEntradaMetros is { } pE ? PoliticaCapturaAsistencia.NormalizarPrecision(pE) : null,
            PrecisionGpsSalidaMetros = solicitud.PrecisionGpsSalidaMetros is { } pS ? PoliticaCapturaAsistencia.NormalizarPrecision(pS) : null
        };
        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        await puertoAsistencia.BloquearMarcacionesAsync([solicitud.IdAsignacion], cancelacion);
        var existentePorUuid = await puertoAsistencia.ObtenerPorUuidAsync(uuid, cancelacion);
        if (existentePorUuid is not null)
        {
            var authExistente = AutorizarMarcacion(existentePorUuid.IdTrabajador, existentePorUuid.IdObra);
            if (!authExistente.EsExitoso)
            {
                return Resultado<RespuestaAsistencia>.Fallo(authExistente.Error!);
            }

            if (existentePorUuid.IdAsignacion != solicitud.IdAsignacion || existentePorUuid.FechaJornada != solicitud.FechaJornada
                || existentePorUuid.EntradaEn != solicitud.EntradaEn || existentePorUuid.LatitudEntrada != solicitud.LatitudEntrada
                || existentePorUuid.LongitudEntrada != solicitud.LongitudEntrada
                || existentePorUuid.PrecisionGpsEntradaMetros != solicitud.PrecisionGpsEntradaMetros
                || (solicitud.SalidaEn is not null && (existentePorUuid.SalidaEn != solicitud.SalidaEn
                    || existentePorUuid.LatitudSalida != solicitud.LatitudSalida || existentePorUuid.LongitudSalida != solicitud.LongitudSalida
                    || existentePorUuid.PrecisionGpsSalidaMetros != solicitud.PrecisionGpsSalidaMetros)))
                return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado", "El UUID ya está asociado a una operación con datos diferentes."));
            return Resultado<RespuestaAsistencia>.Exito(existentePorUuid with { FueIdempotente = true });
        }

        var instantes = politicaCaptura.ValidarInstantes(solicitud.EntradaEn, solicitud.SalidaEn);
        if (!instantes.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(instantes.Error!);
        var jornada = politicaCaptura.ValidarJornada(solicitud.FechaJornada, solicitud.EntradaEn);
        if (!jornada.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(jornada.Error!);
        var precisionEntrada = PoliticaCapturaAsistencia.ValidarPrecision(solicitud.PrecisionGpsEntradaMetros);
        if (!precisionEntrada.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(precisionEntrada.Error!);
        if (solicitud.SalidaEn.HasValue && PoliticaCapturaAsistencia.ValidarPrecision(solicitud.PrecisionGpsSalidaMetros) is { EsExitoso: false } precisionSalida)
            return Resultado<RespuestaAsistencia>.Fallo(precisionSalida.Error!);

        var contextoAsignacion = await puertoAsistencia.ObtenerContextoAsignacionAsync(solicitud.IdAsignacion, cancelacion);
        if (contextoAsignacion is null)
        {
            return Resultado<RespuestaAsistencia>.Fallo(
                ErrorNegocio.NoEncontrado("asignacion_no_encontrada", "La asignación del trabajador a la obra no existe."));
        }

        var autorizacionObra = AutorizarMarcacion(contextoAsignacion.Value.IdTrabajador, contextoAsignacion.Value.IdObra);
        if (!autorizacionObra.EsExitoso)
        {
            return Resultado<RespuestaAsistencia>.Fallo(autorizacionObra.Error!);
        }

        if (!contextoAsignacion.Value.EsVigenteEn(solicitud.FechaJornada))
        {
            return Resultado<RespuestaAsistencia>.Fallo(
                ErrorNegocio.Conflicto("asignacion_no_activa", "Solo se puede registrar asistencia sobre una asignación activa."));
        }

        if (await puertoAsistencia.ExisteMarcacionMismaEntradaAsync(solicitud.IdAsignacion, solicitud.EntradaEn, cancelacion))
        {
            return Resultado<RespuestaAsistencia>.Fallo(
                ErrorNegocio.Conflicto("marcacion_duplicada", "Ya existe una marcación de asistencia con el mismo instante de entrada para esta asignación."));
        }

        var geocerca = await ObtenerGeocercaAsync(contextoAsignacion.Value.IdObra, cancelacion);
        if (!geocerca.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(geocerca.Error!);
        var ubicacionEntrada = PoliticaCapturaAsistencia.ValidarUbicacion(geocerca.Valor,
            lecturaOriginal.LatitudEntrada!.Value, lecturaOriginal.LongitudEntrada!.Value, solicitud.PrecisionGpsEntradaMetros!.Value);
        if (!ubicacionEntrada.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(ubicacionEntrada.Error!);
        if (solicitud.SalidaEn.HasValue)
        {
            var ubicacionSalida = PoliticaCapturaAsistencia.ValidarUbicacion(geocerca.Valor,
                lecturaOriginal.LatitudSalida!.Value, lecturaOriginal.LongitudSalida!.Value, solicitud.PrecisionGpsSalidaMetros!.Value);
            if (!ubicacionSalida.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(ubicacionSalida.Error!);
        }
        if (await puertoAsistencia.ExisteSolapamientoAsync(contextoAsignacion.Value.IdTrabajador,
            solicitud.EntradaEn, solicitud.SalidaEn, cancelacion: cancelacion))
            return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.Conflicto("asistencia_solapada",
                "El trabajador ya tiene una asistencia abierta o un registro que se superpone con este período."));

        var actor = autorizacionObra.Valor!;
        var ahora = reloj.AhoraUtc;
        var estado = solicitud.SalidaEn.HasValue ? EstadosAsistencia.Completa : EstadosAsistencia.Abierta;
        var sincronizadoEn = solicitud.EsSincronizacionDiferida ? ahora : (DateTime?)null;

        var registrada = await puertoAsistencia.RegistrarAsistenciaAsync(
            solicitud,
            estado,
            actor.IdUsuario,
            uuid,
            ahora,
            sincronizadoEn,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "asistencias",
                uuid.ToString(),
                AccionesAuditoria.Crear,
                "estado",
                null,
                estado,
                actor.IpOrigen,
                uuid),
            cancelacion);

        return Resultado<RespuestaAsistencia>.Exito(registrada);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaAsistencia>>> RegistrarAsistenciaCuadrillaAsync(
        SolicitudRegistrarAsistenciaCuadrilla solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.RegistrarAsistencia,
            solicitud.IdObra);

        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(autorizacion.Error!);
        }

        if (solicitud.Marcaciones is null || solicitud.Marcaciones.Count == 0)
        {
            return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(
                ErrorNegocio.Validacion("marcaciones_vacias", "Debe seleccionar al menos un trabajador para registrar asistencia."));
        }

        var validacionGps = ValidarCoordenadas(
            solicitud.LatitudLectura,
            solicitud.LongitudLectura,
            solicitud.LatitudLectura,
            solicitud.LongitudLectura);
        if (!validacionGps.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(validacionGps.Error!);
        }

        await puertoAsistencia.BloquearMarcacionesAsync(solicitud.Marcaciones.Select(m => m.IdAsignacion).ToArray(), cancelacion);
        var resultados = new List<RespuestaAsistencia>(solicitud.Marcaciones.Count);
        foreach (var item in solicitud.Marcaciones)
        {
            var contextoAsignacion = await puertoAsistencia.ObtenerContextoAsignacionAsync(item.IdAsignacion, cancelacion);
            if (contextoAsignacion is null || contextoAsignacion.Value.IdObra != solicitud.IdObra)
            {
                return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(
                    ErrorNegocio.Conflicto(
                        "asignacion_obra_inconsistente",
                        $"La asignación {item.IdAsignacion} no pertenece a la obra {solicitud.IdObra}."));
            }

            var resultadoItem = await RegistrarAsistenciaAsync(
                new SolicitudRegistrarAsistencia(
                    item.IdAsignacion,
                    solicitud.FechaJornada,
                    item.EntradaEn,
                    item.SalidaEn,
                    solicitud.LatitudLectura,
                    solicitud.LongitudLectura,
                    item.SalidaEn.HasValue ? solicitud.LatitudLectura : null,
                    item.SalidaEn.HasValue ? solicitud.LongitudLectura : null,
                    item.UuidRegistro,
                    solicitud.EsSincronizacionDiferida,
                    solicitud.PrecisionGpsMetros,
                    item.SalidaEn.HasValue ? solicitud.PrecisionGpsMetros : null),
                cancelacion);

            if (!resultadoItem.EsExitoso)
            {
                return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(resultadoItem.Error!);
            }

            resultados.Add(resultadoItem.Valor!);
        }

        return Resultado<IReadOnlyList<RespuestaAsistencia>>.Exito(resultados);
    }

    public async Task<Resultado<RespuestaAsistencia>> RegistrarSalidaAsync(
        long idAsistencia,
        SolicitudRegistrarSalidaAsistencia solicitud,
        CancellationToken cancelacion = default)
    {
        await puertoAsistencia.BloquearAsistenciaAsync(idAsistencia, cancelacion);
        var existente = await puertoAsistencia.ObtenerPorIdAsync(idAsistencia, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaAsistencia>.Fallo(
                ErrorNegocio.NoEncontrado("asistencia_no_encontrada", "El registro de asistencia no existe."));
        }

        var autorizacion = AutorizarMarcacion(existente.IdTrabajador, existente.IdObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaAsistencia>.Fallo(autorizacion.Error!);
        }

        if (solicitud.SalidaEn < existente.EntradaEn)
        {
            return Resultado<RespuestaAsistencia>.Fallo(
                ErrorNegocio.Validacion("horas_asistencia_invalidas", "La hora de salida no puede ser anterior a la hora de entrada."));
        }

        if (solicitud.UuidRegistro is { } uuid && uuid != existente.UuidRegistro)
            return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.Conflicto("uuid_asistencia_inconsistente",
                "El UUID de salida debe corresponder al registro de entrada."));

        var validacionGps = ValidarCoordenadas(null, null, solicitud.LatitudSalida, solicitud.LongitudSalida,
            exigirEntrada: false, exigirSalida: true);
        if (!validacionGps.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(validacionGps.Error!);
        if (solicitud.PrecisionGpsSalidaMetros.HasValue
            && PoliticaCapturaAsistencia.ValidarPrecision(solicitud.PrecisionGpsSalidaMetros) is { EsExitoso: false } precisionInvalida)
            return Resultado<RespuestaAsistencia>.Fallo(precisionInvalida.Error!);
        var lecturaOriginal = solicitud;
        solicitud = solicitud with
        {
            LatitudSalida = decimal.Round(solicitud.LatitudSalida!.Value, 6),
            LongitudSalida = decimal.Round(solicitud.LongitudSalida!.Value, 6),
            PrecisionGpsSalidaMetros = solicitud.PrecisionGpsSalidaMetros is { } p ? PoliticaCapturaAsistencia.NormalizarPrecision(p) : null
        };

        if (existente.SalidaEn == solicitud.SalidaEn && existente.Estado == EstadosAsistencia.Completa
            && existente.LatitudSalida == solicitud.LatitudSalida && existente.LongitudSalida == solicitud.LongitudSalida
            && existente.PrecisionGpsSalidaMetros == solicitud.PrecisionGpsSalidaMetros)
            return Resultado<RespuestaAsistencia>.Exito(existente with { FueIdempotente = true });
        if (existente.Estado != EstadosAsistencia.Abierta || existente.SalidaEn is not null)
            return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.Conflicto(
                "asistencia_no_abierta", "La asistencia ya está cerrada, corregida o anulada. Utilice una corrección autorizada."));

        var precisionSalida = PoliticaCapturaAsistencia.ValidarPrecision(solicitud.PrecisionGpsSalidaMetros);
        if (!precisionSalida.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(precisionSalida.Error!);
        var instantes = politicaCaptura.ValidarInstantes(existente.EntradaEn, solicitud.SalidaEn);
        if (!instantes.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(instantes.Error!);

        var contextoAsignacion = await puertoAsistencia.ObtenerContextoAsignacionAsync(existente.IdAsignacion, cancelacion);
        if (contextoAsignacion is null || !contextoAsignacion.Value.EsVigenteEn(existente.FechaJornada))
            return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.Conflicto("asignacion_no_activa",
                "La asignación del trabajador no está activa para esta jornada."));
        var geocerca = await ObtenerGeocercaAsync(existente.IdObra, cancelacion);
        if (!geocerca.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(geocerca.Error!);
        var ubicacion = PoliticaCapturaAsistencia.ValidarUbicacion(geocerca.Valor, lecturaOriginal.LatitudSalida!.Value,
            lecturaOriginal.LongitudSalida!.Value, solicitud.PrecisionGpsSalidaMetros!.Value);
        if (!ubicacion.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(ubicacion.Error!);
        if (await puertoAsistencia.ExisteSolapamientoAsync(existente.IdTrabajador, existente.EntradaEn,
            solicitud.SalidaEn, existente.IdAsistencia, cancelacion))
            return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.Conflicto("asistencia_solapada",
                "La salida se superpone con otra asistencia del trabajador."));

        var actor = autorizacion.Valor!;
        var actualizada = await puertoAsistencia.RegistrarSalidaAsistenciaAsync(
            idAsistencia,
            solicitud.SalidaEn,
            solicitud.LatitudSalida,
            solicitud.LongitudSalida,
            solicitud.PrecisionGpsSalidaMetros,
            solicitud.EsSincronizacionDiferida ? reloj.AhoraUtc : null,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "asistencias",
                idAsistencia.ToString(),
                AccionesAuditoria.Modificar,
                "salida_at",
                existente.SalidaEn?.ToString("O"),
                solicitud.SalidaEn.ToString("O"),
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaAsistencia>.Exito(actualizada!);
    }

    public async Task<Resultado<RespuestaAsistencia>> SincronizarSalidaAsync(
        SolicitudSincronizarSalidaAsistencia solicitud, CancellationToken cancelacion = default)
    {
        var permiso = ExigirPermisoMarcacion();
        if (!permiso.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(permiso.Error!);
        var existente = solicitud.IdAsistencia is { } id
            ? await puertoAsistencia.ObtenerPorIdAsync(id, cancelacion)
            : await puertoAsistencia.ObtenerPorUuidAsync(solicitud.UuidRegistro ?? Guid.Empty, cancelacion);
        if (existente is null) return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.NoEncontrado(
            "asistencia_no_encontrada", "Sincronice la entrada antes de confirmar su salida."));
        return await RegistrarSalidaAsync(existente.IdAsistencia,
            new(solicitud.SalidaEn, solicitud.LatitudSalida, solicitud.LongitudSalida, solicitud.UuidRegistro, true,
                solicitud.PrecisionGpsSalidaMetros), cancelacion);
    }

    public async Task<Resultado<RespuestaCorreccionAsistencia>> SolicitarCorreccionAsync(
        SolicitudCrearCorreccionAsistencia solicitud,
        CancellationToken cancelacion = default)
    {
        var motivo = solicitud.Motivo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Length > 300)
        {
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(
                ErrorNegocio.Validacion("motivo_correccion_invalido", "El motivo de la corrección es obligatorio y no debe exceder 300 caracteres."));
        }

        if (solicitud.EntradaNueva is null && solicitud.SalidaNueva is null)
        {
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(
                ErrorNegocio.Validacion("correccion_sin_cambios", "Debe especificar al menos una nueva hora de entrada o de salida."));
        }

        await puertoAsistencia.BloquearAsistenciaAsync(solicitud.IdAsistencia, cancelacion);
        var asistencia = await puertoAsistencia.ObtenerPorIdAsync(solicitud.IdAsistencia, cancelacion);
        if (asistencia is null)
        {
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(
                ErrorNegocio.NoEncontrado("asistencia_no_encontrada", "El registro de asistencia a corregir no existe."));
        }

        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.CorregirAsistencia,
            asistencia.IdObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(autorizacion.Error!);
        }

        var entradaEfectiva = solicitud.EntradaNueva ?? asistencia.EntradaEn;
        var salidaEfectiva = solicitud.SalidaNueva ?? asistencia.SalidaEn;
        var temporal = politicaCaptura.ValidarInstantes(entradaEfectiva, salidaEfectiva);
        if (!temporal.EsExitoso) return Resultado<RespuestaCorreccionAsistencia>.Fallo(temporal.Error!);
        var periodo = await ValidarPeriodoCorreccionAsync(asistencia.IdAsignacion, entradaEfectiva, cancelacion);
        if (!periodo.EsExitoso) return Resultado<RespuestaCorreccionAsistencia>.Fallo(periodo.Error!);
        var actor = autorizacion.Valor!;
        var correccion = await puertoAsistencia.CrearCorreccionAsistenciaAsync(
            solicitud.IdAsistencia,
            actor.IdUsuario,
            asistencia.EntradaEn,
            asistencia.SalidaEn,
            entradaEfectiva,
            salidaEfectiva,
            motivo,
            reloj.AhoraUtc,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "correcciones_asistencia",
                solicitud.IdAsistencia.ToString(),
                AccionesAuditoria.Crear,
                "estado",
                null,
                EstadosCorreccionAsistencia.Pendiente,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaCorreccionAsistencia>.Exito(correccion);
    }

    public async Task<Resultado<RespuestaCorreccionAsistencia>> ResolverCorreccionAsync(
        long idCorreccion,
        SolicitudResolverCorreccionAsistencia solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.AprobarCorreccionAsistencia);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(autorizacion.Error!);
        }

        var decision = solicitud.Decision?.Trim().ToUpperInvariant() ?? string.Empty;
        if (decision is not (EstadosCorreccionAsistencia.Aprobada or EstadosCorreccionAsistencia.Rechazada))
        {
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(
                ErrorNegocio.Validacion("decision_correccion_invalida", "La decisión debe ser APROBADA o RECHAZADA."));
        }

        var existente = await puertoAsistencia.ObtenerCorreccionPorIdAsync(idCorreccion, cancelacion);
        if (existente is not null)
        {
            await puertoAsistencia.BloquearAsistenciaAsync(existente.IdAsistencia, cancelacion);
            existente = await puertoAsistencia.ObtenerCorreccionPorIdAsync(idCorreccion, cancelacion);
        }
        if (existente is null)
        {
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(
                ErrorNegocio.NoEncontrado("correccion_no_encontrada", "La solicitud de corrección no existe."));
        }

        if (!autorizacion.Valor!.TieneAccesoAObra(existente.IdObra))
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(ErrorNegocio.Prohibido(
                "obra_fuera_de_alcance", "No tiene autorización para resolver correcciones de esta obra."));

        if (existente.Estado != EstadosCorreccionAsistencia.Pendiente)
        {
            return Resultado<RespuestaCorreccionAsistencia>.Fallo(
                ErrorNegocio.Conflicto("correccion_ya_resuelta", "La solicitud de corrección ya fue procesada anteriormente."));
        }

        if (decision == EstadosCorreccionAsistencia.Aprobada)
        {
            var asistencia = await puertoAsistencia.ObtenerPorIdAsync(existente.IdAsistencia, cancelacion);
            if (asistencia is null) return Resultado<RespuestaCorreccionAsistencia>.Fallo(ErrorNegocio.NoEncontrado(
                "asistencia_no_encontrada", "La asistencia ya no existe."));
            var entrada = existente.EntradaNueva ?? asistencia.EntradaEn;
            var instantes = politicaCaptura.ValidarInstantes(entrada, existente.SalidaNueva ?? asistencia.SalidaEn);
            if (!instantes.EsExitoso) return Resultado<RespuestaCorreccionAsistencia>.Fallo(instantes.Error!);
            var periodo = await ValidarPeriodoCorreccionAsync(asistencia.IdAsignacion, entrada, cancelacion);
            if (!periodo.EsExitoso) return Resultado<RespuestaCorreccionAsistencia>.Fallo(periodo.Error!);
        }

        var actor = autorizacion.Valor!;
        var accionAuditoria = decision == EstadosCorreccionAsistencia.Aprobada
            ? AccionesAuditoria.Aprobar
            : AccionesAuditoria.Rechazar;

        var resuelta = await puertoAsistencia.ResolverCorreccionAsistenciaAsync(
            idCorreccion,
            actor.IdUsuario,
            decision,
            reloj.AhoraUtc,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "correcciones_asistencia",
                idCorreccion.ToString(),
                accionAuditoria,
                "estado",
                existente.Estado,
                decision,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaCorreccionAsistencia>.Exito(resuelta!);
    }

    public async Task<Resultado<IReadOnlyList<ItemConsolidadoAsistencia>>> ConsultarConsolidadoAsync(
        FiltroConsolidadoAsistencia filtro,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarConsolidadoAsistencia);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<ItemConsolidadoAsistencia>>.Fallo(autorizacion.Error!);
        }

        if (filtro.FechaDesde is DateOnly desde && filtro.FechaHasta is DateOnly hasta && hasta < desde)
        {
            return Resultado<IReadOnlyList<ItemConsolidadoAsistencia>>.Fallo(
                ErrorNegocio.Validacion("periodo_consolidado_invalido", "La fecha final del período no puede ser anterior a la fecha inicial."));
        }

        var usuario = autorizacion.Valor!;
        if (filtro.IdObra is int idObra && !usuario.TieneAccesoAObra(idObra))
        {
            return Resultado<IReadOnlyList<ItemConsolidadoAsistencia>>.Fallo(
                ErrorNegocio.Prohibido("obra_fuera_de_alcance", "No tiene autorización para consultar la asistencia de esta obra."));
        }

        IReadOnlySet<int>? obrasPermitidas = usuario.TieneAlcanceGlobal ? null : usuario.ObrasAsignadas;
        var consolidado = await puertoAsistencia.ConsultarConsolidadoAsync(filtro, obrasPermitidas, cancelacion);
        return Resultado<IReadOnlyList<ItemConsolidadoAsistencia>>.Exito(consolidado);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaAsistencia>>> ListarRegistrosAsync(
        FiltroConsolidadoAsistencia filtro, CancellationToken cancelacion = default)
    {
        var permiso = ExigirPermisoLectura();
        if (!permiso.EsExitoso) return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(permiso.Error!);
        var usuario = permiso.Valor!;
        if (!usuario.TienePermiso(CodigosPermiso.ConsultarConsolidadoAsistencia))
        {
            if (usuario.IdTrabajador is not int idTrabajador || filtro.IdTrabajador is { } solicitado && solicitado != idTrabajador)
                return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(AsistenciaAjena());
            if (filtro.FechaHasta < filtro.FechaDesde)
                return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(ErrorNegocio.Validacion("periodo_invalido", "La fecha final no puede preceder a la inicial."));
            return Resultado<IReadOnlyList<RespuestaAsistencia>>.Exito(await puertoAsistencia.ListarRegistrosAsync(
                filtro with { IdTrabajador = idTrabajador }, null, cancelacion));
        }
        if (filtro.IdObra is { } obra && !usuario.TieneAccesoAObra(obra))
            return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(ErrorNegocio.Prohibido("obra_fuera_de_alcance", "La obra no está autorizada."));
        if (filtro.FechaHasta < filtro.FechaDesde)
            return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(ErrorNegocio.Validacion("periodo_invalido", "La fecha final no puede preceder a la inicial."));
        return Resultado<IReadOnlyList<RespuestaAsistencia>>.Exito(await puertoAsistencia.ListarRegistrosAsync(
            filtro, usuario.TieneAlcanceGlobal ? null : usuario.ObrasAsignadas, cancelacion));
    }

    private static Resultado ValidarCoordenadas(
        decimal? latEntrada,
        decimal? lonEntrada,
        decimal? latSalida,
        decimal? lonSalida,
        bool exigirEntrada = true,
        bool exigirSalida = false)
    {
        if (latEntrada.HasValue != lonEntrada.HasValue || latSalida.HasValue != lonSalida.HasValue)
            return Resultado.Fallo(ErrorNegocio.Validacion("gps_incompleto", "La latitud y longitud deben enviarse juntas."));
        if ((exigirEntrada && latEntrada is null) || (exigirSalida && latSalida is null))
            return Resultado.Fallo(ErrorNegocio.Validacion("gps_obligatorio",
                "Debe obtener la ubicación GPS antes de registrar la entrada o la salida."));
        if ((latEntrada is < -90m or > 90m) || (latSalida is < -90m or > 90m))
        {
            return Resultado.Fallo(
                ErrorNegocio.Validacion("latitud_invalida", "La latitud GPS debe encontrarse en el rango de -90 a 90 grados."));
        }

        if ((lonEntrada is < -180m or > 180m) || (lonSalida is < -180m or > 180m))
        {
            return Resultado.Fallo(
                ErrorNegocio.Validacion("longitud_invalida", "La longitud GPS debe encontrarse en el rango de -180 a 180 grados."));
        }

        return Resultado.Exito();
    }
    public async Task<Resultado<RespuestaAsistencia>> ObtenerAsistenciaPorIdAsync(long idAsistencia, CancellationToken cancelacion = default)
    {
        var permiso = ExigirPermisoLectura();
        if (!permiso.EsExitoso) return Resultado<RespuestaAsistencia>.Fallo(permiso.Error!);
        var registro = await puertoAsistencia.ObtenerPorIdAsync(idAsistencia, cancelacion);
        if (registro is null) return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.NoEncontrado(
            "asistencia_no_encontrada", "El registro de asistencia no existe."));
        var usuario = permiso.Valor!;
        if (usuario.TienePermiso(CodigosPermiso.ConsultarConsolidadoAsistencia))
        {
            if (!usuario.TieneAccesoAObra(registro.IdObra))
                return Resultado<RespuestaAsistencia>.Fallo(ErrorNegocio.Prohibido("obra_fuera_de_alcance", "La obra no está autorizada."));
        }
        else if (usuario.IdTrabajador != registro.IdTrabajador)
            return Resultado<RespuestaAsistencia>.Fallo(AsistenciaAjena());
        return Resultado<RespuestaAsistencia>.Exito(registro);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaAsignacionAsistencia>>> ListarAsignacionesPropiasAsync(CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarAsistenciaPropia);
        if (!permiso.EsExitoso) return Resultado<IReadOnlyList<RespuestaAsignacionAsistencia>>.Fallo(permiso.Error!);
        if (permiso.Valor!.IdTrabajador is not int idTrabajador)
            return Resultado<IReadOnlyList<RespuestaAsignacionAsistencia>>.Fallo(AsistenciaAjena());
        return Resultado<IReadOnlyList<RespuestaAsignacionAsistencia>>.Exito(
            await puertoAsistencia.ListarAsignacionesPropiasAsync(idTrabajador, calendario.Hoy, cancelacion));
    }

    public async Task<Resultado<RespuestaValidacionUbicacionAsistencia>> ValidarUbicacionAsync(
        SolicitudValidarUbicacionAsistencia solicitud, CancellationToken cancelacion = default)
    {
        var permiso = ExigirPermisoMarcacion();
        if (!permiso.EsExitoso) return Resultado<RespuestaValidacionUbicacionAsistencia>.Fallo(permiso.Error!);
        var coordenadas = ValidarCoordenadas(solicitud.Latitud, solicitud.Longitud, null, null);
        if (!coordenadas.EsExitoso) return Resultado<RespuestaValidacionUbicacionAsistencia>.Fallo(coordenadas.Error!);
        var precisionValida = PoliticaCapturaAsistencia.ValidarPrecision(solicitud.PrecisionGpsMetros);
        if (!precisionValida.EsExitoso) return Resultado<RespuestaValidacionUbicacionAsistencia>.Fallo(precisionValida.Error!);
        var asignacion = await puertoAsistencia.ObtenerContextoAsignacionAsync(solicitud.IdAsignacion, cancelacion);
        if (asignacion is null) return Resultado<RespuestaValidacionUbicacionAsistencia>.Fallo(ErrorNegocio.NoEncontrado(
            "asignacion_no_encontrada", "La asignación no existe."));
        var permisoObra = AutorizarMarcacion(asignacion.Value.IdTrabajador, asignacion.Value.IdObra);
        if (!permisoObra.EsExitoso) return Resultado<RespuestaValidacionUbicacionAsistencia>.Fallo(permisoObra.Error!);
        if (!asignacion.Value.EsVigenteEn(calendario.Hoy))
            return Resultado<RespuestaValidacionUbicacionAsistencia>.Fallo(ErrorNegocio.Conflicto("asignacion_no_activa", "La asignación no está activa."));
        var geocerca = await ObtenerGeocercaAsync(asignacion.Value.IdObra, cancelacion);
        if (!geocerca.EsExitoso) return Resultado<RespuestaValidacionUbicacionAsistencia>.Fallo(geocerca.Error!);
        var latitud = solicitud.Latitud!.Value;
        var longitud = solicitud.Longitud!.Value;
        var precision = PoliticaCapturaAsistencia.NormalizarPrecision(solicitud.PrecisionGpsMetros!.Value);
        var distancia = geocerca.Valor.CalcularDistanciaMetros(latitud, longitud);
        return Resultado<RespuestaValidacionUbicacionAsistencia>.Exito(new(solicitud.IdAsignacion,
            asignacion.Value.IdObra, distancia + (double)precision <= (double)geocerca.Valor.RadioMetros,
            decimal.Round((decimal)distancia, 2), geocerca.Valor.RadioMetros, precision,
            decimal.Ceiling(((decimal)distancia + precision) * 100m) / 100m));
    }

    private Resultado<UsuarioAutenticado> ExigirPermisoMarcacion() => ExigirAlguno(
        CodigosPermiso.RegistrarAsistencia, CodigosPermiso.RegistrarAsistenciaPropia);

    private Resultado<UsuarioAutenticado> ExigirPermisoLectura() => ExigirAlguno(
        CodigosPermiso.ConsultarConsolidadoAsistencia, CodigosPermiso.ConsultarAsistenciaPropia);

    private Resultado<UsuarioAutenticado> ExigirAlguno(string permisoGeneral, string permisoPropio)
    {
        var usuario = contextoUsuario.ObtenerUsuario();
        if (usuario is null) return Resultado<UsuarioAutenticado>.Fallo(ErrorNegocio.NoAutenticado("sesion_requerida", "Debe iniciar sesión."));
        if (!usuario.TienePermiso(permisoGeneral) && !usuario.TienePermiso(permisoPropio))
            return Resultado<UsuarioAutenticado>.Fallo(ErrorNegocio.Prohibido("permiso_denegado", "No cuenta con permiso para esta operación."));
        return Resultado<UsuarioAutenticado>.Exito(usuario);
    }

    private Resultado<UsuarioAutenticado> AutorizarMarcacion(int idTrabajador, int idObra)
    {
        var permiso = ExigirPermisoMarcacion();
        if (!permiso.EsExitoso) return permiso;
        var usuario = permiso.Valor!;
        if (!usuario.TienePermiso(CodigosPermiso.RegistrarAsistencia) && usuario.IdTrabajador != idTrabajador)
            return Resultado<UsuarioAutenticado>.Fallo(AsistenciaAjena());
        if (!usuario.TieneAccesoAObra(idObra))
            return Resultado<UsuarioAutenticado>.Fallo(ErrorNegocio.Prohibido("obra_fuera_de_alcance", "La obra no está autorizada."));
        return permiso;
    }

    private async Task<Resultado<GeocercaObra>> ObtenerGeocercaAsync(int idObra, CancellationToken cancelacion)
    {
        var obra = await puertoObras.ObtenerObraPorIdAsync(idObra, cancelacion);
        if (obra is null) return Resultado<GeocercaObra>.Fallo(ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra no existe."));
        if (!GeocercaObra.EsConfiguracionValida(obra.Latitud, obra.Longitud, obra.RadioAsistenciaMetros))
            return Resultado<GeocercaObra>.Fallo(ErrorNegocio.Conflicto("geocerca_no_configurada",
                "Configure las coordenadas y el radio de asistencia de la obra antes de marcar."));
        if (obra.Estado != EstadosObra.Activa)
            return Resultado<GeocercaObra>.Fallo(ErrorNegocio.Conflicto("obra_no_activa", "La obra debe estar activa para registrar asistencia."));
        return Resultado<GeocercaObra>.Exito(new(obra.Latitud!.Value, obra.Longitud!.Value, obra.RadioAsistenciaMetros!.Value));
    }

    private async Task<Resultado> ValidarPeriodoCorreccionAsync(int idAsignacion, DateTime entrada, CancellationToken cancelacion)
    {
        var asignacion = await puertoAsistencia.ObtenerContextoAsignacionAsync(idAsignacion, cancelacion);
        var fecha = calendario.ObtenerFecha(entrada);
        return asignacion is { } a && a.FechaInicio <= fecha && (a.FechaFin is null || a.FechaFin >= fecha)
            ? Resultado.Exito() : Resultado.Fallo(ErrorNegocio.Validacion("correccion_fuera_de_asignacion",
                "La entrada corregida debe permanecer dentro del período de la asignación."));
    }

    private static ErrorNegocio AsistenciaAjena() => ErrorNegocio.Prohibido("asistencia_ajena",
        "La cuenta solo puede consultar o registrar la asistencia del trabajador al que está vinculada.");

    public Task<Resultado<RespuestaCorreccionAsistencia>> ObtenerCorreccionPorIdAsync(long idCorreccion, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarConsolidadoAsistencia,
            CodigosPermiso.CorregirAsistencia, CodigosPermiso.AprobarCorreccionAsistencia],
            () => puertoAsistencia.ObtenerCorreccionPorIdAsync(idCorreccion, cancelacion), r => r.IdObra);
}
