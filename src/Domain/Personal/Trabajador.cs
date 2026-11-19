namespace ConstructoraEderTorres.Dominio.Personal;

public sealed class Trabajador
{
    public int IdTrabajador { get; set; }

    public int IdCargo { get; set; }

    public string Cedula { get; set; } = null!;

    public string PrimerNombre { get; set; } = null!;

    public string? SegundoNombre { get; set; }

    public string PrimerApellido { get; set; } = null!;

    public string? SegundoApellido { get; set; }

    public string? Telefono { get; set; }

    public string? Correo { get; set; }

    public string? Direccion { get; set; }

    public DateOnly FechaIngreso { get; set; }

    public string Estado { get; set; } = null!;
}
