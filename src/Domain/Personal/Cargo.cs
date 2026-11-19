namespace ConstructoraEderTorres.Dominio.Personal;

public sealed class Cargo
{
    public int IdCargo { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }
}
