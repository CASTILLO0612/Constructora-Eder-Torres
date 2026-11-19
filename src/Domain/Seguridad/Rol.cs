namespace ConstructoraEderTorres.Dominio.Seguridad;

public sealed class Rol
{
    public int IdRol { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }
}
