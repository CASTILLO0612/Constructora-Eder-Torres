using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Obras;

public sealed class ManejadorObras(
    IPuertoObras puertoObras,
    IContextoUsuarioActual contextoUsuario,
    IRelojSistema reloj,
    ICalendarioLaboral calendario)
{
    public async Task<Resultado<RespuestaObra>> CrearObraAsync(
        SolicitudCrearObra solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarObras);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaObra>.Fallo(autorizacion.Error!);
        }

        var codigo = solicitud.Codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!GeocercaObra.EsConfiguracionValida(solicitud.Latitud, solicitud.Longitud, solicitud.RadioAsistenciaMetros))
            return Resultado<RespuestaObra>.Fallo(ErrorNegocio.Validacion("geocerca_invalida",
                "Indique la latitud (-90 a 90), longitud (-180 a 180) y un radio de asistencia positivo en metros."));
        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        var direccion = solicitud.Direccion?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 20)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("codigo_obra_invalido", "El código de la obra es obligatorio y no debe exceder 20 caracteres."));
        }

        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 120)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("nombre_obra_invalido", "El nombre de la obra es obligatorio y no debe exceder 120 caracteres."));
        }

        if (string.IsNullOrWhiteSpace(direccion) || direccion.Length > 200)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("direccion_obra_invalida", "La dirección de la obra es obligatoria y no debe exceder 200 caracteres."));
        }

        if (solicitud.FechaFinPlan < solicitud.FechaInicioPlan)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("fechas_plan_invalidas", "La fecha de finalización planificada no puede ser anterior a la fecha de inicio."));
        }

        var estadoInicial = string.IsNullOrWhiteSpace(solicitud.EstadoInicial)
            ? EstadosObra.Planificada
            : solicitud.EstadoInicial.Trim().ToUpperInvariant();

        if (!EstadosObra.Todos.Contains(estadoInicial))
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("estado_obra_invalido", "El estado de la obra debe ser PLANIFICADA, ACTIVA, SUSPENDIDA o FINALIZADA."));
        }

        if (!PoliticaEstadosObra.PermiteAlta(estadoInicial))
            return Resultado<RespuestaObra>.Fallo(ErrorNegocio.Validacion("estado_inicial_obra_invalido",
                "Una obra nueva debe registrarse PLANIFICADA o ACTIVA."));

        if (await puertoObras.ExisteCodigoObraAsync(codigo, null, cancelacion))
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Conflicto("codigo_obra_duplicado", "Ya existe una obra registrada con ese código."));
        }

        var actor = autorizacion.Valor!;
        var creada = await puertoObras.CrearObraAsync(
            solicitud with
            {
                Codigo = codigo,
                Nombre = nombre,
                Descripcion = solicitud.Descripcion?.Trim(),
                Direccion = direccion,
                Latitud = decimal.Round(solicitud.Latitud!.Value, 6),
                Longitud = decimal.Round(solicitud.Longitud!.Value, 6)
            },
            estadoInicial,
            reloj.AhoraUtc,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "obras",
                codigo,
                AccionesAuditoria.Crear,
                "estado",
                null,
                estadoInicial,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaObra>.Exito(creada);
    }

    public async Task<Resultado<IReadOnlyList<RespuestaObra>>> ListarObrasAsync(
        string? estado = null,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.ConsultarObras);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<IReadOnlyList<RespuestaObra>>.Fallo(autorizacion.Error!);
        }

        string? estadoNormalizado = null;
        if (!string.IsNullOrWhiteSpace(estado))
        {
            estadoNormalizado = estado.Trim().ToUpperInvariant();
            if (!EstadosObra.Todos.Contains(estadoNormalizado))
            {
                return Resultado<IReadOnlyList<RespuestaObra>>.Fallo(
                    ErrorNegocio.Validacion("estado_obra_invalido", "El filtro de estado de obra no es válido."));
            }
        }

        var usuario = autorizacion.Valor!;
        IReadOnlySet<int>? obrasPermitidas = usuario.TieneAlcanceGlobal ? null : usuario.ObrasAsignadas;
        var obras = await puertoObras.ListarObrasAsync(estadoNormalizado, obrasPermitidas, cancelacion);
        return Resultado<IReadOnlyList<RespuestaObra>>.Exito(obras);
    }

    public async Task<Resultado<RespuestaObra>> ObtenerObraPorIdAsync(
        int idObra,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(
            contextoUsuario,
            CodigosPermiso.ConsultarObras,
            idObra);

        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaObra>.Fallo(autorizacion.Error!);
        }

        var obra = await puertoObras.ObtenerObraPorIdAsync(idObra, cancelacion);
        if (obra is null)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra solicitada no existe."));
        }

        return Resultado<RespuestaObra>.Exito(obra);
    }

    public async Task<Resultado<RespuestaObra>> ActualizarObraAsync(
        int idObra,
        SolicitudActualizarObra solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.GestionarObras, idObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaObra>.Fallo(autorizacion.Error!);
        }

        var existente = await puertoObras.ObtenerObraPorIdAsync(idObra, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra solicitada no existe."));
        }

        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        var direccion = solicitud.Direccion?.Trim() ?? string.Empty;
        if (!GeocercaObra.EsConfiguracionValida(solicitud.Latitud, solicitud.Longitud, solicitud.RadioAsistenciaMetros))
            return Resultado<RespuestaObra>.Fallo(ErrorNegocio.Validacion("geocerca_invalida",
                "Indique la latitud (-90 a 90), longitud (-180 a 180) y un radio de asistencia positivo en metros."));
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 120)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("nombre_obra_invalido", "El nombre de la obra es obligatorio y no debe exceder 120 caracteres."));
        }

        if (string.IsNullOrWhiteSpace(direccion) || direccion.Length > 200)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("direccion_obra_invalida", "La dirección de la obra es obligatoria y no debe exceder 200 caracteres."));
        }

        if (solicitud.FechaFinPlan < solicitud.FechaInicioPlan)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("fechas_plan_invalidas", "La fecha de finalización planificada no puede ser anterior a la fecha de inicio."));
        }

        if (solicitud.FechaInicioReal is DateOnly inicioReal
            && solicitud.FechaFinReal is DateOnly finReal
            && finReal < inicioReal)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("fechas_reales_invalidas", "La fecha de finalización real no puede ser anterior a la fecha de inicio real."));
        }

        var actor = autorizacion.Valor!;
        var actualizada = await puertoObras.ActualizarObraAsync(
            idObra,
            solicitud with
            {
                Nombre = nombre,
                Descripcion = solicitud.Descripcion?.Trim(),
                Direccion = direccion,
                Latitud = decimal.Round(solicitud.Latitud!.Value, 6),
                Longitud = decimal.Round(solicitud.Longitud!.Value, 6)
            },
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "obras",
                idObra.ToString(),
                AccionesAuditoria.Modificar,
                "nombre",
                existente.Nombre,
                nombre,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaObra>.Exito(actualizada!);
    }

    public async Task<Resultado<RespuestaObra>> CambiarEstadoObraAsync(
        int idObra,
        SolicitudCambiarEstadoObra solicitud,
        CancellationToken cancelacion = default)
    {
        var autorizacion = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.GestionarObras, idObra);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<RespuestaObra>.Fallo(autorizacion.Error!);
        }

        var nuevoEstado = solicitud.Estado?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!EstadosObra.Todos.Contains(nuevoEstado))
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("estado_obra_invalido", "El estado de la obra debe ser PLANIFICADA, ACTIVA, SUSPENDIDA o FINALIZADA."));
        }

        var existente = await puertoObras.ObtenerObraPorIdAsync(idObra, cancelacion);
        if (existente is null)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra solicitada no existe."));
        }

        if (!PoliticaEstadosObra.PermiteTransicion(existente.Estado, nuevoEstado))
            return Resultado<RespuestaObra>.Fallo(ErrorNegocio.Conflicto("transicion_obra_invalida",
                $"No se permite cambiar una obra de {existente.Estado} a {nuevoEstado}."));

        var inicioReal = solicitud.FechaInicioReal ?? existente.FechaInicioReal;
        var finReal = solicitud.FechaFinReal ?? existente.FechaFinReal;

        if (nuevoEstado == EstadosObra.Activa && inicioReal is null)
        {
            inicioReal = calendario.Hoy;
        }

        if (nuevoEstado == EstadosObra.Finalizada && finReal is null)
        {
            finReal = calendario.Hoy;
        }

        if (inicioReal is DateOnly inicio && finReal is DateOnly fin && fin < inicio)
        {
            return Resultado<RespuestaObra>.Fallo(
                ErrorNegocio.Validacion("fechas_reales_invalidas", "La fecha de finalización real no puede ser anterior a la fecha de inicio real."));
        }

        var actor = autorizacion.Valor!;
        var actualizada = await puertoObras.CambiarEstadoObraAsync(
            idObra,
            nuevoEstado,
            inicioReal,
            finReal,
            new RegistroEventoAuditoria(
                actor.IdUsuario,
                "obras",
                idObra.ToString(),
                AccionesAuditoria.Modificar,
                "estado",
                existente.Estado,
                nuevoEstado,
                actor.IpOrigen),
            cancelacion);

        return Resultado<RespuestaObra>.Exito(actualizada!);
    }
}
