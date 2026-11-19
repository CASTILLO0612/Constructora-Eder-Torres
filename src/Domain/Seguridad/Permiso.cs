namespace ConstructoraEderTorres.Dominio.Seguridad;

public sealed class Permiso
{
    public int IdPermiso { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }
}
