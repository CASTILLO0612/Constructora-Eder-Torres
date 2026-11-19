namespace ConstructoraEderTorres.Dominio.Herramientas;

public sealed class Herramienta
{
    public int IdHerramienta { get; set; }

    public int IdTipoHerramienta { get; set; }

    public string CodigoInterno { get; set; } = null!;

    public string? NumeroSerie { get; set; }

    public string? Marca { get; set; }

    public string? Modelo { get; set; }

    public DateOnly FechaAdquisicion { get; set; }

    public string Estado { get; set; } = null!;

    public string Condicion { get; set; } = null!;
}
