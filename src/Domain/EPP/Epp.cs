namespace ConstructoraEderTorres.Dominio.EPP;

public sealed class Epp
{
    public int IdEpp { get; set; }

    public int IdTipoEpp { get; set; }

    public string CodigoInterno { get; set; } = null!;

    public string? NumeroSerie { get; set; }

    public DateOnly FechaAdquisicion { get; set; }

    public string Estado { get; set; } = null!;

    public string Condicion { get; set; } = null!;
}
