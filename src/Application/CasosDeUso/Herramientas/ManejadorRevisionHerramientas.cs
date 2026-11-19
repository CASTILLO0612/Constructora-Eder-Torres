using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Herramientas;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Herramientas;

public sealed partial class ManejadorHerramientas
{
    public async Task<Resultado<RespuestaHerramienta>> ActualizarHerramientaAsync(int idHerramienta,
        SolicitudActualizarHerramienta solicitud, CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioHerramientas);
        if (!permiso.EsExitoso) return Resultado<RespuestaHerramienta>.Fallo(permiso.Error!);
        var existente = await puertoHerramientas.ObtenerHerramientaPorIdAsync(idHerramienta, cancelacion);
        if (existente is null) return Resultado<RespuestaHerramienta>.Fallo(ErrorNegocio.NoEncontrado(
            "herramienta_no_encontrada", "La herramienta no existe."));
        var serie = string.IsNullOrWhiteSpace(solicitud.NumeroSerie) ? null : solicitud.NumeroSerie.Trim();
        if (serie?.Length > 60 || solicitud.Marca?.Trim().Length > 60 || solicitud.Modelo?.Trim().Length > 60)
            return Resultado<RespuestaHerramienta>.Fallo(ErrorNegocio.Validacion("datos_herramienta_invalidos",
                "La serie, marca y modelo admiten un máximo de 60 caracteres."));
        if (serie != existente.NumeroSerie && serie is not null
            && await puertoHerramientas.ExisteHerramientaPorSerieAsync(serie, cancelacion))
            return Resultado<RespuestaHerramienta>.Fallo(ErrorNegocio.Conflicto("serie_herramienta_duplicada",
                "La serie ya pertenece a otra herramienta."));
        var actor = permiso.Valor!;
        var respuesta = await puertoHerramientas.ActualizarHerramientaAsync(idHerramienta,
            solicitud with { NumeroSerie = serie, Marca = solicitud.Marca?.Trim(), Modelo = solicitud.Modelo?.Trim() },
            new RegistroEventoAuditoria(actor.IdUsuario, "herramientas", idHerramienta.ToString(),
                AccionesAuditoria.Modificar, "datos_inventario", null, "actualizados", actor.IpOrigen), cancelacion);
        return Resultado<RespuestaHerramienta>.Exito(respuesta!);
    }

    public async Task<Resultado<RespuestaHerramienta>> RevisarHerramientaAsync(int idHerramienta,
        SolicitudRevisarHerramienta solicitud, CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.GestionarInventarioHerramientas);
        if (!permiso.EsExitoso) return Resultado<RespuestaHerramienta>.Fallo(permiso.Error!);
        var equipo = await puertoHerramientas.ObtenerHerramientaPorIdAsync(idHerramienta, cancelacion);
        if (equipo is null) return Resultado<RespuestaHerramienta>.Fallo(
            ErrorNegocio.NoEncontrado("herramienta_no_encontrada", "La herramienta no existe."));
        if (equipo.Estado != EstadosHerramienta.EnRevision
            || await puertoHerramientas.TieneAsignacionHerramientaActivaAsync(idHerramienta, cancelacion)
            || await puertoHerramientas.TieneIncidenciasPendientesAsync(idHerramienta, cancelacion))
            return Resultado<RespuestaHerramienta>.Fallo(ErrorNegocio.Conflicto("revision_no_disponible",
                "La herramienta debe estar en revisión, sin asignaciones activas ni incidencias pendientes."));
        var condicion = solicitud.Condicion?.Trim().ToUpperInvariant() ?? "";
        if (!CondicionesHerramienta.Todas.Contains(condicion) || (!solicitud.Retirar && condicion == CondicionesHerramienta.Danada)
            || string.IsNullOrWhiteSpace(solicitud.Motivo) || solicitud.Motivo.Length > 500)
            return Resultado<RespuestaHerramienta>.Fallo(ErrorNegocio.Validacion("revision_herramienta_invalida",
                "Indique condición y motivo (hasta 500 caracteres). Una herramienta dañada no puede volver a disponible."));
        var estado = solicitud.Retirar ? EstadosHerramienta.Retirada : EstadosHerramienta.Disponible;
        var actor = permiso.Valor!;
        var respuesta = await puertoHerramientas.RevisarHerramientaAsync(idHerramienta, condicion, estado,
            new RegistroEventoAuditoria(actor.IdUsuario, "herramientas", idHerramienta.ToString(),
                AccionesAuditoria.Modificar, "revision", equipo.Estado, estado + ": " + solicitud.Motivo.Trim(), actor.IpOrigen), cancelacion);
        return Resultado<RespuestaHerramienta>.Exito(respuesta!);
    }

    public async Task<Resultado<RespuestaIncidenciaHerramienta>> AtenderIncidenciaAsync(long idIncidencia,
        SolicitudAtenderIncidenciaHerramienta solicitud, CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermiso(contextoUsuario, CodigosPermiso.RegistrarIncidenciasHerramientas);
        if (!permiso.EsExitoso) return Resultado<RespuestaIncidenciaHerramienta>.Fallo(permiso.Error!);
        var incidencia = await puertoHerramientas.ObtenerIncidenciaPorIdAsync(idIncidencia, cancelacion);
        if (incidencia is null) return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
            ErrorNegocio.NoEncontrado("incidencia_no_encontrada", "La incidencia no existe."));
        if (!permiso.Valor!.TieneAccesoAObra(incidencia.IdObra)) return Resultado<RespuestaIncidenciaHerramienta>.Fallo(
            ErrorNegocio.Prohibido("obra_fuera_de_alcance", "No tiene acceso a la obra de la incidencia."));
        var estado = solicitud.Estado?.Trim().ToUpperInvariant() ?? "";
        if (!PoliticaAtencionIncidenciaHerramienta.PermiteAtencion(incidencia.Estado, estado))
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(ErrorNegocio.Conflicto("transicion_incidencia_invalida",
                "Solo una incidencia abierta o en proceso puede avanzar a su resolución."));
        if (string.IsNullOrWhiteSpace(solicitud.Resolucion) || solicitud.Resolucion.Length > 500 || solicitud.CostoEstimado < 0)
            return Resultado<RespuestaIncidenciaHerramienta>.Fallo(ErrorNegocio.Validacion("resolucion_invalida",
                "Indique la atención o resolución (hasta 500 caracteres) y un costo no negativo."));
        var actor = permiso.Valor!;
        var respuesta = await puertoHerramientas.AtenderIncidenciaAsync(idIncidencia, estado, solicitud.Resolucion.Trim(),
            solicitud.CostoEstimado, new RegistroEventoAuditoria(actor.IdUsuario, "incidencias_herramienta",
                idIncidencia.ToString(), AccionesAuditoria.Modificar, "estado", incidencia.Estado, estado, actor.IpOrigen), cancelacion);
        return Resultado<RespuestaIncidenciaHerramienta>.Exito(respuesta!);
    }
}
