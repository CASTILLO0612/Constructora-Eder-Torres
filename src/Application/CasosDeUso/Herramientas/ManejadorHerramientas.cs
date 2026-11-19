using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Herramientas;

public sealed partial class ManejadorHerramientas(
    IPuertoHerramientas puertoHerramientas,
    IContextoUsuarioActual contextoUsuario,
    IRelojSistema reloj,
    PoliticaOperacionesObra politicaObra)
{
    public async Task<Resultado<RespuestaCategoriaHerramienta>> CrearCategoriaAsync(
        SolicitudCrearCategoriaHerramienta solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioHerramientas);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaCategoriaHerramienta>.Fallo(autorizacion.Error!);
        }

        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 80)
        {
            return Resultado<RespuestaCategoriaHerramienta>.Fallo(
                ErrorNegocio.Validacion("nombre_categoria_invalido", "El nombre de la categoría es obligatorio y no debe exceder 80 caracteres."));
        }

        if (await puertoHerramientas.ExisteCategoriaPorNombreAsync(nombre, cancelacion))
        {
            return Resultado<RespuestaCategoriaHerramienta>.Fallo(
                ErrorNegocio.Conflicto("categoria_herramienta_duplicada", "Ya existe una categoría de herramienta con ese nombre."));
        }

        var creada = await puertoHerramientas.CrearCategoriaAsync(
            new SolicitudCrearCategoriaHerramienta(nombre, solicitud.Descripcion?.Trim()),
            cancelacion);
        return Resultado<RespuestaCategoriaHerramienta>.Exito(creada);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaCategoriaHerramienta>>> ListarCategoriasAsync(
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarAsignacionesHerramientas);
        if (!autorizacion.EsExitoso)
        {
            var alt = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioHerramientas);
            if (!alt.EsExitoso)
            {
                return Resultado<IReadOnlyList<RespuestaCategoriaHerramienta>>.Fallo(autorizacion.Error!);
            }
        }

        return Resultado<IReadOnlyList<RespuestaCategoriaHerramienta>>.Exito(
            await puertoHerramientas.ListarCategoriasAsync(cancelacion));
    }

    public async Task<Resultado<RespuestaTipoHerramienta>> CrearTipoAsync(
        SolicitudCrearTipoHerramienta solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioHerramientas);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaTipoHerramienta>.Fallo(autorizacion.Error!);
        }

        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
        {
            return Resultado<RespuestaTipoHerramienta>.Fallo(
                ErrorNegocio.Validacion("nombre_tipo_herramienta_invalido", "El nombre del tipo de herramienta es obligatorio y no debe exceder 100 caracteres."));
        }

        var categoria = await puertoHerramientas.ObtenerCategoriaPorIdAsync(solicitud.IdCategoria, cancelacion);
        if (categoria is null || !categoria.Activo)
        {
            return Resultado<RespuestaTipoHerramienta>.Fallo(
                ErrorNegocio.NoEncontrado("categoria_no_encontrada", "La categoría de herramienta indicada no existe o está inactiva."));
        }

        if (await puertoHerramientas.ExisteTipoPorNombreEnCategoriaAsync(solicitud.IdCategoria, nombre, cancelacion))
        {
            return Resultado<RespuestaTipoHerramienta>.Fallo(
                ErrorNegocio.Conflicto("tipo_herramienta_duplicado", "Ya existe un tipo de herramienta con ese nombre en la categoría indicada."));
        }

        var creado = await puertoHerramientas.CrearTipoAsync(
            solicitud with { Nombre = nombre, Descripcion = solicitud.Descripcion?.Trim() },
            cancelacion);
        return Resultado<RespuestaTipoHerramienta>.Exito(creado);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaTipoHerramienta>>> ListarTiposAsync(
        int? idCategoria = null,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarAsignacionesHerramientas);
        if (!autorizacion.EsExitoso)
        {
            var alt = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioHerramientas);
            if (!alt.EsExitoso)
            {
                return Resultado<IReadOnlyList<RespuestaTipoHerramienta>>.Fallo(autorizacion.Error!);
            }
        }

        return Resultado<IReadOnlyList<RespuestaTipoHerramienta>>.Exito(
            await puertoHerramientas.ListarTiposAsync(idCategoria, cancelacion));
    }

    public async Task<Resultado<RespuestaHerramienta>> RegistrarHerramientaAsync(
        SolicitudRegistrarHerramienta solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioHerramientas);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaHerramienta>.Fallo(autorizacion.Error!);
        }

        var codigo = solicitud.CodigoInterno?.Trim().ToUpperInvariant() ?? string.Empty;
        var serie = string.IsNullOrWhiteSpace(solicitud.NumeroSerie) ? null : solicitud.NumeroSerie.Trim().ToUpperInvariant();
        var condicion = string.IsNullOrWhiteSpace(solicitud.Condicion)
            ? CondicionesHerramienta.Nueva
            : solicitud.Condicion.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 30)
        {
            return Resultado<RespuestaHerramienta>.Fallo(
                ErrorNegocio.Validacion("codigo_herramienta_invalido", "El código interno de la herramienta es obligatorio y no debe exceder 30 caracteres."));
        }

        if (!CondicionesHerramienta.Todas.Contains(condicion))
        {
            return Resultado<RespuestaHerramienta>.Fallo(
                ErrorNegocio.Validacion("condicion_herramienta_invalida", "La condición de la herramienta debe ser NUEVA, BUENA, DESGASTADA o DANADA."));
        }

        var tipo = await puertoHerramientas.ObtenerTipoPorIdAsync(solicitud.IdTipoHerramienta, cancelacion);
        if (tipo is null || !tipo.Activo)
        {
            return Resultado<RespuestaHerramienta>.Fallo(
                ErrorNegocio.NoEncontrado("tipo_herramienta_no_encontrado", "El tipo de herramienta indicado no existe o está inactivo."));
        }

        if (await puertoHerramientas.ExisteHerramientaPorCodigoAsync(codigo, cancelacion))
        {
            return Resultado<RespuestaHerramienta>.Fallo(
                ErrorNegocio.Conflicto("codigo_herramienta_duplicado", "Ya existe una herramienta registrada con ese código interno."));
        }

        if (serie is not null && await puertoHerramientas.ExisteHerramientaPorSerieAsync(serie, cancelacion))
        {
            return Resultado<RespuestaHerramienta>.Fallo(
                ErrorNegocio.Conflicto("serie_herramienta_duplicada", "Ya existe una herramienta registrada con ese número de serie."));
        }

        var actor = autorizacion.Valor!;
        var creada = await puertoHerramientas.RegistrarHerramientaAsync(
            solicitud with
            {
                CodigoInterno = codigo,
                NumeroSerie = serie,
                Marca = solicitud.Marca?.Trim(),
                Modelo = solicitud.Modelo?.Trim(),
                Condicion = condicion
            },
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "herramientas",
                codigo,
                AccionesAuditoria.Crear,
                "estado",
                null,
                EstadosHerramienta.Disponible,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaHerramienta>.Exito(creada);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaHerramienta>>> ConsultarHerramientasAsync(
        int? idTipoHerramienta = null,
        string? estado = null,
        string? condicion = null,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarAsignacionesHerramientas);
        if (!autorizacion.EsExitoso)
        {
            var alt = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioHerramientas);
            if (!alt.EsExitoso)
            {
                return Resultado<IReadOnlyList<RespuestaHerramienta>>.Fallo(autorizacion.Error!);
            }
        }

        string? estadoNorm = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (estadoNorm is not null && !EstadosHerramienta.Todos.Contains(estadoNorm))
        {
            return Resultado<IReadOnlyList<RespuestaHerramienta>>.Fallo(
                ErrorNegocio.Validacion("estado_herramienta_invalido", "El filtro de estado de herramienta no es válido."));
        }

        string? condicionNorm = string.IsNullOrWhiteSpace(condicion) ? null : condicion.Trim().ToUpperInvariant();
        if (condicionNorm is not null && !CondicionesHerramienta.Todas.Contains(condicionNorm))
        {
            return Resultado<IReadOnlyList<RespuestaHerramienta>>.Fallo(
                ErrorNegocio.Validacion("condicion_herramienta_invalida", "El filtro de condición de herramienta no es válido."));
        }

        return Resultado<IReadOnlyList<RespuestaHerramienta>>.Exito(
            await puertoHerramientas.ListarHerramientasAsync(idTipoHerramienta, estadoNorm, condicionNorm, cancelacion));
    }

    public async Task<Resultado<RespuestaAsignacionHerramienta>> AsignarHerramientaAsync(
        SolicitudAsignarHerramienta solicitud,
        CancellationToken cancelacion = default)
    {
        var validacionPermiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarAsignacionesHerramientas);
        if (!validacionPermiso.EsExitoso)
        {
            return Resultado<RespuestaAsignacionHerramienta>.Fallo(validacionPermiso.Error!);
        }

        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        var existenteUuid = await puertoHerramientas.ObtenerAsignacionPorUuidAsync(uuid, cancelacion);
        if (existenteUuid is not null)
        {
            var authExistente = AutorizadorOperacion.ExigirPermisoYObra(
                contextoUsuario,
                CodigosPermiso.GestionarAsignacionesHerramientas,
                existenteUuid.IdObra);
            if (!authExistente.EsExitoso)
            {
                return Resultado<RespuestaAsignacionHerramienta>.Fallo(authExistente.Error!);
            }

            if (existenteUuid.IdHerramienta != solicitud.IdHerramienta || existenteUuid.IdAsignacionTrabajadorObra != solicitud.IdAsignacionTrabajadorObra || existenteUuid.EntregadaEn != solicitud.EntregadaEn)
                return Resultado<RespuestaAsignacionHerramienta>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado", "El UUID ya está asociado a una operación con datos diferentes."));
            return Resultado<RespuestaAsignacionHerramienta>.Exito(existenteUuid with { FueIdempotente = true });
        }

        var ctxAsignacion = await puertoHerramientas.ObtenerContextoAsignacionTrabajadorAsync(
            solicitud.IdAsignacionTrabajadorObra,
            cancelacion);
        if (ctxAsignacion is null)
        {
            return Resultado<RespuestaAsignacionHerramienta>.Fallo(
                ErrorNegocio.NoEncontrado("asignacion_trabajador_no_encontrada", "La asignación del trabajador a la obra no existe."));
        }

        var autorizacionObra = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.GestionarAsignacionesHerramientas,
            ctxAsignacion.Value.IdObra);
        if (!autorizacionObra.EsExitoso)
        {
            return Resultado<RespuestaAsignacionHerramienta>.Fallo(autorizacionObra.Error!);
        }

        var obraOperativa = await politicaObra.ExigirActivaAsync(ctxAsignacion.Value.IdObra, cancelacion);
        if (!obraOperativa.EsExitoso) return Resultado<RespuestaAsignacionHerramienta>.Fallo(obraOperativa.Error!);
        if (!ctxAsignacion.Value.EsVigenteEn(DateOnly.FromDateTime(solicitud.EntregadaEn)))
        {
            return Resultado<RespuestaAsignacionHerramienta>.Fallo(
                ErrorNegocio.Conflicto("trabajador_no_activo", "Solo se pueden asignar herramientas a trabajadores activos con asignación vigente en la obra."));
        }

        var herramienta = await puertoHerramientas.ObtenerHerramientaPorIdAsync(solicitud.IdHerramienta, cancelacion);
        if (herramienta is null)
        {
            return Resultado<RespuestaAsignacionHerramienta>.Fallo(
                ErrorNegocio.NoEncontrado("herramienta_no_encontrada", "La herramienta indicada no existe."));
        }

        if (herramienta.Estado != EstadosHerramienta.Disponible
            || await puertoHerramientas.TieneAsignacionHerramientaActivaAsync(solicitud.IdHerramienta, cancelacion))
        {
            return Resultado<RespuestaAsignacionHerramienta>.Fallo(
                ErrorNegocio.Conflicto(
                    "herramienta_no_disponible",
                    "La herramienta no está disponible o ya posee una asignación activa."));
        }

        var condicionEntrega = string.IsNullOrWhiteSpace(solicitud.CondicionEntrega)
            ? herramienta.Condicion
            : solicitud.CondicionEntrega.Trim().ToUpperInvariant();
        if (!CondicionesHerramienta.Todas.Contains(condicionEntrega))
        {
            return Resultado<RespuestaAsignacionHerramienta>.Fallo(
                ErrorNegocio.Validacion("condicion_entrega_invalida", "La condición de entrega de la herramienta no es válida."));
        }

        var actor = autorizacionObra.Valor!;
        var ahora = reloj.AhoraUtc;
        var sincronizadoEn = solicitud.EsSincronizacionDiferida ? ahora : (DateTime?)null;

        var asignacion = await puertoHerramientas.AsignarHerramientaAsync(
            solicitud,
            condicionEntrega,
            actor.IdUsuario,
            uuid,
            ahora,
            sincronizadoEn,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "asignaciones_herramienta",
                uuid.ToString(),
                AccionesAuditoria.Crear,
                "id_herramienta",
                EstadosHerramienta.Disponible,
                EstadosHerramienta.Asignada,
                actor.IpOrigen,
                uuid),
            cancelacion);

        return Resultado<RespuestaAsignacionHerramienta>.Exito(asignacion);
    }

    public async Task<Resultado<RespuestaIncidenciaHerramienta>> RegistrarDevolucionOIncidenciaAsync(
        SolicitudRegistrarEventoHerramienta solicitud,
        CancellationToken cancelacion = default)
    {
        var validacionPermiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.RegistrarIncidenciasHerramientas);
        if (!validacionPermiso.EsExitoso)
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(validacionPermiso.Error!);
        }

        var uuid = solicitud.UuidRegistro is { } g && g != Guid.Empty ? g : Guid.NewGuid();
        var existenteUuid = await puertoHerramientas.ObtenerIncidenciaPorUuidAsync(uuid, cancelacion);
        if (existenteUuid is not null)
        {
            var authExistente = AutorizadorOperacion.ExigirPermisoYObra(
                contextoUsuario,
                CodigosPermiso.RegistrarIncidenciasHerramientas,
                existenteUuid.IdObra);
            if (!authExistente.EsExitoso)
            {
                return Resultado<RespuestaIncidenciaHerramienta>.Fallo(authExistente.Error!);
            }

            if (existenteUuid.IdAsignacionHerramienta != solicitud.IdAsignacionHerramienta || existenteUuid.TipoIncidencia != solicitud.TipoIncidencia?.Trim().ToUpperInvariant() || existenteUuid.FechaHora != solicitud.FechaHora || existenteUuid.Descripcion != solicitud.Descripcion?.Trim())
                return Resultado<RespuestaIncidenciaHerramienta>.Fallo(ErrorNegocio.Conflicto("uuid_reutilizado", "El UUID ya está asociado a una operación con datos diferentes."));
            return Resultado<RespuestaIncidenciaHerramienta>.Exito(existenteUuid with { FueIdempotente = true });
        }

        var tipo = solicitud.TipoIncidencia?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!TiposIncidenciaHerramienta.Todos.Contains(tipo))
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
                ErrorNegocio.Validacion("tipo_incidencia_herramienta_invalido", "El tipo de evento debe ser DANO, PERDIDA, DEMORA, DEVOLUCION u OTRA."));
        }

        var descripcion = solicitud.Descripcion?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Length > 500)
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
                ErrorNegocio.Validacion("descripcion_incidencia_invalida", "La descripción del evento es obligatoria y no debe exceder 500 caracteres."));
        }

        if (solicitud.CostoEstimado is < 0m)
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
                ErrorNegocio.Validacion("costo_estimado_invalido", "El costo estimado no puede ser negativo."));
        }

        var asignacion = await puertoHerramientas.ObtenerAsignacionPorIdAsync(solicitud.IdAsignacionHerramienta, cancelacion);
        if (asignacion is null)
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
                ErrorNegocio.NoEncontrado("asignacion_herramienta_no_encontrada", "La asignación de herramienta indicada no existe."));
        }

        var autorizacionObra = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.RegistrarIncidenciasHerramientas,
            asignacion.IdObra);
        if (!autorizacionObra.EsExitoso)
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(autorizacionObra.Error!);
        }

        if (asignacion.Estado != EstadosAsignacionHerramienta.Activa)
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
                ErrorNegocio.Conflicto("asignacion_herramienta_no_activa", "Se requiere una asignación activa para registrar devolución, pérdida o incidencia."));
        }

        if (solicitud.FechaHora < asignacion.EntregadaEn)
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
                ErrorNegocio.Validacion("fecha_evento_invalida", "La fecha del evento no puede ser anterior a la fecha de entrega de la herramienta."));
        }

        var herramienta = await puertoHerramientas.ObtenerHerramientaPorIdAsync(asignacion.IdHerramienta, cancelacion);
        var condicionDevolucion = string.IsNullOrWhiteSpace(solicitud.CondicionDevolucion)
            ? herramienta!.Condicion
            : solicitud.CondicionDevolucion.Trim().ToUpperInvariant();

        if (!CondicionesHerramienta.Todas.Contains(condicionDevolucion))
        {
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
                ErrorNegocio.Validacion("condicion_devolucion_invalida", "La condición de la herramienta debe ser NUEVA, BUENA, DESGASTADA o DANADA."));
        }

        // Reglas CET-15 / CET-41:
        // - Devolución en buena condición libera a DISPONIBLE; si vuelve DANADA pasa a EN_REVISION.
        // - Pérdida cierra asignación en PERDIDA y retira herramienta de disponibles (PERDIDA).
        // - Daño pasa a EN_REVISION o RETIRADA según indicación.
        string nuevoEstadoAsignacion;
        string nuevoEstadoHerramienta;
        string nuevaCondicionHerramienta = condicionDevolucion;
        string estadoIncidencia;

        if (tipo == TiposIncidenciaHerramienta.Perdida)
        {
            nuevoEstadoAsignacion = EstadosAsignacionHerramienta.Perdida;
            nuevoEstadoHerramienta = EstadosHerramienta.Perdida;
            estadoIncidencia = EstadosIncidencia.Abierta;
        }
        else if (tipo == TiposIncidenciaHerramienta.Devolucion)
        {
            nuevoEstadoAsignacion = EstadosAsignacionHerramienta.Devuelta;
            nuevoEstadoHerramienta = condicionDevolucion == CondicionesHerramienta.Danada
                ? (solicitud.RetirarHerramienta ? EstadosHerramienta.Retirada : EstadosHerramienta.EnRevision)
                : EstadosHerramienta.Disponible;
            estadoIncidencia = EstadosIncidencia.Resuelta;
        }
        else if (tipo == TiposIncidenciaHerramienta.Dano)
        {
            nuevaCondicionHerramienta = CondicionesHerramienta.Danada;
            condicionDevolucion = CondicionesHerramienta.Danada;
            nuevoEstadoAsignacion = solicitud.CerrarAsignacion || solicitud.RetirarHerramienta
                ? EstadosAsignacionHerramienta.Devuelta
                : EstadosAsignacionHerramienta.Activa;
            nuevoEstadoHerramienta = solicitud.RetirarHerramienta
                ? EstadosHerramienta.Retirada
                : (solicitud.CerrarAsignacion ? EstadosHerramienta.EnRevision : EstadosHerramienta.Asignada);
            estadoIncidencia = string.IsNullOrWhiteSpace(solicitud.Resolucion)
                ? EstadosIncidencia.Abierta
                : EstadosIncidencia.Resuelta;
        }
        else
        {
            nuevoEstadoAsignacion = solicitud.CerrarAsignacion
                ? EstadosAsignacionHerramienta.Devuelta
                : EstadosAsignacionHerramienta.Activa;
            nuevoEstadoHerramienta = solicitud.CerrarAsignacion
                ? (condicionDevolucion == CondicionesHerramienta.Danada ? EstadosHerramienta.EnRevision : EstadosHerramienta.Disponible)
                : EstadosHerramienta.Asignada;
            estadoIncidencia = string.IsNullOrWhiteSpace(solicitud.Resolucion)
                ? EstadosIncidencia.Abierta
                : EstadosIncidencia.Resuelta;
        }

        var actor = autorizacionObra.Valor!;
        var resultado = await puertoHerramientas.RegistrarEventoHerramientaAsync(
            solicitud with
            {
                TipoIncidencia = tipo,
                Descripcion = descripcion,
                Resolucion = solicitud.Resolucion?.Trim()
            },
            nuevoEstadoAsignacion,
            nuevoEstadoAsignacion == EstadosAsignacionHerramienta.Devuelta ? condicionDevolucion : null,
            nuevoEstadoHerramienta,
            nuevaCondicionHerramienta,
            estadoIncidencia,
            actor.IdUsuario,
            uuid,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "incidencias_herramienta",
                uuid.ToString(),
                AccionesAuditoria.Crear,
                "estado_herramienta",
                herramienta!.Estado,
                nuevoEstadoHerramienta,
                actor.IpOrigen,
                uuid),
            cancelacion);

        return Resultado<RespuestaIncidenciaHerramienta>.Exito(resultado);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaAsignacionHerramienta>>> ListarAsignacionesAsync(
        int? idObra = null,
        bool soloActivas = false,
        CancellationToken cancelacion = default,
        string? estado = null)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarAsignacionesHerramientas);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaAsignacionHerramienta>>.Fallo(autorizacion.Error!);
        }

        var usuario = autorizacion.Valor!;
        if (idObra is int obra && !usuario.TieneAccesoAObra(obra))
        {
            return Resultado<IReadOnlyList<RespuestaAsignacionHerramienta>>.Fallo(
                ErrorNegocio.Prohibido("obra_fuera_de_alcance", "No tiene autorización para consultar las herramientas asignadas a esta obra."));
        }

        IReadOnlySet<int>? obrasPermitidas = usuario.TieneAlcanceGlobal ? null : usuario.ObrasAsignadas;
        var filtroEstado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (filtroEstado is not null && !EstadosAsignacionHerramienta.Todos.Contains(filtroEstado))
            return Resultado<IReadOnlyList<RespuestaAsignacionHerramienta>>.Fallo(
                ErrorNegocio.Validacion("estado_asignacion_invalido", "El estado de asignación de herramienta no es válido."));
        var lista = await puertoHerramientas.ListarAsignacionesHerramientaAsync(idObra, soloActivas, obrasPermitidas, cancelacion, filtroEstado);
        return Resultado<IReadOnlyList<RespuestaAsignacionHerramienta>>.Exito(lista);
    }
    public Task<Resultado<RespuestaCategoriaHerramienta>> ObtenerCategoriaPorIdAsync(int idCategoria, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.GestionarInventarioHerramientas],
            () => puertoHerramientas.ObtenerCategoriaPorIdAsync(idCategoria, cancelacion));

    public Task<Resultado<RespuestaTipoHerramienta>> ObtenerTipoPorIdAsync(int idTipo, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.GestionarInventarioHerramientas],
            () => puertoHerramientas.ObtenerTipoPorIdAsync(idTipo, cancelacion));

    public Task<Resultado<RespuestaHerramienta>> ObtenerHerramientaPorIdAsync(int idHerramienta, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.GestionarInventarioHerramientas],
            () => puertoHerramientas.ObtenerHerramientaPorIdAsync(idHerramienta, cancelacion));

    public Task<Resultado<RespuestaAsignacionHerramienta>> ObtenerAsignacionPorIdAsync(long idAsignacion, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.GestionarAsignacionesHerramientas],
            () => puertoHerramientas.ObtenerAsignacionPorIdAsync(idAsignacion, cancelacion), r => r.IdObra);

    public Task<Resultado<RespuestaIncidenciaHerramienta>> ObtenerIncidenciaPorIdAsync(long idIncidencia, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.RegistrarIncidenciasHerramientas],
            () => puertoHerramientas.ObtenerIncidenciaPorIdAsync(idIncidencia, cancelacion), r => r.IdObra);

    public Task<Resultado<IReadOnlyList<RespuestaIncidenciaHerramienta>>> ListarIncidenciasAsync(int? idObra,
        long? idAsignacionHerramienta, CancellationToken cancelacion = default, int? idHerramienta = null) =>
        ConsultaAutorizada.ListarAsync(contextoUsuario, [CodigosPermiso.RegistrarIncidenciasHerramientas], idObra,
            obras => puertoHerramientas.ListarIncidenciasAsync(idObra, idAsignacionHerramienta, obras, cancelacion, idHerramienta));
}
