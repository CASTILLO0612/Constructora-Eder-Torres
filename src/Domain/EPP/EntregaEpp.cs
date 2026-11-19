namespace ConstructoraEderTorres.Dominio.EPP;

public sealed class EntregaEpp
{
    public long IdEntregaEpp { get; set; }

    public int IdEpp { get; set; }

    public int IdAsignacion { get; set; }

    public int IdUsuarioEntrega { get; set; }

    public DateTime EntregadoEn { get; set; }

    public string CondicionEntrega { get; set; } = null!;

    public DateTime? DevueltoEn { get; set; }

    public string? CondicionDevolucion { get; set; }

    public string Estado { get; set; } = null!;

    public string? RutaConstancia { get; set; }

    public Guid UuidRegistro { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? SincronizadoEn { get; set; }
}
