namespace ConstructoraEderTorres.Dominio.Herramientas;

public sealed class IncidenciaHerramienta
{
    public long IdIncidenciaHerramienta { get; set; }

    public long IdAsignacionHerramienta { get; set; }

    public string TipoIncidencia { get; set; } = null!;

    public DateTime FechaHora { get; set; }

    public string Descripcion { get; set; } = null!;

    public string? Resolucion { get; set; }

    public decimal? CostoEstimado { get; set; }

    public int IdUsuarioRegistro { get; set; }

    public Guid UuidRegistro { get; set; }

    public string Estado { get; set; } = null!;
}
