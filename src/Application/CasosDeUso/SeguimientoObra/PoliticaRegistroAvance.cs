using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.SeguimientoObra;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.SeguimientoObra;

public static class PoliticaRegistroAvance
{
    public static Resultado<string> DeterminarEstado(SolicitudRegistrarAvance solicitud, RespuestaActividad actividad,
        string estadoAvance, DateOnly hoy)
    {
        if (solicitud.FechaAvance > hoy)
            return Fallo("fecha_avance_futura", "La fecha del avance no puede ser posterior a la jornada actual de Managua.");
        if (PoliticaEstadosActividad.EsTerminal(actividad.Estado))
            return Conflicto("actividad_cerrada", "Una actividad completa o cancelada no admite nuevos avances.");
        if (estadoAvance is not (EstadosAvance.Borrador or EstadosAvance.Registrado))
            return Fallo("estado_inicial_avance_invalido", "Un avance nuevo solo admite BORRADOR o REGISTRADO.");
        var solicitado = string.IsNullOrWhiteSpace(solicitud.EstadoActividad) ? null : solicitud.EstadoActividad.Trim().ToUpperInvariant();
        if (estadoAvance == EstadosAvance.Borrador)
            return solicitado is null ? Resultado<string>.Exito(actividad.Estado)
                : Fallo("estado_actividad_en_borrador", "Un borrador no cambia el estado ni el progreso de la actividad.");

        var total = actividad.CantidadEjecutadaAcumulada + solicitud.CantidadEjecutada;
        var metaAlcanzada = actividad.CantidadPlanificada > 0 && total >= actividad.CantidadPlanificada;
        var retraso = solicitud.EsIncompletoORetrasado || actividad.Estado == EstadosActividad.Retrasada
            || solicitud.FechaAvance > actividad.FechaFinPlan;
        var siguiente = solicitado ?? (metaAlcanzada ? EstadosActividad.Completa
            : retraso ? EstadosActividad.Retrasada : EstadosActividad.EnProceso);
        if (siguiente is not (EstadosActividad.EnProceso or EstadosActividad.Retrasada or EstadosActividad.Completa))
            return Fallo("estado_actividad_en_avance_invalido", "El avance admite actividad EN_PROCESO, RETRASADA o COMPLETA.");
        if (siguiente == EstadosActividad.Completa && total < actividad.CantidadPlanificada
            || metaAlcanzada && siguiente != EstadosActividad.Completa)
            return Conflicto("meta_actividad_inconsistente", "El estado de la actividad debe corresponder a la meta ejecutada.");
        if ((retraso || siguiente == EstadosActividad.Retrasada) && string.IsNullOrWhiteSpace(solicitud.Observaciones))
            return Fallo("justificacion_obligatoria", "La actividad incompleta o retrasada exige registrar su justificación.");

        var anterior = actividad.Estado;
        foreach (var paso in PoliticaEstadosActividad.RutaDeRegistro(anterior, siguiente))
        {
            if (!PoliticaEstadosActividad.PermiteTransicion(anterior, paso))
                return Conflicto("transicion_actividad_invalida", "La transición de actividad no está permitida.");
            anterior = paso;
        }
        return Resultado<string>.Exito(siguiente);
    }

    private static Resultado<string> Fallo(string codigo, string mensaje) => Resultado<string>.Fallo(ErrorNegocio.Validacion(codigo, mensaje));
    private static Resultado<string> Conflicto(string codigo, string mensaje) => Resultado<string>.Fallo(ErrorNegocio.Conflicto(codigo, mensaje));
}
