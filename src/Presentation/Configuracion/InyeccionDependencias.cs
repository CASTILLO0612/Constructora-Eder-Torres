using ConstructoraEderTorres.Presentacion.Intermediarios;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

public static class InyeccionDependencias
{
    public static IServiceCollection AgregarPresentacion(this IServiceCollection servicios)
    {
        servicios.AddProblemDetails(opciones =>
        {
            opciones.CustomizeProblemDetails = contexto =>
            {
                contexto.ProblemDetails.Extensions["identificador"] =
                    contexto.HttpContext.TraceIdentifier;

                if (contexto.ProblemDetails.Status == StatusCodes.Status404NotFound)
                {
                    contexto.ProblemDetails.Title = "Recurso no encontrado.";
                    contexto.ProblemDetails.Detail =
                        "La dirección solicitada no corresponde a un punto de acceso disponible.";
                }
            };
        });

        servicios.AddExceptionHandler<ManejadorExcepciones>();
        servicios.AddOpenApi();

        return servicios;
    }
}
