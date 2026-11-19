namespace ConstructoraEderTorres.Dominio.EPP;

public sealed class MovimientoExistenciasEpp
{
    public long IdMovimientoEpp { get; set; }

    public int IdEpp { get; set; }

    public long? IdEntregaEpp { get; set; }

    public string TipoMovimiento { get; set; } = null!;

    public DateTime FechaHora { get; set; }

    public int IdUsuario { get; set; }

    public string? Observacion { get; set; }

    public Guid UuidRegistro { get; set; }
}
