namespace ConstructoraEderTorres.Dominio.SeguimientoObra;

public sealed class Evidencia
{
    public long IdEvidencia { get; set; }

    public long IdAvance { get; set; }

    public string TipoEvidencia { get; set; } = null!;

    public string RutaArchivo { get; set; } = null!;

    public string HuellaArchivo { get; set; } = null!;

    public DateTime CapturadaEn { get; set; }

    public decimal? Latitud { get; set; }

    public decimal? Longitud { get; set; }

    public int IdUsuarioRegistro { get; set; }

    public Guid UuidRegistro { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? SincronizadoEn { get; set; }
}
