namespace ConstructoraEderTorres.Dominio.Personal;

public sealed class Cuadrilla
{
    public int IdCuadrilla { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }
}
