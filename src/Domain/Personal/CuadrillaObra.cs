namespace ConstructoraEderTorres.Dominio.Personal;

public sealed class CuadrillaObra
{
    public int IdCuadrillaObra { get; set; }

    public int IdCuadrilla { get; set; }

    public int IdObra { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }

    public string Estado { get; set; } = null!;
}
