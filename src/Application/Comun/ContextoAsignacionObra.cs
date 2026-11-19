using ConstructoraEderTorres.Dominio.Reglas;

namespace ConstructoraEderTorres.Aplicacion.Comun;

public readonly record struct ContextoAsignacionObra(
    int IdObra, int IdTrabajador, string EstadoAsignacion, string EstadoTrabajador,
    DateOnly FechaInicio, DateOnly? FechaFin)
{
    public bool EsVigenteEn(DateOnly fecha) => EstadoAsignacion == EstadosAsignacionObra.Activa
        && EstadoTrabajador == EstadosTrabajador.Activo && FechaInicio <= fecha
        && (FechaFin is null || FechaFin >= fecha);
}
