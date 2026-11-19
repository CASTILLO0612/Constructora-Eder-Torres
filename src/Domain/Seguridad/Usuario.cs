namespace ConstructoraEderTorres.Dominio.Seguridad;

public sealed class Usuario
{
    public int IdUsuario { get; set; }

    public int? IdTrabajador { get; set; }

    public string NombreUsuario { get; set; } = null!;

    public string ContrasenaHash { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public DateTime CreadoEn { get; set; }

    public DateTime? UltimoAcceso { get; set; }
}
