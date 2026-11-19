using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Dominio.Reglas;

namespace ConstructoraEderTorres.Aplicacion.Comun;

public sealed class PoliticaOperacionesObra(IPuertoObras obras)
{
    public Task<Resultado> ExigirActivaAsync(int idObra, CancellationToken cancelacion = default) =>
        ValidarAsync(idObra, permitirPlanificada: false, cancelacion);

    public Task<Resultado> ExigirPlanificableAsync(int idObra, CancellationToken cancelacion = default) =>
        ValidarAsync(idObra, permitirPlanificada: true, cancelacion);

    private async Task<Resultado> ValidarAsync(int idObra, bool permitirPlanificada, CancellationToken cancelacion)
    {
        var obra = await obras.ObtenerObraPorIdAsync(idObra, cancelacion);
        if (obra is null) return Resultado.Fallo(ErrorNegocio.NoEncontrado("obra_no_encontrada", "La obra indicada no existe."));
        if (obra.Estado == EstadosObra.Activa || permitirPlanificada && obra.Estado == EstadosObra.Planificada)
            return Resultado.Exito();
        return Resultado.Fallo(ErrorNegocio.Conflicto("obra_no_activa", permitirPlanificada
            ? "La obra debe estar PLANIFICADA o ACTIVA para preparar actividades y asignaciones."
            : "La obra debe estar ACTIVA para registrar esta operación."));
    }
}
