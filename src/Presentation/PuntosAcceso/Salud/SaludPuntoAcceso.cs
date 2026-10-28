using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso.Salud;

public static class SaludPuntoAcceso
{
    public static IEndpointRouteBuilder MapearPuntosAccesoSalud(
        this IEndpointRouteBuilder puntosAcceso)
    {
        var grupo = puntosAcceso.MapGroup("/salud").WithTags("Salud");

        grupo.AddEndpointFilter(async (contexto, siguiente) =>
        {
            contexto.HttpContext.Response.Headers.CacheControl = "no-store";
            return await siguiente(contexto);
        });

        grupo.MapGet("/viva", () => TypedResults.Ok(
                new RespuestaSalud("saludable", new Dictionary<string, string>())))
            .WithName("ConsultarVitalidad")
            .WithSummary("Comprueba que la API puede responder.");

        grupo.MapGet("/lista", ConsultarDisponibilidad)
            .WithName("ConsultarDisponibilidad")
            .WithSummary("Comprueba la disponibilidad de PostgreSQL.")
            .Produces<RespuestaSalud>()
            .Produces<RespuestaSalud>(StatusCodes.Status503ServiceUnavailable);

        return puntosAcceso;
    }

    private static async Task<IResult> ConsultarDisponibilidad(
        HealthCheckService comprobaciones,
        CancellationToken cancelacion)
    {
        var informe = await comprobaciones.CheckHealthAsync(
            comprobacion => comprobacion.Tags.Contains("disponibilidad"),
            cancelacion);

        var respuesta = new RespuestaSalud(
            TraducirEstado(informe.Status),
            informe.Entries.ToDictionary(
                entrada => entrada.Key,
                entrada => TraducirEstado(entrada.Value.Status)));

        var codigo = informe.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return Results.Json(respuesta, statusCode: codigo);
    }

    private static string TraducirEstado(HealthStatus estado) => estado switch
    {
        HealthStatus.Healthy => "saludable",
        HealthStatus.Degraded => "degradado",
        _ => "no_disponible"
    };
}
