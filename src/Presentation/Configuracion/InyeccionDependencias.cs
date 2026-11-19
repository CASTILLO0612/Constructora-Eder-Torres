using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Presentacion.Identidad;
using ConstructoraEderTorres.Presentacion.Intermediarios;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

public static class InyeccionDependencias
{
    public const string PoliticaCorsConstructora = "PoliticaCorsConstructora";
    public const string PoliticaInicioSesion = "inicio_sesion";
    public const string CabeceraAntifalsificacion = "X-CET-CSRF";

    public static IServiceCollection AgregarPresentacion(this IServiceCollection servicios)
    {
        servicios.TryAddSingleton(TimeProvider.System);
        servicios.AddSingleton(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<IConfiguration>()
                .GetSection(OpcionesSesion.Seccion).Get<OpcionesSesion>() ?? new();
            opciones.Validar();
            return opciones;
        });
        servicios.AddSingleton(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<IConfiguration>()
                .GetSection(OpcionesApi.Seccion).Get<OpcionesApi>() ?? new();
            if (opciones.MaximoBytesSolicitud <= 0 || opciones.TamanoPaginaPredeterminado <= 0
                || opciones.TamanoPaginaMaximo < opciones.TamanoPaginaPredeterminado
                || opciones.TamanoPaginaMaximo > 10_000 || opciones.MaximoElementosSolicitud < 1
                || opciones.IntentosInicioSesionPorMinuto < 1)
                throw new InvalidOperationException("La configuración de límites de Api no es válida.");
            return opciones;
        });
        servicios.AddOptions<KestrelServerOptions>().Configure<OpcionesApi>(
            (servidor, opciones) => { servidor.Limits.MaxRequestBodySize = opciones.MaximoBytesSolicitud; servidor.AddServerHeader = false; });
        servicios.AddHttpContextAccessor();
        servicios.AddScoped<IContextoUsuarioActual, ContextoUsuarioHttp>();
        servicios.AddScoped(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<OpcionesApi>();
            var solicitud = proveedor.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request;
            if (solicitud is null || !HttpMethods.IsGet(solicitud.Method)) return new PaginacionConsulta(1, int.MaxValue);
            var consulta = solicitud.Query;
            var pagina = consulta is not null && int.TryParse(consulta["pagina"], out var p) ? p : 1;
            var tamano = consulta is not null && int.TryParse(consulta["tamanoPagina"], out var t) ? t : opciones.TamanoPaginaPredeterminado;
            return new PaginacionConsulta(pagina, tamano);
        });

        servicios.AddProblemDetails(opciones => opciones.CustomizeProblemDetails = contexto =>
        {
            contexto.ProblemDetails.Extensions["identificador"] = contexto.HttpContext.TraceIdentifier;
            if (contexto.ProblemDetails.Status == StatusCodes.Status404NotFound && contexto.ProblemDetails.Detail is null)
            {
                contexto.ProblemDetails.Title = "Recurso no encontrado.";
                contexto.ProblemDetails.Detail = "La dirección solicitada no corresponde a un punto de acceso disponible.";
            }
        });
        servicios.AddExceptionHandler<ManejadorExcepciones>();
        servicios.AddOpenApi(opciones =>
        {
            opciones.AddDocumentTransformer<TransformadorDocumentoApi>();
            opciones.AddOperationTransformer<TransformadorOperacionApi>();
        });
        servicios.ConfigureHttpJsonOptions(opciones =>
        {
            opciones.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            opciones.SerializerOptions.Converters.Add(new ConvertidorFechaHoraUtc());
        });

        servicios.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        servicios.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<OpcionesSesion, TimeProvider>((opciones, sesion, tiempo) =>
            {
                opciones.Cookie.Name = sesion.NombreCookie;
                opciones.Cookie.HttpOnly = true;
                opciones.Cookie.IsEssential = true;
                opciones.Cookie.SameSite = Enum.Parse<SameSiteMode>(sesion.PoliticaSameSite);
                opciones.Cookie.SecurePolicy = Enum.Parse<CookieSecurePolicy>(sesion.PoliticaSeguridadCookie);
                opciones.ExpireTimeSpan = TimeSpan.FromMinutes(sesion.MinutosInactividad);
                opciones.SlidingExpiration = true;
                opciones.TimeProvider = tiempo;
                opciones.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = contexto => ContextoUsuarioHttp.ValidarPrincipalCookieAsync(contexto, sesion),
                    OnRedirectToLogin = contexto =>
                    {
                        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = contexto =>
                    {
                        contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                };
            });
        servicios.AddAuthorization();
        servicios.AddAntiforgery(opciones => opciones.HeaderName = CabeceraAntifalsificacion);
        servicios.AddOptions<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>()
            .Configure<OpcionesSesion>((opciones, sesion) =>
            {
                opciones.Cookie.Name = sesion.NombreCookie + "_csrf";
                opciones.Cookie.HttpOnly = true;
                opciones.Cookie.SameSite = Enum.Parse<SameSiteMode>(sesion.PoliticaSameSite);
                opciones.Cookie.SecurePolicy = Enum.Parse<CookieSecurePolicy>(sesion.PoliticaSeguridadCookie);
            });

        servicios.AddCors();
        servicios.AddOptions<CorsOptions>().Configure<OpcionesSesion>((cors, sesion) =>
            cors.AddPolicy(PoliticaCorsConstructora, new CorsPolicyBuilder()
                .WithOrigins(sesion.OrigenesCorsPermitidos).AllowAnyHeader()
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                .WithExposedHeaders("X-Pagina", "X-Tamano-Pagina", "Location", "Content-Disposition", "Retry-After")
                .AllowCredentials().Build()));

        servicios.AgregarLimitesSolicitudes();
        return servicios;
    }
}
