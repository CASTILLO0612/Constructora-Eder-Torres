using Microsoft.AspNetCore.Antiforgery;

namespace ConstructoraEderTorres.Presentacion.Intermediarios;

public sealed class IntermediarioAntifalsificacion(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext contexto, IAntiforgery antifalsificacion)
    {
        if (contexto.Request.Path.StartsWithSegments("/api"))
        {
            contexto.Response.Headers.CacheControl = "no-store";
            contexto.Response.Headers.XContentTypeOptions = "nosniff";
            var metodo = contexto.Request.Method;
            if (!HttpMethods.IsGet(metodo) && !HttpMethods.IsHead(metodo) && !HttpMethods.IsOptions(metodo))
            {
                try
                {
                    await antifalsificacion.ValidateRequestAsync(contexto);
                }
                catch (AntiforgeryValidationException)
                {
                    await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Token de antifalsificación inválido.",
                        detail: "Obtenga un token de /api/sesion/antifalsificacion y envíelo en la cabecera X-CET-CSRF.",
                        extensions: new Dictionary<string, object?> { ["codigo"] = "antifalsificacion_requerida" })
                        .ExecuteAsync(contexto);
                    return;
                }
            }
        }

        await siguiente(contexto);
    }
}
