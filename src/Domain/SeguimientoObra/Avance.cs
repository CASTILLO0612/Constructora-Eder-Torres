namespace ConstructoraEderTorres.Dominio.SeguimientoObra;

public sealed class Avance
{
    public long IdAvance { get; set; }

    public int IdActividad { get; set; }

    public DateOnly FechaAvance { get; set; }

    public decimal CantidadEjecutada { get; set; }

    public string? Observaciones { get; set; }

    public string Estado { get; set; } = null!;

    public int IdUsuarioRegistro { get; set; }

    public Guid UuidRegistro { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? SincronizadoEn { get; set; }
}
