namespace ConstructoraEderTorres.Dominio.Personal;

public sealed class AsignacionTrabajadorObra
{
    public int IdAsignacion { get; set; }

    public int IdTrabajador { get; set; }

    public int IdObra { get; set; }

    public int? IdEspecialidad { get; set; }

    public string TipoContratacion { get; set; } = null!;

    public DateOnly FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }

    public bool EsResponsable { get; set; }

    public string Estado { get; set; } = null!;
}
