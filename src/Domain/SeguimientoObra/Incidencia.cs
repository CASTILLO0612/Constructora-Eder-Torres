namespace ConstructoraEderTorres.Dominio.SeguimientoObra;

public sealed class Incidencia
{
    public long IdIncidencia { get; set; }

    public int IdObra { get; set; }

    public string TipoIncidencia { get; set; } = null!;

    public DateTime InicioEn { get; set; }

    public DateTime? FinEn { get; set; }

    public decimal? HorasPerdidas { get; set; }

    public string Descripcion { get; set; } = null!;

    public string? Justificacion { get; set; }

    public string Estado { get; set; } = null!;

    public int IdUsuarioRegistro { get; set; }

    public Guid UuidRegistro { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? SincronizadoEn { get; set; }
}
