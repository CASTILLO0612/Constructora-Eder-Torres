namespace ConstructoraEderTorres.Dominio.Asistencia;

public sealed class RegistroAsistencia
{
    public long IdAsistencia { get; set; }

    public int IdAsignacion { get; set; }

    public DateOnly FechaJornada { get; set; }

    public DateTime EntradaEn { get; set; }

    public DateTime? SalidaEn { get; set; }

    public decimal? LatitudEntrada { get; set; }

    public decimal? LongitudEntrada { get; set; }

    public decimal? LatitudSalida { get; set; }

    public decimal? LongitudSalida { get; set; }

    public decimal? PrecisionGpsEntradaMetros { get; set; }

    public decimal? PrecisionGpsSalidaMetros { get; set; }

    public string Estado { get; set; } = null!;

    public int IdUsuarioRegistro { get; set; }

    public Guid UuidRegistro { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? SincronizadoEn { get; set; }
}
