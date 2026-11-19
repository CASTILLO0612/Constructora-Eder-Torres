using System.Collections;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

internal sealed class TransformadorOperacionApi(OpcionesApi opciones) : IOpenApiOperationTransformer
{
    public async Task TransformAsync(OpenApiOperation operacion, OpenApiOperationTransformerContext contexto,
        CancellationToken cancelacion)
    {
        var descripcion = contexto.Description;
        var ruta = descripcion.RelativePath ?? string.Empty;
        if (!ruta.StartsWith("api/", StringComparison.Ordinal)) return;
        var metodo = descripcion.HttpMethod ?? "GET";
        var escritura = metodo is "POST" or "PUT" or "PATCH" or "DELETE";
        var anonima = descripcion.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        operacion.OperationId ??= CrearIdentificador(metodo, ruta);
        operacion.Parameters ??= [];
        operacion.Security = anonima ? [] :
        [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(TransformadorDocumentoApi.EsquemaSesion, contexto.Document)] = [] }];

        if (escritura)
        {
            operacion.Parameters.Add(new OpenApiParameter
            {
                Name = InyeccionDependencias.CabeceraAntifalsificacion,
                In = ParameterLocation.Header,
                Required = true,
                Description = "Token de /api/sesion/antifalsificacion obtenido con las cookies de la sesión actual.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }

        var paginada = !escritura && descripcion.SupportedResponseTypes.Any(r => r.Type != typeof(string)
            && r.Type is not null && typeof(IEnumerable).IsAssignableFrom(r.Type));
        if (paginada)
        {
            AgregarPaginacion(operacion, "pagina", 1, null);
            AgregarPaginacion(operacion, "tamanoPagina", opciones.TamanoPaginaPredeterminado, opciones.TamanoPaginaMaximo);
        }

        var problema = await contexto.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, cancelacion);
        operacion.Responses ??= new();
        if (descripcion.SupportedResponseTypes.Any(r => r.Type == typeof(Stream)))
            operacion.Responses["200"] = new OpenApiResponse
            {
                Description = "Archivo de evidencia con descarga autenticada y alcance de obra.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/octet-stream"] = new()
                    { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } }
                }
            };
        var errores = new Dictionary<string, string>
        {
            ["400"] = "Contrato, filtros, GPS o reglas de validación inválidos. Puede incluir errors por campo.",
            ["500"] = "Fallo interno; conserve el identificador de la respuesta para diagnóstico.",
            ["503"] = "Persistencia temporalmente no disponible; reintente conservando el UUID."
        };
        if (!anonima || ruta == "api/sesion/iniciar") errores["401"] = "Sesión ausente, vencida o credenciales inválidas.";
        if (!anonima || escritura) errores["403"] = "Permiso, obra o token antifalsificación no autorizados.";
        if (escritura)
        {
            errores["404"] = "Recurso o referencia no encontrado.";
            errores["409"] = "Conflicto, duplicidad, concurrencia o UUID reutilizado con otros datos.";
            errores["413"] = "El cuerpo excede el límite configurado.";
        }
        if (ruta.Contains('{')) errores["404"] = "Recurso no encontrado.";
        errores["429"] = "Límite temporal por identidad/operación o concurrencia; respete Retry-After y conserve el UUID.";
        foreach (var (codigo, mensaje) in errores)
            operacion.Responses[codigo] = new OpenApiResponse
            {
                Description = mensaje,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = problema }
                }
            };

        foreach (var (codigo, respuesta) in operacion.Responses)
        {
            if (respuesta is not OpenApiResponse respuestaConcreta) continue;
            if (codigo == "201")
            {
                respuestaConcreta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                respuestaConcreta.Headers["Location"] = new OpenApiHeader
                { Description = "Dirección del recurso creado.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            }
            if (codigo == "429")
            {
                respuestaConcreta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                respuestaConcreta.Headers["Retry-After"] = new OpenApiHeader
                { Description = "Segundos mínimos antes del reintento.", Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } };
            }
            if (codigo == "200" && paginada)
            {
                respuestaConcreta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                foreach (var cabecera in new[] { "X-Pagina", "X-Tamano-Pagina" })
                    respuestaConcreta.Headers[cabecera] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } };
            }
        }
    }

    private static void AgregarPaginacion(OpenApiOperation operacion, string nombre, int predeterminado, int? maximo)
    {
        operacion.Parameters!.Add(new OpenApiParameter
        {
            Name = nombre, In = ParameterLocation.Query,
            Description = $"Predeterminado: {predeterminado}." + (maximo is { } tope ? $" Máximo: {tope}." : string.Empty),
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Minimum = "1",
                Maximum = maximo?.ToString(CultureInfo.InvariantCulture)
            }
        });
    }

    private static string CrearIdentificador(string metodo, string ruta)
    {
        var verbo = metodo switch { "GET" => "Consultar", "POST" => "Registrar", "PUT" => "Actualizar", "PATCH" => "Modificar", "DELETE" => "Eliminar", _ => metodo };
        return verbo + string.Concat(ruta.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(segmento =>
        {
            var parametro = segmento.StartsWith('{');
            var nombre = segmento.Trim('{', '}').Split(':')[0];
            return (parametro ? "Por" : string.Empty) + string.Concat(nombre.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        }));
    }
}
