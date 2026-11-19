using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.EPP;

public sealed partial class ManejadorEpp(
    IPuertoEpp puertoEpp,
    IContextoUsuarioActual contextoUsuario,
    IRelojSistema reloj,
    PoliticaOperacionesObra politicaObra)
{
    public async Task<Resultado<RespuestaTipoEpp>> CrearTipoEppAsync(
        SolicitudCrearTipoEpp solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioEpp);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaTipoEpp>.Fallo(autorizacion.Error!);
        }

        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 80)
        {
            return Resultado<RespuestaTipoEpp>.Fallo(
                ErrorNegocio.Validacion("nombre_tipo_epp_invalido", "El nombre del tipo de EPP es obligatorio y no debe exceder 80 caracteres."));
        }

        if (solicitud.ExistenciaMinima < 0 || solicitud.VidaUtilDias is <= 0)
        {
            return Resultado<RespuestaTipoEpp>.Fallo(
                ErrorNegocio.Validacion("parametros_tipo_epp_invalidos", "El stock mínimo no puede ser negativo y la vida útil, si se indica, debe ser mayor que cero."));
        }

        if (await puertoEpp.ExisteTipoEppPorNombreAsync(nombre, cancelacion))
        {
            return Resultado<RespuestaTipoEpp>.Fallo(
                ErrorNegocio.Conflicto("tipo_epp_duplicado", "Ya existe un tipo de EPP registrado con ese nombre."));
        }

        var creado = await puertoEpp.CrearTipoEppAsync(
            solicitud with { Nombre = nombre, Descripcion = solicitud.Descripcion?.Trim() },
            cancelacion);
        return Resultado<RespuestaTipoEpp>.Exito(creado);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaTipoEpp>>> ListarTiposEppAsync(
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarHistorialEpp);
        if (!autorizacion.EsExitoso)
        {
            var alt = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioEpp);
            if (!alt.EsExitoso)
            {
                return Resultado<IReadOnlyList<RespuestaTipoEpp>>.Fallo(autorizacion.Error!);
            }
        }

        return Resultado<IReadOnlyList<RespuestaTipoEpp>>.Exito(
            await puertoEpp.ListarTiposEppAsync(cancelacion));
    }

    public async Task<Resultado<RespuestaEpp>> RegistrarEntradaAutorizadaAsync(
        SolicitudRegistrarEntradaEpp solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioEpp);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaEpp>.Fallo(autorizacion.Error!);
        }

        var codigo = solicitud.CodigoInterno?.Trim().ToUpperInvariant() ?? string.Empty;
        var serie = string.IsNullOrWhiteSpace(solicitud.NumeroSerie) ? null : solicitud.NumeroSerie.Trim().ToUpperInvariant();
        var condicion = string.IsNullOrWhiteSpace(solicitud.Condicion)
            ? CondicionesEpp.Nuevo
            : solicitud.Condicion.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 30)
        {
            return Resultado<RespuestaEpp>.Fallo(
                ErrorNegocio.Validacion("codigo_epp_invalido", "El código interno del EPP es obligatorio y no debe exceder 30 caracteres."));
        }

        if (!CondicionesEpp.Todas.Contains(condicion))
        {
            return Resultado<RespuestaEpp>.Fallo(
                ErrorNegocio.Validacion("condicion_epp_invalida", "La condición del EPP debe ser NUEVO, BUENO, DESGASTADO o DANADO."));
        }

        var tipoEpp = await puertoEpp.ObtenerTipoEppPorIdAsync(solicitud.IdTipoEpp, cancelacion);
        if (tipoEpp is null || !tipoEpp.Activo)
        {
            return Resultado<RespuestaEpp>.Fallo(
                ErrorNegocio.NoEncontrado("tipo_epp_no_encontrado", "El tipo de EPP indicado no existe o está inactivo."));
        }

        if (await puertoEpp.ExisteEppPorCodigoAsync(codigo, cancelacion))
        {
            return Resultado<RespuestaEpp>.Fallo(
                ErrorNegocio.Conflicto("codigo_epp_duplicado", "Ya existe una unidad de EPP registrada con ese código interno."));
        }

        if (serie is not null && await puertoEpp.ExisteEppPorSerieAsync(serie, cancelacion))
        {
            return Resultado<RespuestaEpp>.Fallo(
                ErrorNegocio.Conflicto("serie_epp_duplicada", "Ya existe una unidad de EPP registrada con ese número de serie."));
        }

        var actor = autorizacion.Valor!;
        var uuid = solicitud.UuidMovimiento is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        var creado = await puertoEpp.RegistrarEntradaEppAsync(
            solicitud with
            {
                CodigoInterno = codigo,
                NumeroSerie = serie,
                Condicion = condicion,
                Observacion = solicitud.Observacion?.Trim()
            },
            actor.IdUsuario,
            reloj.AhoraUtc,
            uuid,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "epps",
                codigo,
                AccionesAuditoria.Crear,
                "estado",
                null,
                EstadosEpp.Disponible,
                actor.IpOrigen,
                uuid),
            cancelacion);

        return Resultado<RespuestaEpp>.Exito(creado);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaEpp>>> ConsultarExistenciasAsync(
        int? idTipoEpp = null,
        string? estado = null,
        string? condicion = null,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioEpp);
        if (!autorizacion.EsExitoso)
        {
            var alt = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarHistorialEpp);
            if (!alt.EsExitoso)
            {
                return Resultado<IReadOnlyList<RespuestaEpp>>.Fallo(autorizacion.Error!);
            }
        }

        string? estadoNorm = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (estadoNorm is not null && !EstadosEpp.Todos.Contains(estadoNorm))
        {
            return Resultado<IReadOnlyList<RespuestaEpp>>.Fallo(
                ErrorNegocio.Validacion("estado_epp_invalido", "El filtro de estado de EPP no es válido."));
        }

        string? condicionNorm = string.IsNullOrWhiteSpace(condicion) ? null : condicion.Trim().ToUpperInvariant();
        if (condicionNorm is not null && !CondicionesEpp.Todas.Contains(condicionNorm))
        {
            return Resultado<IReadOnlyList<RespuestaEpp>>.Fallo(
                ErrorNegocio.Validacion("condicion_epp_invalida", "El filtro de condición de EPP no es válido."));
        }

        return Resultado<IReadOnlyList<RespuestaEpp>>.Exito(
            await puertoEpp.ConsultarExistenciasEppAsync(idTipoEpp, estadoNorm, condicionNorm, cancelacion));
    }

    public async Task<Resultado<IReadOnlyList<ResumenExistenciaTipoEpp>>> ConsultarResumenExistenciasAsync(
        int? idTipoEpp = null,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioEpp);
        if (!autorizacion.EsExitoso)
        {
            var alt = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarReportesEpp);
            if (!alt.EsExitoso)
            {
                return Resultado<IReadOnlyList<ResumenExistenciaTipoEpp>>.Fallo(autorizacion.Error!);
            }
        }

        return Resultado<IReadOnlyList<ResumenExistenciaTipoEpp>>.Exito(
            await puertoEpp.ConsultarResumenExistenciasAsync(idTipoEpp, cancelacion));
    }

    public async Task<Resultado<RespuestaEntregaEpp>> RegistrarEntregaAsync(
        SolicitudRegistrarEntregaEpp solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.RegistrarEntregasEpp);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(autorizacion.Error!);
        }

        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        var existenteUuid = await puertoEpp.ObtenerEntregaPorUuidAsync(uuid, cancelacion);
        if (existenteUuid is not null)
        {
            var alcancePrevio = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.RegistrarEntregasEpp, existenteUuid.IdObra);
            if (!alcancePrevio.EsExitoso) return Resultado<RespuestaEntregaEpp>.Fallo(alcancePrevio.Error!);
            if (existenteUuid.IdEpp != solicitud.IdEpp || existenteUuid.IdAsignacion != solicitud.IdAsignacion || existenteUuid.EntregadoEn != solicitud.EntregadoEn)
                return Resultado<RespuestaEntregaEpp>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado", "El UUID ya está asociado a una operación con datos diferentes."));
            return Resultado<RespuestaEntregaEpp>.Exito(existenteUuid with { FueIdempotente = true });
        }

        var contextoAsignacion = await puertoEpp.ObtenerContextoAsignacionAsync(solicitud.IdAsignacion, cancelacion);
        if (contextoAsignacion is null)
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(
                ErrorNegocio.NoEncontrado("asignacion_no_encontrada", "La asignación del trabajador a la obra no existe."));
        }

        var alcanceEntrega = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.RegistrarEntregasEpp, contextoAsignacion.Value.IdObra);
        if (!alcanceEntrega.EsExitoso) return Resultado<RespuestaEntregaEpp>.Fallo(alcanceEntrega.Error!);
        var obraOperativa = await politicaObra.ExigirActivaAsync(contextoAsignacion.Value.IdObra, cancelacion);
        if (!obraOperativa.EsExitoso) return Resultado<RespuestaEntregaEpp>.Fallo(obraOperativa.Error!);
        if (!contextoAsignacion.Value.EsVigenteEn(DateOnly.FromDateTime(solicitud.EntregadoEn)))
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(
                ErrorNegocio.Conflicto("trabajador_no_activo", "Solo se puede entregar EPP a un trabajador activo con asignación vigente en la obra."));
        }

        var epp = await puertoEpp.ObtenerEppPorIdAsync(solicitud.IdEpp, cancelacion);
        if (epp is null)
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(
                ErrorNegocio.NoEncontrado("epp_no_encontrado", "La unidad de EPP indicada no existe."));
        }

        if (epp.Estado != EstadosEpp.Disponible)
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(
                ErrorNegocio.Conflicto("epp_no_disponible", "La unidad de EPP no se encuentra disponible para entrega."));
        }

        var condicionEntrega = string.IsNullOrWhiteSpace(solicitud.CondicionEntrega)
            ? epp.Condicion
            : solicitud.CondicionEntrega.Trim().ToUpperInvariant();
        if (!CondicionesEpp.Todas.Contains(condicionEntrega))
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(
                ErrorNegocio.Validacion("condicion_entrega_invalida", "La condición de entrega del EPP no es válida."));
        }

        var actor = autorizacion.Valor!;
        var ahora = reloj.AhoraUtc;
        var sincronizadoEn = solicitud.EsSincronizacionDiferida ? ahora : (DateTime?)null;

        var entrega = await puertoEpp.RegistrarEntregaEppAsync(
            solicitud,
            condicionEntrega,
            actor.IdUsuario,
            uuid,
            ahora,
            sincronizadoEn,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "entregas_epp",
                uuid.ToString(),
                AccionesAuditoria.Crear,
                "id_epp",
                EstadosEpp.Disponible,
                EstadosEpp.Entregado,
                actor.IpOrigen,
                uuid),
            cancelacion);

        return Resultado<RespuestaEntregaEpp>.Exito(entrega);
    }

    public async Task<Resultado<RespuestaEntregaEpp>> DevolverEppAsync(
        long idEntregaEpp,
        SolicitudDevolverEpp solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.RegistrarEntregasEpp);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(autorizacion.Error!);
        }

        var entrega = await puertoEpp.ObtenerEntregaPorIdAsync(idEntregaEpp, cancelacion);
        if (entrega is null)
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(
                ErrorNegocio.NoEncontrado("entrega_epp_no_encontrada", "El registro de entrega de EPP no existe."));
        }

        var alcance = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.RegistrarEntregasEpp, entrega.IdObra);
        if (!alcance.EsExitoso) return Resultado<RespuestaEntregaEpp>.Fallo(alcance.Error!);

        if (solicitud.DevueltoEn < entrega.EntregadoEn)
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(
                ErrorNegocio.Validacion("fecha_devolucion_invalida", "La fecha de devolución no puede ser anterior a la fecha de entrega."));
        }

        var condicion = solicitud.CondicionDevolucion?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CondicionesEpp.Todas.Contains(condicion))
        {
            return Resultado<RespuestaEntregaEpp>.Fallo(
                ErrorNegocio.Validacion("condicion_devolucion_invalida", "La condición de devolución debe ser NUEVO, BUENO, DESGASTADO o DANADO."));
        }

        var uuid = solicitud.UuidRegistro is { } identificador && identificador != Guid.Empty ? identificador : Guid.NewGuid();
        var previo = await puertoEpp.ObtenerMovimientoPorUuidAsync(uuid, cancelacion);
        var observacion = string.IsNullOrWhiteSpace(solicitud.Observacion)
            ? $"Devolucion en condicion {condicion}" : solicitud.Observacion.Trim();
        if (previo is not null)
        {
            if (previo.TipoMovimiento != TiposMovimientoEpp.Devolucion || previo.IdEntregaEpp != idEntregaEpp
                || previo.FechaHora != solicitud.DevueltoEn || previo.Observacion != observacion
                || entrega.CondicionDevolucion != condicion || entrega.DevueltoEn != solicitud.DevueltoEn)
                return Resultado<RespuestaEntregaEpp>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado",
                    "El UUID ya está asociado a una operación con datos diferentes."));
            return Resultado<RespuestaEntregaEpp>.Exito(entrega with { FueIdempotente = true });
        }
        if (entrega.Estado != EstadosEntregaEpp.Activa)
            return Resultado<RespuestaEntregaEpp>.Fallo(ErrorNegocio.Conflicto(
                "entrega_epp_no_activa", "La entrega de EPP ya fue cerrada anteriormente."));

        var actor = autorizacion.Valor!;
        var devuelta = await puertoEpp.RegistrarDevolucionEppAsync(
            idEntregaEpp,
            solicitud with { CondicionDevolucion = condicion, Observacion = observacion, UuidRegistro = uuid },
            actor.IdUsuario,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "entregas_epp",
                idEntregaEpp.ToString(),
                AccionesAuditoria.Modificar,
                "estado",
                entrega.Estado,
                EstadosEntregaEpp.Devuelta,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaEntregaEpp>.Exito(devuelta!);
    }

    public Task<Resultado<RespuestaReposicionEpp>> RegistrarReposicionAsync(
        SolicitudRegistrarReposicionEpp solicitud, CancellationToken cancelacion = default) =>
        ProcesarReposicionAsync(solicitud, null, cancelacion);

    public async Task<Resultado<RespuestaReposicionEpp>> EjecutarReposicionAsync(long idReposicion,
        SolicitudEjecutarReposicionEpp solicitud, CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarReposicionesEpp);
        if (!permiso.EsExitoso) return Resultado<RespuestaReposicionEpp>.Fallo(permiso.Error!);
        var existente = await puertoEpp.ObtenerReposicionPorIdAsync(idReposicion, cancelacion);
        if (existente is null) return Resultado<RespuestaReposicionEpp>.Fallo(ErrorNegocio.NoEncontrado("reposicion_no_encontrada", "La reposición no existe."));
        if (!permiso.Valor!.TieneAccesoAObra(existente.IdObra))
            return Resultado<RespuestaReposicionEpp>.Fallo(ErrorNegocio.Prohibido("obra_fuera_de_alcance", "La obra no está autorizada."));
        if (existente.Estado == EstadosReposicionEpp.Ejecutada && existente.IdEntregaNueva is { } entregaNueva)
        {
            var entrega = await puertoEpp.ObtenerEntregaPorIdAsync(entregaNueva, cancelacion);
            return entrega?.IdEpp == solicitud.IdEppSustituto
                ? Resultado<RespuestaReposicionEpp>.Exito(existente with { FueIdempotente = true })
                : Resultado<RespuestaReposicionEpp>.Fallo(ErrorNegocio.Conflicto("reposicion_ya_ejecutada", "La reposición ya se ejecutó con otro equipo."));
        }
        if (existente.Estado != EstadosReposicionEpp.Pendiente)
            return Resultado<RespuestaReposicionEpp>.Fallo(ErrorNegocio.Conflicto("reposicion_no_pendiente", "La reposición ya fue resuelta."));
        return await ProcesarReposicionAsync(new(existente.IdEntregaOrigen, solicitud.IdEppSustituto,
            existente.Motivo, existente.Descripcion, solicitud.EquipoDevuelto, solicitud.EjecutadaEn ?? reloj.AhoraUtc,
            existente.UuidRegistro), idReposicion, cancelacion);
    }

    private async Task<Resultado<RespuestaReposicionEpp>> ProcesarReposicionAsync(
        SolicitudRegistrarReposicionEpp solicitud, long? idReposicionExistente,
        CancellationToken cancelacion)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarReposicionesEpp);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaReposicionEpp>.Fallo(autorizacion.Error!);
        }

        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        var existenteUuid = await puertoEpp.ObtenerReposicionPorUuidAsync(uuid, cancelacion);
        if (existenteUuid is not null && idReposicionExistente is null)
        {
            var alcancePrevio = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.GestionarReposicionesEpp, existenteUuid.IdObra);
            if (!alcancePrevio.EsExitoso) return Resultado<RespuestaReposicionEpp>.Fallo(alcancePrevio.Error!);
            if (existenteUuid.IdEntregaOrigen != solicitud.IdEntregaOrigen || existenteUuid.Motivo != solicitud.Motivo?.Trim().ToUpperInvariant() || existenteUuid.Descripcion != solicitud.Descripcion?.Trim() || existenteUuid.EquipoDevuelto != solicitud.EquipoDevuelto)
                return Resultado<RespuestaReposicionEpp>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado", "El UUID ya está asociado a una operación con datos diferentes."));
            return Resultado<RespuestaReposicionEpp>.Exito(existenteUuid with { FueIdempotente = true });
        }

        var motivo = solicitud.Motivo?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!MotivosReposicionEpp.Todos.Contains(motivo))
        {
            return Resultado<RespuestaReposicionEpp>.Fallo(
                ErrorNegocio.Validacion("motivo_reposicion_invalido", "El motivo de reposición debe ser DANO, PERDIDA, DESGASTE, VENCIMIENTO u OTRO."));
        }

        var descripcion = solicitud.Descripcion?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Length > 300)
        {
            return Resultado<RespuestaReposicionEpp>.Fallo(
                ErrorNegocio.Validacion("descripcion_reposicion_invalida", "La descripción de la reposición es obligatoria y no debe exceder 300 caracteres."));
        }

        var entregaOrigen = await puertoEpp.ObtenerEntregaPorIdAsync(solicitud.IdEntregaOrigen, cancelacion);
        if (entregaOrigen is null)
        {
            return Resultado<RespuestaReposicionEpp>.Fallo(
                ErrorNegocio.NoEncontrado("entrega_origen_no_encontrada", "La entrega de EPP original no existe."));
        }

        if (entregaOrigen.Estado != EstadosEntregaEpp.Activa)
        {
            return Resultado<RespuestaReposicionEpp>.Fallo(
                ErrorNegocio.Conflicto("entrega_origen_no_activa", "La entrega original no se encuentra activa para reposición."));
        }

        var alcance = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.GestionarReposicionesEpp, entregaOrigen.IdObra);
        if (!alcance.EsExitoso) return Resultado<RespuestaReposicionEpp>.Fallo(alcance.Error!);
        if (solicitud.SolicitadaEn < entregaOrigen.EntregadoEn)
            return Resultado<RespuestaReposicionEpp>.Fallo(ErrorNegocio.Validacion("fecha_reposicion_invalida", "La reposición no puede ser anterior a la entrega original."));

        // CRQ-0002 (CET-15 / CET-36): En reposición por daño o desgaste se exige devolución/comprobación del equipo dañado.
        if (motivo is (MotivosReposicionEpp.Dano or MotivosReposicionEpp.Desgaste) && !solicitud.EquipoDevuelto)
        {
            return Resultado<RespuestaReposicionEpp>.Fallo(
                ErrorNegocio.Validacion(
                    "comprobacion_devolucion_requerida",
                    "En reposiciones por daño o desgaste se debe comprobar y registrar la devolución del equipo anterior (equipoDevuelto = true)."));
        }

        if (solicitud.IdEppSustituto is int idSustituto)
        {
            var obraOperativa = await politicaObra.ExigirActivaAsync(entregaOrigen.IdObra, cancelacion);
            if (!obraOperativa.EsExitoso) return Resultado<RespuestaReposicionEpp>.Fallo(obraOperativa.Error!);
            var eppNuevo = await puertoEpp.ObtenerEppPorIdAsync(idSustituto, cancelacion);
            if (eppNuevo is null)
            {
                return Resultado<RespuestaReposicionEpp>.Fallo(
                    ErrorNegocio.NoEncontrado("epp_sustituto_no_encontrado", "La unidad de EPP sustituta no existe."));
            }

            if (eppNuevo.Estado != EstadosEpp.Disponible)
            {
                return Resultado<RespuestaReposicionEpp>.Fallo(
                    ErrorNegocio.Conflicto("epp_sustituto_no_disponible", "La unidad de EPP sustituta no está disponible."));
            }

            if (eppNuevo.IdTipoEpp != entregaOrigen.IdTipoEpp)
                return Resultado<RespuestaReposicionEpp>.Fallo(ErrorNegocio.Validacion("tipo_epp_incompatible", "El equipo sustituto debe ser del mismo tipo de protección."));
        }

        var actor = autorizacion.Valor!;
        var ahora = reloj.AhoraUtc;
        var sincronizadoEn = solicitud.EsSincronizacionDiferida ? ahora : (DateTime?)null;

        var reposicion = await puertoEpp.RegistrarReposicionEppAsync(
            solicitud with { Motivo = motivo, Descripcion = descripcion },
            actor.IdUsuario,
            ahora,
            uuid,
            sincronizadoEn,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "reposiciones_epp",
                uuid.ToString(),
                AccionesAuditoria.Crear,
                "motivo",
                null,
                motivo,
                actor.IpOrigen,
                uuid),
            idReposicionExistente,
            cancelacion);

        return Resultado<RespuestaReposicionEpp>.Exito(reposicion);
    }

    public async Task<Resultado<IReadOnlyList<EventoHistorialEppTrabajador>>> ConsultarHistorialTrabajadorAsync(
        int idTrabajador,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarHistorialEpp);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<EventoHistorialEppTrabajador>>.Fallo(autorizacion.Error!);
        }

        var usuario = autorizacion.Valor!;
        IReadOnlySet<int>? obrasPermitidas = usuario.TieneAlcanceGlobal ? null : usuario.ObrasAsignadas;
        var historial = await puertoEpp.ConsultarHistorialPorTrabajadorAsync(idTrabajador, obrasPermitidas, cancelacion);
        return Resultado<IReadOnlyList<EventoHistorialEppTrabajador>>.Exito(historial);
    }

    public async Task<Resultado<IReadOnlyList<ItemReporteEpp>>> GenerarReporteAsync(
        FiltroReporteEpp filtro,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarReportesEpp);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<ItemReporteEpp>>.Fallo(autorizacion.Error!);
        }

        if (filtro.FechaDesde is DateOnly desde && filtro.FechaHasta is DateOnly hasta && hasta < desde)
        {
            return Resultado<IReadOnlyList<ItemReporteEpp>>.Fallo(
                ErrorNegocio.Validacion("periodo_reporte_invalido", "La fecha final del reporte no puede ser anterior a la fecha inicial."));
        }

        var usuario = autorizacion.Valor!;
        if (filtro.IdObra is int idObra && !usuario.TieneAccesoAObra(idObra))
        {
            return Resultado<IReadOnlyList<ItemReporteEpp>>.Fallo(
                ErrorNegocio.Prohibido("obra_fuera_de_alcance", "No tiene autorización para consultar reportes de esta obra."));
        }

        IReadOnlySet<int>? obrasPermitidas = usuario.TieneAlcanceGlobal ? null : usuario.ObrasAsignadas;
        var reporte = await puertoEpp.GenerarReporteEppAsync(filtro, obrasPermitidas, cancelacion);
        return Resultado<IReadOnlyList<ItemReporteEpp>>.Exito(reporte);
    }
    public Task<Resultado<RespuestaTipoEpp>> ObtenerTipoEppPorIdAsync(int idTipoEpp, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.GestionarInventarioEpp, CodigosPermiso.ConsultarReportesEpp],
            () => puertoEpp.ObtenerTipoEppPorIdAsync(idTipoEpp, cancelacion));

    public Task<Resultado<IReadOnlyList<RespuestaEntregaEpp>>> ListarEntregasAsync(int? idObra,
        int? idTrabajador, string? estado, CancellationToken cancelacion = default)
    {
        var normalizado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (normalizado is not null && !EstadosEntregaEpp.Todos.Contains(normalizado))
            return Task.FromResult(Resultado<IReadOnlyList<RespuestaEntregaEpp>>.Fallo(
                ErrorNegocio.Validacion("estado_entrega_invalido", "El estado de entrega no es válido.")));
        return ConsultaAutorizada.ListarAsync(contextoUsuario,
            [CodigosPermiso.RegistrarEntregasEpp, CodigosPermiso.ConsultarHistorialEpp], idObra,
            obras => puertoEpp.ListarEntregasAsync(idObra, idTrabajador, normalizado, obras, cancelacion));
    }

    public Task<Resultado<IReadOnlyList<RespuestaReposicionEpp>>> ListarReposicionesAsync(int? idObra,
        string? estado, CancellationToken cancelacion = default)
    {
        var normalizado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (normalizado is not null && !EstadosReposicionEpp.Todos.Contains(normalizado))
            return Task.FromResult(Resultado<IReadOnlyList<RespuestaReposicionEpp>>.Fallo(
                ErrorNegocio.Validacion("estado_reposicion_invalido", "El estado de reposición no es válido.")));
        return ConsultaAutorizada.ListarAsync(contextoUsuario,
            [CodigosPermiso.GestionarReposicionesEpp, CodigosPermiso.ConsultarHistorialEpp], idObra,
            obras => puertoEpp.ListarReposicionesAsync(idObra, normalizado, obras, cancelacion));
    }

    public Task<Resultado<RespuestaEpp>> ObtenerEppPorIdAsync(int idEpp, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.GestionarInventarioEpp, CodigosPermiso.ConsultarReportesEpp],
            () => puertoEpp.ObtenerEppPorIdAsync(idEpp, cancelacion));

    public Task<Resultado<RespuestaEntregaEpp>> ObtenerEntregaPorIdAsync(long idEntrega, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.RegistrarEntregasEpp, CodigosPermiso.ConsultarHistorialEpp],
            () => puertoEpp.ObtenerEntregaPorIdAsync(idEntrega, cancelacion), r => r.IdObra);

    public Task<Resultado<RespuestaReposicionEpp>> ObtenerReposicionPorIdAsync(long idReposicion, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.GestionarReposicionesEpp, CodigosPermiso.ConsultarHistorialEpp],
            () => puertoEpp.ObtenerReposicionPorIdAsync(idReposicion, cancelacion), r => r.IdObra);
}
