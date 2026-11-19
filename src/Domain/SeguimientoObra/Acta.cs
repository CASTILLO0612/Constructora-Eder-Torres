namespace ConstructoraEderTorres.Dominio.SeguimientoObra;

public sealed class Acta
{
    public int IdActa { get; set; }

    public int IdObra { get; set; }

    public string NumeroActa { get; set; } = null!;

    public DateOnly PeriodoInicio { get; set; }

    public DateOnly PeriodoFin { get; set; }

    public string? Observaciones { get; set; }

    public string? MejorasPropuestas { get; set; }

    public string? SolicitudesCambio { get; set; }

    public string Estado { get; set; } = null!;

    public int IdUsuarioCreador { get; set; }

    public Guid UuidRegistro { get; set; }

    public DateTime CreadoEn { get; set; }
}
