namespace ConstructoraEderTorres.Dominio.Herramientas;

public sealed class AsignacionHerramienta
{
    public long IdAsignacionHerramienta { get; set; }

    public int IdHerramienta { get; set; }

    public int IdAsignacionTrabajadorObra { get; set; }

    public int IdUsuarioEntrega { get; set; }

    public DateTime EntregadaEn { get; set; }

    public string CondicionEntrega { get; set; } = null!;

    public DateTime? DevueltaEn { get; set; }

    public string? CondicionDevolucion { get; set; }

    public string Estado { get; set; } = null!;

    public Guid UuidRegistro { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? SincronizadoEn { get; set; }
}
