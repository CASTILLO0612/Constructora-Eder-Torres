using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Seguridad;

public sealed class ManejadorSeguridad(
    IPuertoSeguridad puertoSeguridad,
    IServicioHashContrasena servicioHash,
    IContextoUsuarioActual contextoUsuario,
    IRelojSistema reloj,
    ICalendarioLaboral calendario)
{
    private const int LongitudMinimaContrasena = 8;

    public async Task<Resultado<RespuestaUsuario>> CrearUsuarioAsync(
        SolicitudCrearUsuario solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarUsuarios);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaUsuario>.Fallo(autorizacion.Error!);
        }

        var nombreNormalizado = solicitud.NombreUsuario?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombreNormalizado) || nombreNormalizado.Length > 60)
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.Validacion("nombre_usuario_invalido", "El nombre de usuario es obligatorio y no debe exceder 60 caracteres."));
        }

        if (string.IsNullOrWhiteSpace(solicitud.Contrasena) || solicitud.Contrasena.Length < LongitudMinimaContrasena || solicitud.Contrasena.Length > 256)
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.Validacion("contrasena_invalida", $"La contraseña debe tener al menos {LongitudMinimaContrasena} caracteres."));
        }

        if (await puertoSeguridad.ExisteNombreUsuarioAsync(nombreNormalizado, null, cancelacion))
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.Conflicto("nombre_usuario_duplicado", "Ya existe una cuenta registrada con ese nombre de usuario."));
        }

        if (solicitud.IdTrabajador is int idTrabajador)
        {
            var validacionTrabajador = await ValidarVinculoTrabajadorAsync(idTrabajador, null, cancelacion);
            if (!validacionTrabajador.EsExitoso)
            {
                return Resultado<RespuestaUsuario>.Fallo(validacionTrabajador.Error!);
            }
        }

        var actor = autorizacion.Valor!;
        var hash = servicioHash.GenerarHash(solicitud.Contrasena);
        var rolesIniciales = solicitud.IdsRolesIniciales?.Distinct().ToArray() ?? [];
        var rolesDisponibles = await puertoSeguridad.ListarRolesAsync(cancelacion);
        if (rolesIniciales.Any(id => !rolesDisponibles.Any(r => r.IdRol == id && r.Activo)))
            return Resultado<RespuestaUsuario>.Fallo(ErrorNegocio.Validacion("rol_invalido", "Todos los roles iniciales deben existir y estar activos."));
        if (rolesIniciales.Length > 0 && !actor.TienePermiso(CodigosPermiso.GestionarRoles))
            return Resultado<RespuestaUsuario>.Fallo(ErrorNegocio.Prohibido("permiso_denegado", "La asignación inicial de roles requiere administrar roles."));
        if (solicitud.IdTrabajador is null && rolesDisponibles.Any(r => rolesIniciales.Contains(r.IdRol) && r.Nombre == NombresRol.Trabajador))
            return Resultado<RespuestaUsuario>.Fallo(ErrorNegocio.Validacion("trabajador_requerido",
                "Una cuenta con el rol TRABAJADOR debe estar vinculada a un trabajador activo."));


        var creado = await puertoSeguridad.CrearUsuarioAsync(
            nombreNormalizado,
            hash,
            solicitud.IdTrabajador,
            reloj.AhoraUtc,
            calendario.Hoy,
            rolesIniciales,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "usuarios",
                nombreNormalizado,
                AccionesAuditoria.Crear,
                "estado",
                null,
                EstadosUsuario.Activo,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaUsuario>.Exito(creado);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaUsuario>>> ListarUsuariosAsync(
        string? estado = null,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarUsuarios);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaUsuario>>.Fallo(autorizacion.Error!);
        }

        if (!string.IsNullOrWhiteSpace(estado) && !EstadosUsuario.Todos.Contains(estado.Trim().ToUpperInvariant()))
        {
            return Resultado<IReadOnlyList<RespuestaUsuario>>.Fallo(
                ErrorNegocio.Validacion("estado_usuario_invalido", "El filtro de estado de usuario no es válido."));
        }

        var usuarios = await puertoSeguridad.ListarUsuariosAsync(
             string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant(),
            cancelacion);
        return Resultado<IReadOnlyList<RespuestaUsuario>>.Exito(usuarios);
    }

    public async Task<Resultado<RespuestaUsuario>> ObtenerUsuarioPorIdAsync(
        int idUsuario,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarUsuarios);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaUsuario>.Fallo(autorizacion.Error!);
        }

        var usuario = await puertoSeguridad.ObtenerUsuarioPorIdAsync(idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.NoEncontrado("usuario_no_encontrado", "La cuenta de usuario solicitada no existe."));
        }

        return Resultado<RespuestaUsuario>.Exito(usuario);
    }

    public async Task<Resultado<RespuestaUsuario>> ActualizarUsuarioAsync(
        int idUsuario,
        SolicitudActualizarUsuario solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarUsuarios);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaUsuario>.Fallo(autorizacion.Error!);
        }

        var existente = await puertoSeguridad.ObtenerUsuarioPorIdAsync(idUsuario, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.NoEncontrado("usuario_no_encontrado", "La cuenta de usuario solicitada no existe."));
        }

        var nombreNormalizado = solicitud.NombreUsuario?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombreNormalizado) || nombreNormalizado.Length > 60)
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.Validacion("nombre_usuario_invalido", "El nombre de usuario es obligatorio y no debe exceder 60 caracteres."));
        }

        if (await puertoSeguridad.ExisteNombreUsuarioAsync(nombreNormalizado, idUsuario, cancelacion))
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.Conflicto("nombre_usuario_duplicado", "Ya existe otra cuenta registrada con ese nombre de usuario."));
        }

        if (solicitud.IdTrabajador is null && existente.Roles.Any(r => r.NombreRol == NombresRol.Trabajador
            && (r.FechaFin is null || r.FechaFin >= calendario.Hoy)))
            return Resultado<RespuestaUsuario>.Fallo(ErrorNegocio.Validacion("trabajador_requerido",
                "Debe conservar el vínculo del trabajador mientras tenga asignado el rol TRABAJADOR."));

        if (solicitud.IdTrabajador is int idTrabajador)
        {
            var validacionTrabajador = await ValidarVinculoTrabajadorAsync(idTrabajador, idUsuario, cancelacion);
            if (!validacionTrabajador.EsExitoso)
            {
                return Resultado<RespuestaUsuario>.Fallo(validacionTrabajador.Error!);
            }
        }

        string? nuevoHash = null;
        if (!string.IsNullOrWhiteSpace(solicitud.NuevaContrasena))
        {
            if (solicitud.NuevaContrasena.Length < LongitudMinimaContrasena || solicitud.NuevaContrasena.Length > 256)
            {
                return Resultado<RespuestaUsuario>.Fallo(
                    ErrorNegocio.Validacion("contrasena_invalida", $"La nueva contraseña debe tener al menos {LongitudMinimaContrasena} caracteres."));
            }

            nuevoHash = servicioHash.GenerarHash(solicitud.NuevaContrasena);
        }

        var actor = autorizacion.Valor!;
        var actualizado = await puertoSeguridad.ActualizarUsuarioAsync(
            idUsuario,
            nombreNormalizado,
            solicitud.IdTrabajador,
            nuevoHash,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "usuarios",
                idUsuario.ToString(),
                AccionesAuditoria.Modificar,
                "nombre_usuario",
                existente.NombreUsuario,
                nombreNormalizado,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaUsuario>.Exito(actualizado!);
    }

    public async Task<Resultado<RespuestaUsuario>> CambiarEstadoUsuarioAsync(
        int idUsuario,
        SolicitudCambiarEstadoUsuario solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarUsuarios);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaUsuario>.Fallo(autorizacion.Error!);
        }

        var nuevoEstado = solicitud.Estado?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!EstadosUsuario.Todos.Contains(nuevoEstado))
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.Validacion("estado_usuario_invalido", "El estado de cuenta debe ser ACTIVO, BLOQUEADO o INACTIVO."));
        }

        var existente = await puertoSeguridad.ObtenerUsuarioPorIdAsync(idUsuario, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaUsuario>.Fallo(
                ErrorNegocio.NoEncontrado("usuario_no_encontrado", "La cuenta de usuario solicitada no existe."));
        }

        var actor = autorizacion.Valor!;
        var accion = nuevoEstado == EstadosUsuario.Inactivo ? AccionesAuditoria.Anular : AccionesAuditoria.Modificar;
        var actualizado = await puertoSeguridad.CambiarEstadoUsuarioAsync(
            idUsuario,
            nuevoEstado,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "usuarios",
                idUsuario.ToString(),
                accion,
                "estado",
                existente.Estado,
                nuevoEstado,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaUsuario>.Exito(actualizado!);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaPermiso>>> ListarPermisosAsync(
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarRoles);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaPermiso>>.Fallo(autorizacion.Error!);
        }

        var permisos = await puertoSeguridad.ListarPermisosAsync(cancelacion);
        return Resultado<IReadOnlyList<RespuestaPermiso>>.Exito(permisos);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaRol>>> ListarRolesAsync(
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarRoles);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaRol>>.Fallo(autorizacion.Error!);
        }

        var roles = await puertoSeguridad.ListarRolesAsync(cancelacion);
        return Resultado<IReadOnlyList<RespuestaRol>>.Exito(roles);
    }

    public async Task<Resultado<RespuestaRol>> CrearRolAsync(
        SolicitudCrearRol solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarRoles);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaRol>.Fallo(autorizacion.Error!);
        }

        var nombreNormalizado = solicitud.Nombre?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombreNormalizado) || nombreNormalizado.Length > 50)
        {
            return Resultado<RespuestaRol>.Fallo(
                ErrorNegocio.Validacion("nombre_rol_invalido", "El nombre del rol es obligatorio y no debe exceder 50 caracteres."));
        }

        if (await puertoSeguridad.ExisteRolPorNombreAsync(nombreNormalizado, null, cancelacion))
        {
            return Resultado<RespuestaRol>.Fallo(
                ErrorNegocio.Conflicto("rol_duplicado", "Ya existe un rol con ese nombre."));
        }

        var codigos = solicitud.CodigosPermiso?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToArray() ?? [];
        var permisosDisponibles = await puertoSeguridad.ListarPermisosAsync(cancelacion);
        if (codigos.Any(c => !permisosDisponibles.Any(p => p.Activo && p.Codigo == c)))
            return Resultado<RespuestaRol>.Fallo(ErrorNegocio.Validacion("permiso_invalido", "Todos los códigos de permiso deben existir y estar activos."));
        var actor = autorizacion.Valor!;
        var rol = await puertoSeguridad.CrearRolAsync(
            nombreNormalizado,
            solicitud.Descripcion?.Trim(),
            codigos,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "roles",
                nombreNormalizado,
                AccionesAuditoria.Crear,
                "permisos",
                null,
                string.Join(',', codigos),
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaRol>.Exito(rol);
    }

    public async Task<Resultado<RespuestaRol>> AsignarPermisosARolAsync(
        int idRol,
        SolicitudAsignarPermisosRol solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarRoles);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaRol>.Fallo(autorizacion.Error!);
        }

        var rolExistente = await puertoSeguridad.ObtenerRolPorIdAsync(idRol, cancelacion);
        if (rolExistente is null)
        {
            return Resultado<RespuestaRol>.Fallo(
                ErrorNegocio.NoEncontrado("rol_no_encontrado", "El rol indicado no existe."));
        }

        var codigos = solicitud.CodigosPermiso?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToArray() ?? [];
        var permisosDisponibles = await puertoSeguridad.ListarPermisosAsync(cancelacion);
        if (codigos.Any(c => !permisosDisponibles.Any(p => p.Activo && p.Codigo == c)))
            return Resultado<RespuestaRol>.Fallo(ErrorNegocio.Validacion("permiso_invalido", "Todos los códigos de permiso deben existir y estar activos."));
        var actor = autorizacion.Valor!;
        var actualizado = await puertoSeguridad.ActualizarPermisosDeRolAsync(
            idRol,
            codigos,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "roles_permiso",
                idRol.ToString(),
                AccionesAuditoria.Modificar,
                "permisos",
                string.Join(',', rolExistente.CodigosPermiso),
                string.Join(',', codigos),
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaRol>.Exito(actualizado!);
    }

    public async Task<Resultado<RolAsignadoRespuesta>> AsignarRolAUsuarioAsync(
        int idUsuario,
        SolicitudAsignarRolUsuario solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarRoles);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RolAsignadoRespuesta>.Fallo(autorizacion.Error!);
        }

        if (solicitud.FechaFin is DateOnly fechaFin && fechaFin < solicitud.FechaInicio)
        {
            return Resultado<RolAsignadoRespuesta>.Fallo(
                ErrorNegocio.Validacion("fechas_rol_invalidas", "La fecha de finalización del rol no puede ser anterior a su fecha de inicio."));
        }

        var usuario = await puertoSeguridad.ObtenerUsuarioPorIdAsync(idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado<RolAsignadoRespuesta>.Fallo(
                ErrorNegocio.NoEncontrado("usuario_no_encontrado", "La cuenta de usuario indicada no existe."));
        }

        var rol = await puertoSeguridad.ObtenerRolPorIdAsync(solicitud.IdRol, cancelacion);
        if (rol is null || !rol.Activo)
        {
            return Resultado<RolAsignadoRespuesta>.Fallo(
                ErrorNegocio.NoEncontrado("rol_no_encontrado", "El rol indicado no existe o no se encuentra activo."));
        }

        if (rol.Nombre == NombresRol.Trabajador && usuario.IdTrabajador is null)
            return Resultado<RolAsignadoRespuesta>.Fallo(ErrorNegocio.Validacion("trabajador_requerido",
                "Vincule un trabajador activo antes de asignar el rol TRABAJADOR."));

        var actor = autorizacion.Valor!;
        var asignado = await puertoSeguridad.AsignarRolAUsuarioAsync(
            idUsuario,
            solicitud.IdRol,
            solicitud.FechaInicio,
            solicitud.FechaFin,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "usuarios_rol",
                $"{idUsuario}:{solicitud.IdRol}",
                AccionesAuditoria.Crear,
                "id_rol",
                null,
                rol.Nombre,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RolAsignadoRespuesta>.Exito(asignado!);
    }

    public async Task<Resultado<ResultadoAutenticacion>> AutenticarCredencialesAsync(
        SolicitudIniciarSesion solicitud,
        CancellationToken cancelacion = default)
    {
        var nombreNormalizado = solicitud.NombreUsuario?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombreNormalizado) || string.IsNullOrWhiteSpace(solicitud.Contrasena))
        {
            return Resultado<ResultadoAutenticacion>.Fallo(
                ErrorNegocio.NoAutenticado("credenciales_invalidas", "Usuario o contraseña inválidos."));
        }

        var credenciales = await puertoSeguridad.ObtenerCredencialesPorNombreUsuarioAsync(nombreNormalizado, cancelacion);

        var claveValida = servicioHash.VerificarHash(solicitud.Contrasena, credenciales?.ContrasenaHash ?? string.Empty);

        // Respuesta uniforme para no revelar si la cuenta existe o está inactiva/bloqueada (CET-21).
        if (credenciales is null
            || credenciales.Estado != EstadosUsuario.Activo
            || !claveValida)
        {
            return Resultado<ResultadoAutenticacion>.Fallo(
                ErrorNegocio.NoAutenticado("credenciales_invalidas", "Usuario o contraseña inválidos."));
        }

        var instanteUtc = reloj.AhoraUtc;
        var contexto = await puertoSeguridad.ConstruirContextoUsuarioActivoAsync(
            credenciales.IdUsuario,
            calendario.Hoy,
            null,
            cancelacion);

        if (contexto is null)
        {
            return Resultado<ResultadoAutenticacion>.Fallo(
                ErrorNegocio.NoAutenticado("credenciales_invalidas", "Usuario o contraseña inválidos."));
        }

        await puertoSeguridad.RegistrarUltimoAccesoAsync(credenciales.IdUsuario, instanteUtc, cancelacion);
        return Resultado<ResultadoAutenticacion>.Exito(new ResultadoAutenticacion(new RespuestaSesionActiva(
            contexto.IdUsuario,
            contexto.NombreUsuario,
            contexto.IdTrabajador,
            contexto.Roles.Order().ToArray(),
            contexto.Permisos.Order().ToArray(),
            contexto.ObrasAsignadas.Order().ToArray(),
            contexto.TieneAlcanceGlobal,
            instanteUtc), contexto.SelloCredenciales));
    }

    public Resultado<RespuestaSesionActiva> ObtenerPerfilSesionActual()
    {
        var usuario = contextoUsuario.ObtenerUsuario();
        if (usuario is null)
        {
            return Resultado<RespuestaSesionActiva>.Fallo(
                ErrorNegocio.NoAutenticado("sesion_requerida", "No hay una sesión activa."));
        }

        return Resultado<RespuestaSesionActiva>.Exito(new RespuestaSesionActiva(
                usuario.IdUsuario,
                usuario.NombreUsuario,
                usuario.IdTrabajador,
                usuario.Roles.Order().ToArray(),
                usuario.Permisos.Order().ToArray(),
                usuario.ObrasAsignadas.Order().ToArray(),
                usuario.TieneAlcanceGlobal,
                usuario.AutenticadoEnUtc ?? reloj.AhoraUtc));
    }

    public async Task<Resultado<IReadOnlyList<RespuestaAuditoria>>> ConsultarAuditoriasAsync(
        FiltroAuditoria filtro,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarAuditoria);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaAuditoria>>.Fallo(autorizacion.Error!);
        }

        if (filtro.DesdeUtc is { Kind: not DateTimeKind.Utc } || filtro.HastaUtc is { Kind: not DateTimeKind.Utc }
            || filtro.HastaUtc < filtro.DesdeUtc)
            return Resultado<IReadOnlyList<RespuestaAuditoria>>.Fallo(ErrorNegocio.Validacion(
                "periodo_auditoria_invalido", "Use instantes UTC y una fecha final no anterior a la inicial."));
        var limiteNormalizado = Math.Clamp(filtro.Limite <= 0 ? 100 : filtro.Limite, 1, 500);
        var registros = await puertoSeguridad.ConsultarAuditoriasAsync(
            filtro with { Limite = limiteNormalizado },
            cancelacion);

        return Resultado<IReadOnlyList<RespuestaAuditoria>>.Exito(registros);
    }

    private async Task<Resultado> ValidarVinculoTrabajadorAsync(
        int idTrabajador,
        int? excluirIdUsuario,
        CancellationToken cancelacion)
    {
        if (!await puertoSeguridad.ExisteTrabajadorActivoAsync(idTrabajador, cancelacion))
        {
            return Resultado.Fallo(
                ErrorNegocio.Validacion("trabajador_no_disponible", "El trabajador vinculado no existe o se encuentra inactivo."));
        }

        if (await puertoSeguridad.TrabajadorTieneUsuarioAsync(idTrabajador, excluirIdUsuario, cancelacion))
        {
            return Resultado.Fallo(
                ErrorNegocio.Conflicto("trabajador_ya_vinculado", "El trabajador indicado ya está asociado a otra cuenta de usuario."));
        }

        return Resultado.Exito();
    }
    public Task<Resultado<RespuestaRol>> ObtenerRolPorIdAsync(int idRol, CancellationToken cancelacion = default) =>
        ConsultaAutorizada.ObtenerAsync(contextoUsuario, [CodigosPermiso.GestionarRoles],
            () => puertoSeguridad.ObtenerRolPorIdAsync(idRol, cancelacion));
}
