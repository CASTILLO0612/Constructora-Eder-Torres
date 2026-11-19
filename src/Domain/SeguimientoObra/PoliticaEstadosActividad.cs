using ConstructoraEderTorres.Dominio.Reglas;

namespace ConstructoraEderTorres.Dominio.SeguimientoObra;

public static class PoliticaEstadosActividad
{
    public static bool EsTerminal(string estado) => estado is EstadosActividad.Completa or EstadosActividad.Cancelada;

    public static bool PermiteTransicion(string actual, string siguiente) => actual == siguiente
        || (actual, siguiente) is (EstadosActividad.Planificada, EstadosActividad.EnProceso)
            or (EstadosActividad.Planificada, EstadosActividad.Cancelada)
            or (EstadosActividad.EnProceso, EstadosActividad.Retrasada)
            or (EstadosActividad.Retrasada, EstadosActividad.EnProceso)
            or (EstadosActividad.EnProceso, EstadosActividad.Completa)
            or (EstadosActividad.Retrasada, EstadosActividad.Completa);

    public static IReadOnlyList<string> RutaDeRegistro(string actual, string siguiente)
    {
        if (actual == siguiente) return [];
        return actual == EstadosActividad.Planificada && siguiente != EstadosActividad.EnProceso
            ? [EstadosActividad.EnProceso, siguiente] : [siguiente];
    }
}
