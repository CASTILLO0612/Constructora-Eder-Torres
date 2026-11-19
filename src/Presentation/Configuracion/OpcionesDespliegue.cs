using System.Net;

namespace ConstructoraEderTorres.Presentacion.Configuracion;

public sealed class CertificadoProteccion
{
    public string Ruta { get; init; } = "";
    public string? Contrasena { get; init; }
}

public sealed class OpcionesDespliegue
{
    public const string Seccion = "Seguridad:Despliegue";
    public string NombreAplicacion { get; init; } = "ConstructoraEderTorres";
    public string? RutaClaves { get; init; }
    public CertificadoProteccion Certificado { get; init; } = new();
    public CertificadoProteccion[] CertificadosAnteriores { get; init; } = [];
    public string[] ProxiesConfiables { get; init; } = [];
    public int MaximoSaltosProxy { get; init; } = 1;
    public TimeSpan TiempoVerificacionArranque { get; init; } = TimeSpan.FromSeconds(15);

    public void Validar(IConfiguration configuracion, bool produccion)
    {
        if (string.IsNullOrWhiteSpace(NombreAplicacion) || NombreAplicacion.Length > 150)
            throw new InvalidOperationException("Configure un nombre estable para la protección de sesiones.");
        if (MaximoSaltosProxy is < 1 or > 5 || TiempoVerificacionArranque <= TimeSpan.Zero
            || TiempoVerificacionArranque > TimeSpan.FromMinutes(1))
            throw new InvalidOperationException("La configuración de proxy o verificación de arranque no es válida.");
        foreach (var proxy in ProxiesConfiables)
            if (!IPAddress.TryParse(proxy, out var ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("Cada proxy de confianza debe ser una dirección IP explícita.");
        if (!string.IsNullOrWhiteSpace(RutaClaves) && string.IsNullOrWhiteSpace(Certificado.Ruta))
            throw new InvalidOperationException("La persistencia explícita de claves requiere un certificado de cifrado.");
        if (!produccion) return;
        var hosts = configuracion["AllowedHosts"]?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (hosts.Length == 0 || hosts.Any(h => h.Contains('*') || Uri.CheckHostName(h) == UriHostNameType.Unknown))
            throw new InvalidOperationException("Producción requiere AllowedHosts con nombres de host explícitos.");
        var sesion = configuracion.GetSection(OpcionesSesion.Seccion).Get<OpcionesSesion>() ?? new();
        sesion.Validar();
        if (sesion.PoliticaSeguridadCookie != "Always" || sesion.OrigenesCorsPermitidos.Any(o => !o.StartsWith("https://", StringComparison.Ordinal)))
            throw new InvalidOperationException("Producción requiere cookies Secure y orígenes CORS HTTPS exactos.");
        var evidencias = configuracion["Evidencias:RutaRaiz"];
        if (string.IsNullOrWhiteSpace(RutaClaves) || !Path.IsPathFullyQualified(RutaClaves)
            || string.IsNullOrWhiteSpace(evidencias) || !Path.IsPathFullyQualified(evidencias)
            || !Path.IsPathFullyQualified(Certificado.Ruta))
            throw new InvalidOperationException("Producción requiere rutas absolutas de claves, certificado y evidencias en almacenamiento persistente.");
        var comparacion = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var claves = Path.GetFullPath(RutaClaves).TrimEnd(Path.DirectorySeparatorChar);
        var archivos = Path.GetFullPath(evidencias).TrimEnd(Path.DirectorySeparatorChar);
        if (claves.Equals(archivos, comparacion) || claves.StartsWith(archivos + Path.DirectorySeparatorChar, comparacion)
            || archivos.StartsWith(claves + Path.DirectorySeparatorChar, comparacion))
            throw new InvalidOperationException("Las claves de sesión y las evidencias requieren carpetas separadas.");
    }
}
