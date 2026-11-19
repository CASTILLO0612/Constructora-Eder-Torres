using ConstructoraEderTorres.Presentacion.Configuracion;

namespace ConstructoraEderTorres.Presentacion.Intermediarios;

public sealed class IntermediarioLimitesApi(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext contexto, OpcionesApi opciones)
    {
        if (contexto.Request.Path.StartsWithSegments("/api"))
        {
            contexto.Response.Headers.CacheControl = "no-store";
            contexto.Response.Headers.XContentTypeOptions = "nosniff";
            if (contexto.Request.ContentLength > opciones.MaximoBytesSolicitud)
            {
                await Results.Problem(statusCode: 413, title: "Solicitud demasiado grande.")
                    .ExecuteAsync(contexto);
                return;
            }
            // También acota cuerpos sin Content-Length (por ejemplo, transferencia fragmentada).
            contexto.Request.Body = new FlujoLecturaLimitado(contexto.Request.Body, opciones.MaximoBytesSolicitud);

            var consulta = contexto.Request.Query;
            var pagina = 1;
            var tamano = opciones.TamanoPaginaPredeterminado;
            if (consulta.ContainsKey("pagina") && (!int.TryParse(consulta["pagina"], out pagina) || pagina < 1)
                || consulta.ContainsKey("tamanoPagina") && (!int.TryParse(consulta["tamanoPagina"], out tamano) || tamano < 1 || tamano > opciones.TamanoPaginaMaximo)
                || ((long)pagina - 1) * tamano > int.MaxValue)
            {
                await Results.Problem(statusCode: 400, title: "Paginación inválida.",
                    detail: $"Use pagina >= 1 y tamanoPagina entre 1 y {opciones.TamanoPaginaMaximo}.")
                    .ExecuteAsync(contexto);
                return;
            }
            contexto.Response.Headers["X-Pagina"] = pagina.ToString(System.Globalization.CultureInfo.InvariantCulture);
            contexto.Response.Headers["X-Tamano-Pagina"] = tamano.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        await siguiente(contexto);
    }
}
