namespace ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;

public sealed class OpcionesCapturaAsistencia
{
    public const string Seccion = "Asistencia";

    public string ZonaHorariaJornada { get; init; } = "America/Managua";

    public TimeSpan ToleranciaRelojFuturo { get; init; } = TimeSpan.FromMinutes(2);

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(ZonaHorariaJornada) || ToleranciaRelojFuturo < TimeSpan.Zero)
            throw new InvalidOperationException("Configure la zona horaria de asistencia y una tolerancia de reloj no negativa.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(ZonaHorariaJornada);
    }
}
