namespace ConstructoraEderTorres.Presentacion.Diagnostico;

public sealed class OpcionesObservabilidad
{
    public const string Seccion = "Observabilidad";
    public bool Habilitada { get; init; } = true;
    public string NombreServicio { get; init; } = "constructora-eder-torres-api";
    public double FraccionMuestreo { get; init; } = 0.1;
    public string? DestinoOtlp { get; init; }
    public string? CabecerasOtlp { get; init; }

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(NombreServicio) || NombreServicio.Length > 100
            || NombreServicio.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new InvalidOperationException("Observabilidad:NombreServicio debe ser un identificador de servicio.");
        if (!double.IsFinite(FraccionMuestreo) || FraccionMuestreo is < 0 or > 1)
            throw new InvalidOperationException("Observabilidad:FraccionMuestreo debe estar entre 0 y 1.");
        if (!string.IsNullOrWhiteSpace(DestinoOtlp)
            && (!Uri.TryCreate(DestinoOtlp, UriKind.Absolute, out var destino)
                || destino.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(destino.UserInfo)
                || !string.IsNullOrEmpty(destino.Query) || !string.IsNullOrEmpty(destino.Fragment)
                || (destino.Scheme == "http" && !destino.IsLoopback)))
            throw new InvalidOperationException("Observabilidad:DestinoOtlp requiere HTTPS, o HTTP local, sin credenciales, consulta ni fragmento.");
    }
}
