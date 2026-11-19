using ConstructoraEderTorres.Presentacion.Configuracion;
using ConstructoraEderTorres.Aplicacion.Comun;

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
            if (!BusquedaTexto.EsValida(consulta["buscar"]))
            {
                await Results.Problem(statusCode: 400, title: "Búsqueda inválida.",
                    detail: $"Escriba entre {BusquedaTexto.LongitudMinima} y {BusquedaTexto.LongitudMaxima} caracteres y hasta {BusquedaTexto.MaximoPalabras} palabras.")
                    .ExecuteAsync(contexto);
                return;
            }
            var pagina = 1;
            var tamano = opciones.TamanoPaginaPredeterminado;
            if (consulta.ContainsKey("pagina") && (!int.TryParse(consulta["pagina"], out pagina) || pagina < 1)
                || consulta.ContainsKey("tamanoPagina") && (!int.TryParse(consulta["tamanoPagina"], out tamano) || tamano < 1 || tamano > opciones.TamanoPaginaMaximo)
                )
            {
                await Results.Problem(statusCode: 400, title: "Paginación inválida.",
                    detail: $"Use pagina >= 1 y tamanoPagina entre 1 y {opciones.TamanoPaginaMaximo}.")
                    .ExecuteAsync(contexto);
                return;
            }
            if (contexto.Request.Path == "/api/auditorias" && int.TryParse(consulta["limite"], out var limite) && limite is > 0 and <= 500)
                tamano = consulta.ContainsKey("tamanoPagina") ? Math.Min(tamano, limite) : limite;
            if (((long)pagina - 1) * tamano > int.MaxValue)
            {
                await Results.Problem(statusCode: 400, title: "Paginación inválida.", detail: "La página solicitada excede el rango permitido.")
                    .ExecuteAsync(contexto);
                return;
            }
            contexto.Response.Headers["X-Pagina"] = pagina.ToString(System.Globalization.CultureInfo.InvariantCulture);
            contexto.Response.Headers["X-Tamano-Pagina"] = tamano.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        await siguiente(contexto);
    }
}
