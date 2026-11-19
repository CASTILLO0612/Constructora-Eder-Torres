using System.Security.Cryptography;

namespace ConstructoraEderTorres.Respaldos;

public static class RutasRespaldo
{
    public static StringComparison Comparacion => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    public static string Absoluta(string ruta)
    {
        if (!Path.IsPathFullyQualified(ruta)) throw new ExcepcionRespaldo("ruta_invalida", "Las rutas operativas deben ser absolutas.");
        var completa = Path.GetFullPath(ruta);
        if (string.Equals(completa, Path.GetPathRoot(completa), Comparacion))
            throw new ExcepcionRespaldo("raiz_de_volumen_no_admitida", "Seleccione una carpeta operativa, no la raíz del volumen.");
        return completa.TrimEnd(Path.DirectorySeparatorChar);
    }

    public static bool Contiene(string raiz, string ruta) => ruta.Equals(raiz, Comparacion)
        || ruta.StartsWith(raiz + Path.DirectorySeparatorChar, Comparacion);

    public static string Resolver(string raiz, string relativa)
    {
        if (string.IsNullOrWhiteSpace(relativa) || Path.IsPathRooted(relativa)
            || relativa.Split('/', '\\').Any(s => s is "" or "." or ".." || s.Contains(':')))
            throw new ExcepcionRespaldo("ruta_evidencia_invalida", "Una ruta de evidencia no es relativa o contiene segmentos no admitidos.");
        var destino = Path.GetFullPath(Path.Combine(raiz, relativa.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        if (!Contiene(raiz, destino)) throw new ExcepcionRespaldo("ruta_fuera_de_raiz", "El archivo excede la raíz autorizada.");
        var elemento = destino;
        while (Contiene(raiz, elemento))
        {
            if ((File.Exists(elemento) || Directory.Exists(elemento)) && (File.GetAttributes(elemento) & FileAttributes.ReparsePoint) != 0)
                throw new ExcepcionRespaldo("enlace_no_admitido", "El respaldo no admite enlaces simbólicos ni puntos de reanálisis.");
            elemento = Path.GetDirectoryName(elemento)!;
            if (elemento is null) break;
        }
        return destino;
    }

    public static async Task<string> HuellaAsync(string archivo, CancellationToken cancelacion)
    {
        await using var flujo = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(flujo, cancelacion));
    }

    public static async Task CopiarAsync(string origen, string destino, CancellationToken cancelacion)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await using var entrada = new FileStream(origen, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        await using var salida = new FileStream(destino, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        await entrada.CopyToAsync(salida, cancelacion);
        await salida.FlushAsync(cancelacion);
    }
}
