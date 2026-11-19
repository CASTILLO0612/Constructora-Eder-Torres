namespace ConstructoraEderTorres.Presentacion.Configuracion;

public sealed class OpcionesSesion
{
    public const string Seccion = "Seguridad:Sesion";

    public string NombreCookie { get; init; } = "cet_sesion";

    public int MinutosInactividad { get; init; } = 30;

    public int HorasDuracionMaxima { get; init; } = 8;

    public string PoliticaSameSite { get; init; } = "Lax";

    public string PoliticaSeguridadCookie { get; init; } = "Always";

    public string[] OrigenesCorsPermitidos { get; init; } = [];

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(NombreCookie) || NombreCookie.Any(c => char.IsWhiteSpace(c) || c is ';' or '=' or ','))
            throw new InvalidOperationException("Seguridad:Sesion:NombreCookie no es válido.");
        if (MinutosInactividad < 1 || HorasDuracionMaxima is < 1 or > 8760 || MinutosInactividad > HorasDuracionMaxima * 60)
            throw new InvalidOperationException("Los tiempos de sesión deben ser positivos y la inactividad no puede superar la duración máxima.");
        if (PoliticaSameSite is not ("Lax" or "Strict" or "None") || PoliticaSeguridadCookie is not ("Always" or "SameAsRequest"))
            throw new InvalidOperationException("Las políticas de cookie admitidas son SameSite=Lax/Strict/None y seguridad=Always/SameAsRequest.");
        if (PoliticaSameSite == "None" && PoliticaSeguridadCookie != "Always")
            throw new InvalidOperationException("SameSite=None requiere una cookie Secure (Always).");
        foreach (var origen in OrigenesCorsPermitidos)
        {
            if (!Uri.TryCreate(origen, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
                || origen != uri.GetLeftPart(UriPartial.Authority) || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException("Cada origen CORS debe ser un origen HTTP/HTTPS exacto, sin ruta, comodines ni credenciales.");
        }
    }
}
