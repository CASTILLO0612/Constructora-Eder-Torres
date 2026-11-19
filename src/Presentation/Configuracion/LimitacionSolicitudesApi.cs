using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

public static class LimitacionSolicitudesApi
{
    public static IServiceCollection AgregarLimitesSolicitudes(this IServiceCollection servicios)
    {
        servicios.AddSingleton(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<IConfiguration>().GetSection(OpcionesLimitesSolicitudes.Seccion)
                .Get<OpcionesLimitesSolicitudes>() ?? new();
            opciones.Validar();
            return opciones;
        });
        servicios.AddRateLimiter(_ => { });
        servicios.AddOptions<RateLimiterOptions>().Configure<OpcionesLimitesSolicitudes, OpcionesApi>((opciones, limites, api) =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opciones.OnRejected = async (contexto, _) =>
            {
                var espera = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var demora)
                    ? Math.Max(1, Math.Ceiling(demora.TotalSeconds)) : 1;
                contexto.HttpContext.Response.Headers.RetryAfter = espera.ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Límite temporal de solicitudes alcanzado.",
                    detail: "Espere el tiempo indicado en Retry-After y reintente conservando el UUID de la operación.",
                    extensions: new Dictionary<string, object?> { ["codigo"] = "limite_solicitudes" })
                    .ExecuteAsync(contexto.HttpContext);
            };
            opciones.AddPolicy(InyeccionDependencias.PoliticaInicioSesion, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(contexto.Connection.RemoteIpAddress?.ToString() ?? "sin_ip",
                    _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = api.IntentosInicioSesionPorMinuto, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            opciones.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                {
                    if (ObtenerPerfil(contexto) is not { } perfil) return RateLimitPartition.GetNoLimiter("sin_limite");
                    var limite = limites.Obtener(perfil);
                    var identidad = contexto.User.Identity?.IsAuthenticated == true
                        ? "usuario:" + contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        : "ip:" + (contexto.Connection.RemoteIpAddress?.ToString() ?? "sin_ip");
                    var ruta = (contexto.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "sin_ruta";
                    return RateLimitPartition.GetFixedWindowLimiter(identidad + ":" + contexto.Request.Method + ":" + ruta,
                        _ => new FixedWindowRateLimiterOptions
                        { PermitLimit = limite.Permisos, Window = limite.Ventana, QueueLimit = 0 });
                }),
                PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                {
                    if (ObtenerPerfil(contexto) is not { } perfil || limites.Obtener(perfil).MaximoSimultaneo == 0)
                        return RateLimitPartition.GetNoLimiter("sin_limite");
                    return RateLimitPartition.GetConcurrencyLimiter(perfil.ToString(), _ => new ConcurrencyLimiterOptions
                    { PermitLimit = limites.Obtener(perfil).MaximoSimultaneo, QueueLimit = 0 });
                }));
        });
        return servicios;
    }

    private static PerfilLimiteApi? ObtenerPerfil(HttpContext contexto)
    {
        if (!contexto.Request.Path.StartsWithSegments("/api")
            || contexto.GetEndpoint() is null
            || contexto.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                == InyeccionDependencias.PoliticaInicioSesion) return null;
        if (HttpMethods.IsGet(contexto.Request.Method) || HttpMethods.IsHead(contexto.Request.Method))
            return PerfilLimiteApi.Consulta;
        return contexto.GetEndpoint()?.Metadata.GetMetadata<LimiteOperacionApi>()?.Perfil ?? PerfilLimiteApi.Escritura;
    }
}
