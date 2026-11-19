using System.Text;

namespace ConstructoraEderTorres.Aplicacion.Comun;

public static class BusquedaTexto
{
    public const int LongitudMinima = 3;
    public const int LongitudMaxima = 120;
    public const int MaximoPalabras = 8;

    public static string Normalizar(string valor) => valor.Normalize(NormalizationForm.FormC)
        .ToLowerInvariant().Replace('á', 'a').Replace('é', 'e').Replace('í', 'i')
        .Replace('ó', 'o').Replace('ú', 'u').Replace('ü', 'u').Replace('ñ', 'n');

    public static string[] Palabras(string? valor) => Normalizar(valor ?? string.Empty)
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();

    public static bool EsValida(string? valor) => string.IsNullOrWhiteSpace(valor)
        || valor.Trim().Length is >= LongitudMinima and <= LongitudMaxima
            && Palabras(valor).Length <= MaximoPalabras;
}
