namespace ConstructoraEderTorres.Dominio.Personal;

public sealed class TrabajadorCuadrilla
{
    public int IdTrabajadorCuadrilla { get; set; }

    public int IdAsignacion { get; set; }

    public int IdCuadrillaObra { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }
}
