using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace ConstructoraEderTorres.Aplicacion.Comun;

public static class ValidadorContratoSolicitud
{
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Propiedad, bool Obligatoria)[]> Propiedades = new();

    public static IReadOnlyDictionary<string, string[]> Validar(object objeto, int maximoElementos = 200, bool validarAnidados = true)
    {
        var errores = new Dictionary<string, string[]>();
        ValidarObjeto(objeto, string.Empty, errores, maximoElementos, validarAnidados);
        return errores;
    }

    private static void ValidarObjeto(object objeto, string prefijo, Dictionary<string, string[]> errores, int maximoElementos, bool validarAnidados)
    {
        var propiedades = Propiedades.GetOrAdd(objeto.GetType(), tipo =>
        {
            var nulabilidad = new NullabilityInfoContext();
            return tipo.GetProperties().Select(p => (p, nulabilidad.Create(p).ReadState == NullabilityState.NotNull)).ToArray();
        });

        foreach (var (propiedad, obligatoria) in propiedades)
        {
            var nombre = prefijo + JsonNamingPolicy.CamelCase.ConvertName(propiedad.Name);
            var valor = propiedad.GetValue(objeto);
            if (valor is null)
            {
                if (obligatoria) errores[nombre] = ["El campo es obligatorio."];
                continue;
            }

            if (valor is DateTime instante && (instante == default || instante.Kind != DateTimeKind.Utc))
                errores[nombre] = ["Indique una fecha y hora válida en UTC con sufijo Z."];
            else if (valor is decimal numero && !propiedad.Name.StartsWith("Latitud", StringComparison.Ordinal)
                && !propiedad.Name.StartsWith("Longitud", StringComparison.Ordinal)
                && !propiedad.Name.StartsWith("PrecisionGps", StringComparison.Ordinal) && decimal.Round(numero, 2) != numero)
                errores[nombre] = ["El campo admite como máximo dos decimales conforme al esquema de datos."];
            else if (valor is DateOnly fecha && fecha == default)
                errores[nombre] = ["Indique una fecha válida."];
            else if (propiedad.Name.StartsWith("Id", StringComparison.Ordinal) && (valor is int id && id <= 0 || valor is long idLargo && idLargo <= 0))
                errores[nombre] = ["El identificador debe ser mayor que cero."];
            else if (valor is IEnumerable elementos && valor is not string && valor is not byte[])
            {
                var indice = 0;
                foreach (var elemento in elementos)
                {
                    if (indice >= maximoElementos)
                    {
                        errores[nombre] = [$"La lista admite como máximo {maximoElementos} elementos."];
                        break;
                    }
                    if (validarAnidados && elemento is null) errores[$"{nombre}[{indice}]"] = ["No se admiten elementos nulos."];
                    else if (validarAnidados && elemento is not null && elemento.GetType().Namespace?.StartsWith("ConstructoraEderTorres.Aplicacion.Contratos", StringComparison.Ordinal) == true)
                        ValidarObjeto(elemento, $"{nombre}[{indice}].", errores, maximoElementos, validarAnidados);
                    indice++;
                }
            }
        }
    }
}
