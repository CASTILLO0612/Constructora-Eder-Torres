using System.Security.Cryptography;
using System.Text;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConstructoraEderTorres.Infraestructura.Evidencias;

public sealed class AlmacenamientoEvidenciasLocalConfigurable : IAlmacenamientoEvidencias
{
    private readonly string _raiz;
    private readonly ILogger<AlmacenamientoEvidenciasLocalConfigurable>? _registro;
    public long MaximoBytes { get; }

    public AlmacenamientoEvidenciasLocalConfigurable(IConfiguration configuracion, string raizContenido,
        ILogger<AlmacenamientoEvidenciasLocalConfigurable>? registro = null)
    {
        _registro = registro;
        var ruta = configuracion["Evidencias:RutaRaiz"];
        if (string.IsNullOrWhiteSpace(ruta))
            throw new InvalidOperationException("Configure Evidencias:RutaRaiz con una carpeta persistente fuera de wwwroot.");
        _raiz = Path.GetFullPath(ruta, raizContenido);
        var publica = Path.GetFullPath(Path.Combine(raizContenido, "wwwroot"));
        var comparacion = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(_raiz.TrimEnd(Path.DirectorySeparatorChar), publica, comparacion)
            || _raiz.StartsWith(publica + Path.DirectorySeparatorChar, comparacion))
            throw new InvalidOperationException("El almacenamiento de evidencias debe quedar fuera de wwwroot.");
        MaximoBytes = long.TryParse(configuracion["Evidencias:TamanoMaximoBytes"], out var limite) && limite > 0
            ? limite : throw new InvalidOperationException("Evidencias:TamanoMaximoBytes debe ser positivo.");
    }

    public async Task<(string RutaRelativa, string HuellaSha256)> GuardarEvidenciaAsync(
        int idObra, long idAvance, string nombreArchivoOriginal,
        ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default)
    {
        if (contenido.Length == 0 || contenido.Length > MaximoBytes)
            throw new ExcepcionPersistencia(ErrorNegocio.Validacion("tamano_evidencia_invalido", $"El archivo debe contener entre 1 y {MaximoBytes} bytes."));
        var extension = Path.GetExtension(nombreArchivoOriginal).ToLowerInvariant();
        if (!FormatoValido(extension, contenido.Span))
            throw new ExcepcionPersistencia(ErrorNegocio.Validacion("formato_evidencia_invalido", "El contenido no corresponde a un archivo JPEG, PNG, WebP, PDF o texto UTF-8 admitido."));

        var huella = Convert.ToHexStringLower(SHA256.HashData(contenido.Span));
        var rutaRelativa = $"obra-{idObra}/avance-{idAvance}/{Guid.NewGuid():N}{extension}";
        var destino = ResolverRuta(rutaRelativa);
        var temporal = destino + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            await using (var archivo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous))
            {
                await archivo.WriteAsync(contenido, cancelacion);
                await archivo.FlushAsync(cancelacion);
            }
            cancelacion.ThrowIfCancellationRequested();
            File.Move(temporal, destino, overwrite: false);
            return (rutaRelativa, huella);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            DiagnosticoAplicacion.FallosEvidencias.Add(1, new KeyValuePair<string, object?>("operacion", "escritura"));
            throw new ExcepcionPersistencia(new ErrorNegocio("almacenamiento_no_disponible",
                "El almacenamiento de evidencias no está disponible. Conserve el archivo y reintente después.", TipoError.NoDisponible), error);
        }
        finally
        {
            try { if (File.Exists(temporal)) File.Delete(temporal); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { _registro?.LogWarning(error, "No se pudo limpiar un archivo temporal de evidencia."); }
        }
    }

    public Task EliminarAsync(string rutaRelativa, CancellationToken cancelacion = default)
    {
        File.Delete(ResolverRuta(rutaRelativa));
        return Task.CompletedTask;
    }

    public Stream? AbrirLectura(string rutaRelativa)
    {
        var ruta = ResolverRuta(rutaRelativa);
        return File.Exists(ruta)
            ? new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
    }

    private string ResolverRuta(string relativa)
    {
        if (Path.IsPathRooted(relativa))
            throw new InvalidOperationException("La evidencia requiere una ruta relativa.");
        var ruta = Path.GetFullPath(Path.Combine(_raiz, relativa.Replace('/', Path.DirectorySeparatorChar)));
        if (!ruta.StartsWith(_raiz.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("La ruta queda fuera del almacenamiento de evidencias.");
        return ruta;
    }

    private static bool FormatoValido(string extension, ReadOnlySpan<byte> contenido)
    {
        switch (extension)
        {
            case ".jpg":
            case ".jpeg": return contenido.StartsWith(new byte[] { 0xff, 0xd8, 0xff });
            case ".png": return contenido.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            case ".webp": return contenido.Length >= 12 && contenido[..4].SequenceEqual("RIFF"u8) && contenido.Slice(8, 4).SequenceEqual("WEBP"u8);
            case ".pdf": return contenido.StartsWith("%PDF-"u8);
            case ".txt":
            case ".csv":
                try { return !contenido.Contains((byte)0) && new UTF8Encoding(false, true).GetCharCount(contenido) > 0; }
                catch (DecoderFallbackException) { return false; }
            default: return false;
        }
    }
}
