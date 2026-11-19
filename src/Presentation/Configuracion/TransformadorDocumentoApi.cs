using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Comun;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

internal sealed class TransformadorDocumentoApi(OpcionesSesion sesion) : IOpenApiDocumentTransformer
{
    internal const string EsquemaSesion = "SesionCookie";

    public Task TransformAsync(OpenApiDocument documento, OpenApiDocumentTransformerContext contexto,
        CancellationToken cancelacion)
    {
        documento.Info.Title = "API de Constructora Eder Torres";
        documento.Info.Version = "v1";
        documento.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        documento.Extensions["x-limites-busqueda"] = new JsonNodeExtension(JsonSerializer.SerializeToNode(new
        {
            longitudMinima = BusquedaTexto.LongitudMinima,
            longitudMaxima = BusquedaTexto.LongitudMaxima,
            maximoPalabras = BusquedaTexto.MaximoPalabras
        })!);
        documento.Info.Description = "Seguimiento operativo por obra. La sesión usa cookie HttpOnly. "
            + "Antes del login, obtenga el token en GET /api/sesion/antifalsificacion. "
            + "Después del login solicite un token nuevo y envíelo en X-CET-CSRF para cada escritura. "
            + "Desde React use credentials: 'include'. Las fechas y horas se envían en UTC con sufijo Z. "
            + "Los identificadores se obtienen de las respuestas; no hay cuentas ni IDs predeterminados.";
        documento.Components ??= new();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        documento.Components.SecuritySchemes[EsquemaSesion] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = sesion.NombreCookie,
            Description = "Cookie emitida por POST /api/sesion/iniciar y enviada automáticamente por el navegador."
        };
        return Task.CompletedTask;
    }
}
