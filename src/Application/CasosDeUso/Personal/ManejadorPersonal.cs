using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Personal;

public sealed class ManejadorPersonal(
    IPuertoPersonal puertoPersonal,
    IContextoUsuarioActual contextoUsuario,
    PoliticaOperacionesObra politicaObra)
{
    public async Task<Resultado<RespuestaCargo>> CrearCargoAsync(
        SolicitudCargo solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarTrabajadores);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaCargo>.Fallo(autorizacion.Error!);
        }

        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 60)
        {
            return Resultado<RespuestaCargo>.Fallo(
                ErrorNegocio.Validacion("nombre_cargo_invalido", "El nombre del cargo es obligatorio y no debe exceder 60 caracteres."));
        }

        if (await puertoPersonal.ExisteCargoPorNombreAsync(nombre, cancelacion))
        {
            return Resultado<RespuestaCargo>.Fallo(
                ErrorNegocio.Conflicto("cargo_duplicado", "Ya existe un cargo registrado con ese nombre."));
        }

        var creado = await puertoPersonal.CrearCargoAsync(
            new SolicitudCargo(nombre, solicitud.Descripcion?.Trim()),
            cancelacion);
        return Resultado<RespuestaCargo>.Exito(creado);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaCargo>>> ListarCargosAsync(
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarTrabajadores);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaCargo>>.Fallo(autorizacion.Error!);
        }

        return Resultado<IReadOnlyList<RespuestaCargo>>.Exito(
            await puertoPersonal.ListarCargosAsync(cancelacion));
    }

    public async Task<Resultado<RespuestaEspecialidad>> CrearEspecialidadAsync(
        SolicitudEspecialidad solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarTrabajadores);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaEspecialidad>.Fallo(autorizacion.Error!);
        }

        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 60)
        {
            return Resultado<RespuestaEspecialidad>.Fallo(
                ErrorNegocio.Validacion("nombre_especialidad_invalido", "El nombre de la especialidad es obligatorio y no debe exceder 60 caracteres."));
        }

        if (await puertoPersonal.ExisteEspecialidadPorNombreAsync(nombre, cancelacion))
        {
            return Resultado<RespuestaEspecialidad>.Fallo(
                ErrorNegocio.Conflicto("especialidad_duplicada", "Ya existe una especialidad registrada con ese nombre."));
        }

        var creada = await puertoPersonal.CrearEspecialidadAsync(
            new SolicitudEspecialidad(nombre, solicitud.Descripcion?.Trim()),
            cancelacion);
        return Resultado<RespuestaEspecialidad>.Exito(creada);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaEspecialidad>>> ListarEspecialidadesAsync(
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarTrabajadores);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaEspecialidad>>.Fallo(autorizacion.Error!);
        }

        return Resultado<IReadOnlyList<RespuestaEspecialidad>>.Exito(
            await puertoPersonal.ListarEspecialidadesAsync(cancelacion));
    }

    public async Task<Resultado<RespuestaCuadrilla>> CrearCuadrillaAsync(
        SolicitudCuadrilla solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarAsignaciones);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaCuadrilla>.Fallo(autorizacion.Error!);
        }

        var codigo = solicitud.Codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 20 || string.IsNullOrWhiteSpace(nombre) || nombre.Length > 80)
        {
            return Resultado<RespuestaCuadrilla>.Fallo(
                ErrorNegocio.Validacion("cuadrilla_invalida", "El código (máx. 20) y nombre (máx. 80) de la cuadrilla son obligatorios."));
        }

        if (await puertoPersonal.ExisteCuadrillaPorCodigoAsync(codigo, cancelacion))
        {
            return Resultado<RespuestaCuadrilla>.Fallo(
                ErrorNegocio.Conflicto("cuadrilla_duplicada", "Ya existe una cuadrilla registrada con ese código."));
        }

        var creada = await puertoPersonal.CrearCuadrillaAsync(
            new SolicitudCuadrilla(codigo, nombre, solicitud.Descripcion?.Trim()),
            cancelacion);
        return Resultado<RespuestaCuadrilla>.Exito(creada);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaCuadrilla>>> ListarCuadrillasAsync(
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarTrabajadores);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaCuadrilla>>.Fallo(autorizacion.Error!);
        }

        return Resultado<IReadOnlyList<RespuestaCuadrilla>>.Exito(
            await puertoPersonal.ListarCuadrillasAsync(cancelacion));
    }

    public async Task<Resultado<RespuestaTrabajador>> CrearTrabajadorAsync(
        SolicitudCrearTrabajador solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarTrabajadores);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaTrabajador>.Fallo(autorizacion.Error!);
        }

        var cedula = solicitud.Cedula?.Trim().ToUpperInvariant() ?? string.Empty;
        var primerNombre = solicitud.PrimerNombre?.Trim() ?? string.Empty;
        var primerApellido = solicitud.PrimerApellido?.Trim() ?? string.Empty;
        var correo = string.IsNullOrWhiteSpace(solicitud.Correo) ? null : solicitud.Correo.Trim();

        if (string.IsNullOrWhiteSpace(cedula) || cedula.Length > 20
            || string.IsNullOrWhiteSpace(primerNombre) || primerNombre.Length > 50
            || string.IsNullOrWhiteSpace(primerApellido) || primerApellido.Length > 50)
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.Validacion("datos_trabajador_invalidos", "La cédula, el primer nombre y el primer apellido son obligatorios."));
        }

        var cargo = await puertoPersonal.ObtenerCargoPorIdAsync(solicitud.IdCargo, cancelacion);
        if (cargo is null || !cargo.Activo)
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.NoEncontrado("cargo_no_encontrado", "El cargo indicado no existe o está inactivo."));
        }

        if (await puertoPersonal.ExisteTrabajadorPorCedulaAsync(cedula, null, cancelacion))
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.Conflicto("cedula_duplicada", "Ya existe un trabajador registrado con esa cédula."));
        }

        if (correo is not null && await puertoPersonal.ExisteTrabajadorPorCorreoAsync(correo, null, cancelacion))
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.Conflicto("correo_trabajador_duplicado", "Ya existe un trabajador registrado con ese correo electrónico."));
        }

        if (solicitud.Especialidades is { Count: > 0 })
        {
            foreach (var esp in solicitud.Especialidades)
            {
                if (!string.IsNullOrWhiteSpace(esp.Nivel) && !NivelesEspecialidad.Todos.Contains(esp.Nivel.Trim().ToUpperInvariant()))
                {
                    return Resultado<RespuestaTrabajador>.Fallo(
                        ErrorNegocio.Validacion("nivel_especialidad_invalido", "El nivel de especialidad debe ser BASICO, INTERMEDIO, AVANZADO o CERTIFICADO."));
                }

                if (esp.FechaCertificacion is DateOnly fCert && esp.FechaVencimiento is DateOnly fVenc && fVenc < fCert)
                {
                    return Resultado<RespuestaTrabajador>.Fallo(
                        ErrorNegocio.Validacion("fechas_especialidad_invalidas", "La fecha de vencimiento de certificación no puede ser anterior a la de certificación."));
                }
            }
        }

        var actor = autorizacion.Valor!;
        var creado = await puertoPersonal.CrearTrabajadorAsync(
            solicitud with
            {
                Cedula = cedula,
                PrimerNombre = primerNombre,
                SegundoNombre = solicitud.SegundoNombre?.Trim(),
                PrimerApellido = primerApellido,
                SegundoApellido = solicitud.SegundoApellido?.Trim(),
                Telefono = solicitud.Telefono?.Trim(),
                Correo = correo,
                Direccion = solicitud.Direccion?.Trim()
            },
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "trabajadores",
                cedula,
                AccionesAuditoria.Crear,
                "estado",
                null,
                EstadosTrabajador.Activo,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaTrabajador>.Exito(creado);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaTrabajador>>> ListarTrabajadoresAsync(
        string? estado = null,
        bool soloAsignables = false,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarTrabajadores);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaTrabajador>>.Fallo(autorizacion.Error!);
        }

        string? estadoNormalizado = null;
        if (!string.IsNullOrWhiteSpace(estado))
        {
            estadoNormalizado = estado.Trim().ToUpperInvariant();
            if (!EstadosTrabajador.Todos.Contains(estadoNormalizado))
            {
                return Resultado<IReadOnlyList<RespuestaTrabajador>>.Fallo(
                    ErrorNegocio.Validacion("estado_trabajador_invalido", "El estado del trabajador debe ser ACTIVO o INACTIVO."));
            }
        }

        var usuario = autorizacion.Valor!;
        IReadOnlySet<int>? obrasAlcance = usuario.TieneAlcanceGlobal ? null : usuario.ObrasAsignadas;
        var trabajadores = await puertoPersonal.ListarTrabajadoresAsync(
            estadoNormalizado,
            soloAsignables,
            obrasAlcance,
            cancelacion);

        return Resultado<IReadOnlyList<RespuestaTrabajador>>.Exito(trabajadores);
    }

    public async Task<Resultado<RespuestaTrabajador>> ActualizarTrabajadorAsync(
        int idTrabajador,
        SolicitudActualizarTrabajador solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = await AutorizarGestionTrabajadorAsync(idTrabajador, cancelacion);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaTrabajador>.Fallo(autorizacion.Error!);
        }

        var existente = await puertoPersonal.ObtenerTrabajadorPorIdAsync(idTrabajador, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.NoEncontrado("trabajador_no_encontrado", "El trabajador solicitado no existe."));
        }

        var primerNombre = solicitud.PrimerNombre?.Trim() ?? string.Empty;
        var primerApellido = solicitud.PrimerApellido?.Trim() ?? string.Empty;
        var correo = string.IsNullOrWhiteSpace(solicitud.Correo) ? null : solicitud.Correo.Trim();

        if (string.IsNullOrWhiteSpace(primerNombre) || primerNombre.Length > 50
            || string.IsNullOrWhiteSpace(primerApellido) || primerApellido.Length > 50)
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.Validacion("datos_trabajador_invalidos", "El primer nombre y el primer apellido son obligatorios."));
        }

        if (correo is not null && await puertoPersonal.ExisteTrabajadorPorCorreoAsync(correo, idTrabajador, cancelacion))
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.Conflicto("correo_trabajador_duplicado", "Ya existe otro trabajador con ese correo electrónico."));
        }

        var actor = autorizacion.Valor!;
        var actualizado = await puertoPersonal.ActualizarTrabajadorAsync(
            idTrabajador,
            solicitud with
            {
                PrimerNombre = primerNombre,
                SegundoNombre = solicitud.SegundoNombre?.Trim(),
                PrimerApellido = primerApellido,
                SegundoApellido = solicitud.SegundoApellido?.Trim(),
                Telefono = solicitud.Telefono?.Trim(),
                Correo = correo,
                Direccion = solicitud.Direccion?.Trim()
            },
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "trabajadores",
                idTrabajador.ToString(),
                AccionesAuditoria.Modificar,
                "datos_personales",
                $"{existente.PrimerNombre} {existente.PrimerApellido}",
                $"{primerNombre} {primerApellido}",
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaTrabajador>.Exito(actualizado!);
    }

    public async Task<Resultado<RespuestaTrabajador>> CambiarEstadoTrabajadorAsync(
        int idTrabajador,
        SolicitudCambiarEstadoTrabajador solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = await AutorizarGestionTrabajadorAsync(idTrabajador, cancelacion);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaTrabajador>.Fallo(autorizacion.Error!);
        }

        var nuevoEstado = solicitud.Estado?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!EstadosTrabajador.Todos.Contains(nuevoEstado))
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.Validacion("estado_trabajador_invalido", "El estado del trabajador debe ser ACTIVO o INACTIVO."));
        }

        var existente = await puertoPersonal.ObtenerTrabajadorPorIdAsync(idTrabajador, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaTrabajador>.Fallo(
                ErrorNegocio.NoEncontrado("trabajador_no_encontrado", "El trabajador solicitado no existe."));
        }

        var actor = autorizacion.Valor!;
        var accion = nuevoEstado == EstadosTrabajador.Inactivo ? AccionesAuditoria.Anular : AccionesAuditoria.Modificar;
        var actualizado = await puertoPersonal.CambiarEstadoTrabajadorAsync(
            idTrabajador,
            nuevoEstado,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "trabajadores",
                idTrabajador.ToString(),
                accion,
                "estado",
                existente.Estado,
                nuevoEstado,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaTrabajador>.Exito(actualizado!);
    }

    public async Task<Resultado<RespuestaAsignacionTrabajadorObra>> AsignarTrabajadorAObraAsync(
        SolicitudAsignarTrabajadorObra solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.GestionarAsignaciones,
            solicitud.IdObra);

        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(autorizacion.Error!);
        }

        var tipoContratacion = solicitud.TipoContratacion?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!TiposContratacion.Todos.Contains(tipoContratacion))
        {
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(
                ErrorNegocio.Validacion("tipo_contratacion_invalido", "El tipo de contratación debe ser FIJO, SUBCONTRATADO o TEMPORAL."));
        }

        if (solicitud.FechaFin is DateOnly fin && fin < solicitud.FechaInicio)
        {
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(
                ErrorNegocio.Validacion("fechas_asignacion_invalidas", "La fecha de fin no puede ser anterior a la fecha de inicio."));
        }

        if (!await puertoPersonal.ExisteObraAsync(solicitud.IdObra, cancelacion))
        {
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(
                ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra indicada no existe."));
        }

        var obraPlanificable = await politicaObra.ExigirPlanificableAsync(solicitud.IdObra, cancelacion);
        if (!obraPlanificable.EsExitoso) return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(obraPlanificable.Error!);
        var trabajador = await puertoPersonal.ObtenerTrabajadorPorIdAsync(solicitud.IdTrabajador, cancelacion);
        if (trabajador is null)
        {
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(
                ErrorNegocio.NoEncontrado("trabajador_no_encontrado", "El trabajador indicado no existe."));
        }

        // Criterio CET-24: trabajadores inactivos no se permiten para nuevas asignaciones.
        if (trabajador.Estado != EstadosTrabajador.Activo)
        {
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(
                ErrorNegocio.Conflicto("trabajador_inactivo", "Un trabajador inactivo no puede recibir nuevas asignaciones a obras."));
        }

        if (await puertoPersonal.ExisteAsignacionActivaTrabajadorObraAsync(solicitud.IdTrabajador, solicitud.IdObra, cancelacion))
        {
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(
                ErrorNegocio.Conflicto("asignacion_activa_existente", "El trabajador ya tiene una asignación activa vigente en esta obra."));
        }

        if (solicitud.IdEspecialidad is int idEspecialidad)
        {
            var especialidad = await puertoPersonal.ObtenerEspecialidadPorIdAsync(idEspecialidad, cancelacion);
            if (especialidad is null || !especialidad.Activo)
            {
                return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(
                    ErrorNegocio.NoEncontrado("especialidad_no_encontrada", "La especialidad indicada no existe o está inactiva."));
            }
        }

        var actor = autorizacion.Valor!;
        var asignacion = await puertoPersonal.AsignarTrabajadorAObraAsync(
            solicitud with { TipoContratacion = tipoContratacion },
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "asignaciones_trabajador_obra",
                $"{solicitud.IdTrabajador}:{solicitud.IdObra}",
                AccionesAuditoria.Crear,
                "estado",
                null,
                EstadosAsignacionObra.Activa,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaAsignacionTrabajadorObra>.Exito(asignacion);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaAsignacionTrabajadorObra>>> ListarAsignacionesDeObraAsync(
        int idObra,
        bool soloVigentes = true,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ConsultarTrabajadores,
            idObra);

        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaAsignacionTrabajadorObra>>.Fallo(autorizacion.Error!);
        }

        var lista = await puertoPersonal.ListarAsignacionesObraAsync(idObra, soloVigentes, cancelacion);
        return Resultado<IReadOnlyList<RespuestaAsignacionTrabajadorObra>>.Exito(lista);
    }

    public async Task<Resultado<RespuestaCuadrillaObra>> AsignarCuadrillaAObraAsync(
        SolicitudAsignarCuadrillaObra solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.GestionarAsignaciones,
            solicitud.IdObra);

        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaCuadrillaObra>.Fallo(autorizacion.Error!);
        }

        if (solicitud.FechaFin is DateOnly fin && fin < solicitud.FechaInicio)
        {
            return Resultado<RespuestaCuadrillaObra>.Fallo(
                ErrorNegocio.Validacion("fechas_cuadrilla_obra_invalidas", "La fecha de fin no puede ser anterior a la fecha de inicio."));
        }

        var cuadrilla = await puertoPersonal.ObtenerCuadrillaPorIdAsync(solicitud.IdCuadrilla, cancelacion);
        if (cuadrilla is null || !cuadrilla.Activo)
        {
            return Resultado<RespuestaCuadrillaObra>.Fallo(
                ErrorNegocio.NoEncontrado("cuadrilla_no_encontrada", "La cuadrilla indicada no existe o está inactiva."));
        }

        if (!await puertoPersonal.ExisteObraAsync(solicitud.IdObra, cancelacion))
        {
            return Resultado<RespuestaCuadrillaObra>.Fallo(
                ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra indicada no existe."));
        }

        var obraPlanificable = await politicaObra.ExigirPlanificableAsync(solicitud.IdObra, cancelacion);
        if (!obraPlanificable.EsExitoso) return Resultado<RespuestaCuadrillaObra>.Fallo(obraPlanificable.Error!);
        if (await puertoPersonal.ExisteCuadrillaObraActivaAsync(solicitud.IdCuadrilla, solicitud.IdObra, cancelacion))
        {
            return Resultado<RespuestaCuadrillaObra>.Fallo(
                ErrorNegocio.Conflicto("cuadrilla_obra_activa_existente", "La cuadrilla ya está activa en esta obra."));
        }

        var actor = autorizacion.Valor!;
        var creada = await puertoPersonal.AsignarCuadrillaAObraAsync(
            solicitud,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "cuadrillas_obra",
                $"{solicitud.IdCuadrilla}:{solicitud.IdObra}",
                AccionesAuditoria.Crear,
                "estado",
                null,
                EstadosAsignacionObra.Activa,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaCuadrillaObra>.Exito(creada);
    }

    public async Task<Resultado<RespuestaTrabajadorCuadrilla>> AsignarTrabajadorACuadrillaAsync(
        SolicitudAsignarTrabajadorCuadrilla solicitud,
        CancellationToken cancelacion = default)
    {
        if (solicitud.FechaFin is DateOnly fin && fin < solicitud.FechaInicio)
        {
            return Resultado<RespuestaTrabajadorCuadrilla>.Fallo(
                ErrorNegocio.Validacion("fechas_trabajador_cuadrilla_invalidas", "La fecha de fin no puede ser anterior a la fecha de inicio."));
        }

        var asignacion = await puertoPersonal.ObtenerAsignacionPorIdAsync(solicitud.IdAsignacion, cancelacion);
        if (asignacion is null)
        {
            return Resultado<RespuestaTrabajadorCuadrilla>.Fallo(
                ErrorNegocio.NoEncontrado("asignacion_no_encontrada", "La asignación del trabajador no existe."));
        }

        var cuadrillaObra = await puertoPersonal.ObtenerCuadrillaObraPorIdAsync(solicitud.IdCuadrillaObra, cancelacion);
        if (cuadrillaObra is null)
        {
            return Resultado<RespuestaTrabajadorCuadrilla>.Fallo(
                ErrorNegocio.NoEncontrado("cuadrilla_obra_no_encontrada", "La asignación de cuadrilla a obra no existe."));
        }

        // CRQ-0003: Validación explícita multiobra en capa de aplicación además del disparador SQL.
        if (asignacion.IdObra != cuadrillaObra.IdObra)
        {
            return Resultado<RespuestaTrabajadorCuadrilla>.Fallo(
                ErrorNegocio.Conflicto(
                    "asociacion_multiobra_invalida",
                    "No se permite asociar un trabajador y una cuadrilla pertenecientes a obras diferentes."));
        }

        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.GestionarAsignaciones,
            asignacion.IdObra);

        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaTrabajadorCuadrilla>.Fallo(autorizacion.Error!);
        }

        var obraPlanificable = await politicaObra.ExigirPlanificableAsync(asignacion.IdObra, cancelacion);
        if (!obraPlanificable.EsExitoso) return Resultado<RespuestaTrabajadorCuadrilla>.Fallo(obraPlanificable.Error!);
        if (await puertoPersonal.ExisteTrabajadorCuadrillaActivaAsync(solicitud.IdAsignacion, cancelacion))
        {
            return Resultado<RespuestaTrabajadorCuadrilla>.Fallo(
                ErrorNegocio.Conflicto("trabajador_cuadrilla_activa_existente", "El trabajador ya integra una cuadrilla activa en esta asignación."));
        }

        var actor = autorizacion.Valor!;
        var miembro = await puertoPersonal.AsignarTrabajadorACuadrillaAsync(
            solicitud,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "trabajadores_cuadrilla",
                $"{solicitud.IdAsignacion}:{solicitud.IdCuadrillaObra}",
                AccionesAuditoria.Crear,
                "id_cuadrilla_obra",
                null,
                solicitud.IdCuadrillaObra.ToString(),
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaTrabajadorCuadrilla>.Exito(miembro);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaTrabajadorCuadrilla>>> ListarMiembrosCuadrillaObraAsync(
        int idObra,
        int? idCuadrillaObra = null,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ConsultarTrabajadores,
            idObra);

        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaTrabajadorCuadrilla>>.Fallo(autorizacion.Error!);
        }

        var miembros = await puertoPersonal.ListarMiembrosCuadrillaObraAsync(idObra, idCuadrillaObra, cancelacion);
        return Resultado<IReadOnlyList<RespuestaTrabajadorCuadrilla>>.Exito(miembros);
    }
    public async Task<Resultado<RespuestaAsignacionTrabajadorObra>> FinalizarAsignacionObraAsync(
        int idAsignacion, SolicitudFinalizarAsignacionObra solicitud, CancellationToken cancelacion = default)
    {
        var existente = await puertoPersonal.ObtenerAsignacionPorIdAsync(idAsignacion, cancelacion);
        if (existente is null) return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(
            ErrorNegocio.NoEncontrado("asignacion_no_encontrada", "La asignación no existe."));
        var permiso = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.GestionarAsignaciones, existente.IdObra);
        if (!permiso.EsExitoso) return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(permiso.Error!);
        var estado = solicitud.Estado?.Trim().ToUpperInvariant();
        if (estado is not (EstadosAsignacionObra.Finalizada or EstadosAsignacionObra.Anulada)
            || solicitud.FechaFin < existente.FechaInicio)
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(ErrorNegocio.Validacion(
                "cierre_asignacion_invalido", "Indique FINALIZADA o ANULADA y una fecha de fin no anterior al inicio."));
        if (existente.Estado == estado && existente.FechaFin == solicitud.FechaFin)
            return Resultado<RespuestaAsignacionTrabajadorObra>.Exito(existente);
        if (existente.Estado != EstadosAsignacionObra.Activa)
            return Resultado<RespuestaAsignacionTrabajadorObra>.Fallo(ErrorNegocio.Conflicto(
                "asignacion_ya_cerrada", "La asignación ya fue finalizada o anulada."));
        var actor = permiso.Valor!;
        var cerrada = await puertoPersonal.FinalizarAsignacionObraAsync(idAsignacion, solicitud.FechaFin, estado,
            new RegistroEventoAuditoria(actor.IdUsuario, "asignaciones_trabajador_obra", idAsignacion.ToString(),
                AccionesAuditoria.Modificar, "estado", existente.Estado, estado, actor.IpOrigen), cancelacion);
        return Resultado<RespuestaAsignacionTrabajadorObra>.Exito(cerrada!);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaCuadrillaObra>>> ListarCuadrillasDeObraAsync(
        int idObra, CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.ConsultarTrabajadores, idObra);
        if (!permiso.EsExitoso) return Resultado<IReadOnlyList<RespuestaCuadrillaObra>>.Fallo(permiso.Error!);
        return Resultado<IReadOnlyList<RespuestaCuadrillaObra>>.Exito(
            await puertoPersonal.ListarCuadrillasDeObraAsync(idObra, cancelacion));
    }

    public Task<Resultado<RespuestaTrabajadorCuadrilla>> ObtenerMiembroCuadrillaPorIdAsync(
        int idTrabajadorCuadrilla, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarTrabajadores],
            () => puertoPersonal.ObtenerMiembroCuadrillaPorIdAsync(idTrabajadorCuadrilla, cancelacion), r => r.IdObra);

    private async Task<Resultado<UsuarioAutenticado>> AutorizarGestionTrabajadorAsync(int idTrabajador, CancellationToken cancelacion)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarTrabajadores);
        if (!permiso.EsExitoso || permiso.Valor!.TieneAlcanceGlobal) return permiso;
        return await puertoPersonal.TrabajadorPerteneceAObrasAsync(idTrabajador, permiso.Valor.ObrasAsignadas, cancelacion)
            ? permiso
            : Resultado<UsuarioAutenticado>.Fallo(ErrorNegocio.Prohibido("trabajador_fuera_de_alcance", "El trabajador no pertenece a sus obras autorizadas."));
    }
    public Task<Resultado<RespuestaCargo>> ObtenerCargoPorIdAsync(int idCargo, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarTrabajadores],
            () => puertoPersonal.ObtenerCargoPorIdAsync(idCargo, cancelacion));

    public Task<Resultado<RespuestaEspecialidad>> ObtenerEspecialidadPorIdAsync(int idEspecialidad, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarTrabajadores],
            () => puertoPersonal.ObtenerEspecialidadPorIdAsync(idEspecialidad, cancelacion));

    public Task<Resultado<RespuestaCuadrilla>> ObtenerCuadrillaPorIdAsync(int idCuadrilla, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarTrabajadores],
            () => puertoPersonal.ObtenerCuadrillaPorIdAsync(idCuadrilla, cancelacion));

    public Task<Resultado<RespuestaAsignacionTrabajadorObra>> ObtenerAsignacionPorIdAsync(int idAsignacion, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarTrabajadores],
            () => puertoPersonal.ObtenerAsignacionPorIdAsync(idAsignacion, cancelacion), r => r.IdObra);

    public Task<Resultado<RespuestaCuadrillaObra>> ObtenerCuadrillaObraPorIdAsync(int idCuadrillaObra, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarTrabajadores],
            () => puertoPersonal.ObtenerCuadrillaObraPorIdAsync(idCuadrillaObra, cancelacion), r => r.IdObra);
    public async Task<Resultado<RespuestaTrabajador>> ObtenerTrabajadorPorIdAsync(int idTrabajador, CancellationToken cancelacion = default)
    {
        var resultado = await ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.ConsultarTrabajadores],
            () => puertoPersonal.ObtenerTrabajadorPorIdAsync(idTrabajador, cancelacion));
        if (!resultado.EsExitoso || contextoUsuario.ObtenerUsuario()!.TieneAlcanceGlobal) return resultado;
        return await puertoPersonal.TrabajadorPerteneceAObrasAsync(idTrabajador, contextoUsuario.ObtenerUsuario()!.ObrasAsignadas, cancelacion)
            ? resultado : Resultado<RespuestaTrabajador>.Fallo(ErrorNegocio.Prohibido("trabajador_fuera_de_alcance", "El trabajador no pertenece a sus obras."));
    }
}
