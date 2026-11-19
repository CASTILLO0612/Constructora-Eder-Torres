using ConstructoraEderTorres.Aplicacion.Comun;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ConstructoraEderTorres.Presentacion.PuntosAcceso;

internal static class MapeadorResultadosHttp
{
    public static Results<Ok<T>, ProblemHttpResult> AResultadoHttp<T>(
        this Resultado<T> resultado,
        HttpContext contextoHttp)
    {
        if (resultado.EsExitoso)
        {
            return TypedResults.Ok(resultado.Valor!);
        }

        return CrearProblema(resultado.Error!, contextoHttp);
    }

    public static Results<Ok<T>, Created<T>, ProblemHttpResult> AResultadoHttp<T>(
        this Resultado<T> resultado,
        HttpContext contextoHttp,
        string? ubicacionCreacion)
    {
        if (!resultado.EsExitoso) return CrearProblema(resultado.Error!, contextoHttp);
        if (string.IsNullOrWhiteSpace(ubicacionCreacion)) return TypedResults.Ok(resultado.Valor!);
        return TypedResults.Created(ubicacionCreacion, resultado.Valor!);
    }

    private static ProblemHttpResult CrearProblema(ErrorNegocio error, HttpContext contextoHttp)
    {
        var (estadoHttp, titulo) = error.Tipo switch
        {
            TipoError.NoAutenticado => (StatusCodes.Status401Unauthorized, "Sesión requerida o inválida."),
            TipoError.Prohibido => (StatusCodes.Status403Forbidden, "Operación no autorizada."),
            TipoError.NoEncontrado => (StatusCodes.Status404NotFound, "Recurso no encontrado."),
            TipoError.Conflicto => (StatusCodes.Status409Conflict, "Conflicto de reglas de negocio."),
            TipoError.NoDisponible => (StatusCodes.Status503ServiceUnavailable, "Servicio no disponible."),
            _ => (StatusCodes.Status400BadRequest, "Solicitud inválida.")
        };

        return TypedResults.Problem(
            title: titulo,
            detail: error.Mensaje,
            statusCode: estadoHttp,
            extensions: new Dictionary<string, object?>
            {
                ["codigo"] = error.Codigo,
                ["identificador"] = contextoHttp.TraceIdentifier
            });
    }
}
