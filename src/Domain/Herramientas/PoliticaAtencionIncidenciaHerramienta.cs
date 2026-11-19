using ConstructoraEderTorres.Dominio.Reglas;

namespace ConstructoraEderTorres.Dominio.Herramientas;

public static class PoliticaAtencionIncidenciaHerramienta
{
    public static bool PermiteAtencion(string actual, string siguiente) =>
        (actual, siguiente) is (EstadosIncidenciaHerramienta.Abierta, EstadosIncidenciaHerramienta.EnProceso)
            or (EstadosIncidenciaHerramienta.Abierta, EstadosIncidenciaHerramienta.Resuelta)
            or (EstadosIncidenciaHerramienta.EnProceso, EstadosIncidenciaHerramienta.Resuelta);
}
