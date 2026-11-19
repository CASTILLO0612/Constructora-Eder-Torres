namespace ConstructoraEderTorres.Dominio.Seguridad;

public sealed class UsuarioRol
{
    public int IdUsuarioRol { get; set; }

    public int IdUsuario { get; set; }

    public int IdRol { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }
}
