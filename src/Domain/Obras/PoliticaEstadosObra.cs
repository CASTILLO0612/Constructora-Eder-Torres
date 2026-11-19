using ConstructoraEderTorres.Dominio.Reglas;

namespace ConstructoraEderTorres.Dominio.Obras;

public static class PoliticaEstadosObra
{
    public static bool PermiteAlta(string estado) => estado is EstadosObra.Planificada or EstadosObra.Activa;

    public static bool PermiteTransicion(string actual, string siguiente) => actual == siguiente
        || (actual, siguiente) is (EstadosObra.Planificada, EstadosObra.Activa)
            or (EstadosObra.Activa, EstadosObra.Suspendida)
            or (EstadosObra.Suspendida, EstadosObra.Activa)
            or (EstadosObra.Activa, EstadosObra.Finalizada)
            or (EstadosObra.Suspendida, EstadosObra.Finalizada);
}
