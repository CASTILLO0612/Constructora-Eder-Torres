using ConstructoraEderTorres.Aplicacion.Comun;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;

namespace ConstructoraEderTorres.Presentacion.Intermediarios;

internal sealed class ManejadorExcepciones(
    ILogger<ManejadorExcepciones> registro,
    IProblemDetailsService problemas) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken cancelacion)
    {
        var (estado, titulo, detalle, codigo) = excepcion switch
        {
            ExcepcionPersistencia persistencia => (
                persistencia.Error.Tipo == TipoError.NoDisponible ? 503 : persistencia.Error.Tipo == TipoError.Validacion ? 400 : 409,
                "No se pudo completar la operación.", persistencia.Error.Mensaje, persistencia.Error.Codigo),
            BadHttpRequestException solicitud => (solicitud.StatusCode, "Solicitud inválida.",
                "Revise el formato JSON, los campos y el tamaño de la solicitud.", "solicitud_invalida"),
            _ when ErroresConexionPostgreSql.EsNoDisponible(excepcion) => (503, "Base de datos no disponible.",
                "No se pudo completar la operación. Reintente cuando se restablezca la conexión.", "persistencia_no_disponible"),
            _ => (500, "Ocurrió un error interno.",
                "No se pudo completar la solicitud. Utilice el identificador para reportar el error.", "error_interno")
        };
        if (estado >= 500)
            registro.LogError(excepcion, "Error en la solicitud. Identificador: {Identificador}", contexto.TraceIdentifier);
        else
            registro.LogWarning("Solicitud rechazada con {Codigo}. Identificador: {Identificador}", codigo, contexto.TraceIdentifier);
        contexto.Response.StatusCode = estado;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = new ProblemDetails
            {
                Status = estado,
                Title = titulo,
                Detail = detalle,
                Instance = contexto.Request.Path,
                Extensions = { ["codigo"] = codigo }
            }
        });
    }
}
