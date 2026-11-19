namespace ConstructoraEderTorres.Dominio.SeguimientoObra;

public sealed class Actividad
{
    public int IdActividad { get; set; }

    public int IdObra { get; set; }

    public int? IdAsignacionResponsable { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string UnidadMedida { get; set; } = null!;

    public decimal CantidadPlanificada { get; set; }

    public DateOnly FechaInicioPlan { get; set; }

    public DateOnly FechaFinPlan { get; set; }

    public string Estado { get; set; } = null!;
}
