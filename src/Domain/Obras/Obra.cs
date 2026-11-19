namespace ConstructoraEderTorres.Dominio.Obras;

public sealed class Obra
{
    public int IdObra { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string Direccion { get; set; } = null!;

    public decimal? Latitud { get; set; }

    public decimal? Longitud { get; set; }

    public decimal? RadioAsistenciaMetros { get; set; }

    public DateOnly FechaInicioPlan { get; set; }

    public DateOnly FechaFinPlan { get; set; }

    public DateOnly? FechaInicioReal { get; set; }

    public DateOnly? FechaFinReal { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime CreadoEn { get; set; }
}
