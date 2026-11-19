namespace ConstructoraEderTorres.Presentacion.Intermediarios;

/// <summary>Protege las respuestas JSON, incluidos errores anteriores a la autenticación.</summary>
public sealed class IntermediarioCabecerasSeguridad(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        contexto.Response.OnStarting(() =>
        {
            var cabeceras = contexto.Response.Headers;
            cabeceras.XContentTypeOptions = "nosniff";
            cabeceras["X-Frame-Options"] = "DENY";
            cabeceras["Referrer-Policy"] = "no-referrer";
            cabeceras["Permissions-Policy"] = "geolocation=(self), camera=(self), microphone=(), payment=(), usb=()";
            if (contexto.Request.Path.StartsWithSegments("/api") || contexto.Request.Path.StartsWithSegments("/salud"))
            {
                cabeceras.CacheControl = "no-store";
                cabeceras["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            }
            return Task.CompletedTask;
        });
        await siguiente(contexto);
    }
}
