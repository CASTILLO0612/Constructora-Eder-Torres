using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ConstructoraEderTorres.Presentacion.Intermediarios;

internal sealed class ManejadorExcepciones(
    ILogger<ManejadorExcepciones> registro,
    IProblemDetailsService problemas) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken cancelacion)
    {
        registro.LogError(
            excepcion,
            "Error no controlado. Identificador: {Identificador}",
            contexto.TraceIdentifier);

        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrió un error interno.",
                Detail = "No se pudo completar la solicitud. Utilice el identificador para reportar el error.",
                Instance = contexto.Request.Path
            }
        });
    }
}
