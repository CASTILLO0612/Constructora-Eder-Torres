namespace ConstructoraEderTorres.Dominio.Personal;

public sealed class TrabajadorEspecialidad
{
    public int IdTrabajador { get; set; }

    public int IdEspecialidad { get; set; }

    public string? Nivel { get; set; }

    public DateOnly? FechaCertificacion { get; set; }

    public DateOnly? FechaVencimiento { get; set; }
}
