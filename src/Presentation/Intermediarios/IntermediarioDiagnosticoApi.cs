using System.Diagnostics;
using ConstructoraEderTorres.Aplicacion.Comun;
using Microsoft.AspNetCore.Routing;

namespace ConstructoraEderTorres.Presentacion.Intermediarios;

public sealed class IntermediarioDiagnosticoApi(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        if (!contexto.Request.Path.StartsWithSegments("/api")) { await siguiente(contexto); return; }
        var inicio = Stopwatch.GetTimestamp();
        try { await siguiente(contexto); }
        finally
        {
            var ruta = (contexto.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "/sin_ruta";
            var metodo = contexto.Request.Method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "OPTIONS" or "HEAD"
                ? contexto.Request.Method : "OTRO";
            var etiquetas = new TagList { { "http.route", ruta }, { "http.request.method", metodo },
                { "http.response.status_code", contexto.Response.StatusCode } };
            DiagnosticoAplicacion.DuracionApi.Record(Stopwatch.GetElapsedTime(inicio).TotalSeconds, etiquetas);
            DiagnosticoAplicacion.RespuestasApi.Add(1, etiquetas);
        }
    }
}
