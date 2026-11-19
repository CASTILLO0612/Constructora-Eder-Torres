namespace ConstructoraEderTorres.Dominio.Herramientas;

public sealed class TipoHerramienta
{
    public int IdTipoHerramienta { get; set; }

    public int IdCategoria { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }
}
